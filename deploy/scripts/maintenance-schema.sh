#!/usr/bin/env bash
set -Eeuo pipefail

if [[ $# -ne 1 || ! -f "$1" ]]; then
  echo "Usage: sudo $0 /path/to/approved-schema-change.sql" >&2
  exit 2
fi

script_dir=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
source_sql=$(realpath "$1")
container_sql="/tmp/walkamon-schema-$$.sql"
ENV_FILE=/etc/walkamon/walkamon.env
COMPOSE_FILE=/opt/walkamon/compose.prod.yml
VERIFY_DATABASE=WalkamonSchemaVerify
timestamp=$(date -u +%Y%m%dT%H%M%SZ)
backup_name="Walkamon_pre_schema_${timestamp}.bak"
container_backup="/var/opt/mssql/backup/${backup_name}"
host_backup="/srv/walkamon/mssql/backup/${backup_name}"

if [[ ! -r "$ENV_FILE" ]]; then
  echo "Missing or unreadable production environment: $ENV_FILE" >&2
  exit 1
fi

set -a
source "$ENV_FILE"
set +a

sqlcmd() {
  docker exec walkamon-db /bin/bash -lc \
    'exec /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C -b "$@"' \
    walkamon-sqlcmd "$@"
}

docker cp "$source_sql" "walkamon-db:${container_sql}"
cleanup() {
  docker exec --user root walkamon-db rm -f "$container_sql" >/dev/null 2>&1 || true
  sqlcmd -Q "IF DB_ID(N'${VERIFY_DATABASE}') IS NOT NULL BEGIN ALTER DATABASE [${VERIFY_DATABASE}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [${VERIFY_DATABASE}]; END;" >/dev/null 2>&1 || true
  docker compose --env-file "$ENV_FILE" -f "$COMPOSE_FILE" up -d api >/dev/null 2>&1 || true
}
trap cleanup EXIT

docker compose --env-file "$ENV_FILE" -f "$COMPOSE_FILE" stop --timeout 45 api

sqlcmd -Q "BACKUP DATABASE [Walkamon] TO DISK=N'${container_backup}' WITH COPY_ONLY, INIT, CHECKSUM, STATS=10; RESTORE VERIFYONLY FROM DISK=N'${container_backup}' WITH CHECKSUM;"
if [[ ! -s "$host_backup" ]]; then
  echo "Verified pre-schema backup was not created: $host_backup" >&2
  exit 1
fi

sqlcmd -Q "IF DB_ID(N'${VERIFY_DATABASE}') IS NOT NULL BEGIN ALTER DATABASE [${VERIFY_DATABASE}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [${VERIFY_DATABASE}]; END; RESTORE DATABASE [${VERIFY_DATABASE}] FROM DISK=N'${container_backup}' WITH MOVE N'Walkamon' TO N'/var/opt/mssql/data/${VERIFY_DATABASE}.mdf', MOVE N'Walkamon_log' TO N'/var/opt/mssql/log/${VERIFY_DATABASE}_log.ldf', RECOVERY;"
sqlcmd -d "$VERIFY_DATABASE" -i "$container_sql"
sqlcmd -d "$VERIFY_DATABASE" -Q "IF COL_LENGTH('dbo.pvp_matches', 'item_slot_limit') IS NULL OR COL_LENGTH('dbo.pvp_matches', 'last_event_sequence') IS NULL OR COL_LENGTH('dbo.pvp_matches', 'row_version') IS NULL OR COL_LENGTH('dbo.pvp_matches', 'rule_version') IS NULL OR COL_LENGTH('dbo.pvp_matches', 'speed_min_bps') IS NULL OR COL_LENGTH('dbo.pvp_matches', 'speed_max_bps') IS NULL OR OBJECT_ID('dbo.outbox_events', 'U') IS NULL THROW 51000, 'Schema verification failed on clone.', 1; DBCC CHECKDB(N'${VERIFY_DATABASE}') WITH NO_INFOMSGS;"
sqlcmd -Q "ALTER DATABASE [${VERIFY_DATABASE}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [${VERIFY_DATABASE}];"

sqlcmd -d Walkamon -i "$container_sql"
sqlcmd -d Walkamon -Q "IF COL_LENGTH('dbo.pvp_matches', 'item_slot_limit') IS NULL OR COL_LENGTH('dbo.pvp_matches', 'last_event_sequence') IS NULL OR COL_LENGTH('dbo.pvp_matches', 'row_version') IS NULL OR COL_LENGTH('dbo.pvp_matches', 'rule_version') IS NULL OR COL_LENGTH('dbo.pvp_matches', 'speed_min_bps') IS NULL OR COL_LENGTH('dbo.pvp_matches', 'speed_max_bps') IS NULL OR OBJECT_ID('dbo.outbox_events', 'U') IS NULL THROW 51000, 'Production schema verification failed.', 1; DBCC CHECKCONSTRAINTS WITH ALL_CONSTRAINTS;"

docker compose --env-file "$ENV_FILE" -f "$COMPOSE_FILE" up -d api
for _ in {1..36}; do
  if [[ "$(docker inspect --format '{{if .State.Health}}{{.State.Health.Status}}{{end}}' walkamon-api 2>/dev/null)" == "healthy" ]]; then
    trap - EXIT
    cleanup
    echo "SCHEMA_MAINTENANCE_OK backup=${host_backup}"
    exit 0
  fi
  sleep 5
done

docker compose --env-file "$ENV_FILE" -f "$COMPOSE_FILE" logs --tail=150 api >&2 || true
echo "The API did not become ready after the schema upgrade. Backup retained at: $host_backup" >&2
exit 1
