#!/usr/bin/env bash
set -Eeuo pipefail

exec 9>/run/lock/walkamon-backup.lock
flock -n 9 || exit 0

ENV_FILE=/etc/walkamon/walkamon.env
set -a
source "$ENV_FILE"
set +a

timestamp=$(date -u +%Y%m%dT%H%M%SZ)
backup_name="Walkamon_${timestamp}.bak"
container_backup="/var/opt/mssql/backup/${backup_name}"
host_backup="/srv/walkamon/mssql/backup/${backup_name}"

install -o 10001 -g 0 -m 0750 -d /srv/walkamon/mssql/backup

docker exec walkamon-db /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C -b \
  -Q "BACKUP DATABASE [Walkamon] TO DISK=N'${container_backup}' WITH COPY_ONLY, INIT, CHECKSUM, STATS=10; RESTORE VERIFYONLY FROM DISK=N'${container_backup}' WITH CHECKSUM;"

if [[ ! -s "$host_backup" ]]; then
  echo "SQL backup was not created: $host_backup" >&2
  exit 1
fi

if ! restic cat config >/dev/null 2>&1; then
  restic init
fi

restic backup \
  /srv/walkamon/mssql/backup \
  /etc/walkamon \
  /opt/walkamon/compose.prod.yml \
  --tag walkamon-production

restic forget --prune --keep-daily 7 --keep-weekly 4 --keep-monthly 6
find /srv/walkamon/mssql/backup -type f -name 'Walkamon_*.bak' -mtime +7 -delete
