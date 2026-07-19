#!/usr/bin/env bash
set -Eeuo pipefail

if [[ $# -ne 1 || ! -f "$1" ]]; then
  echo "Usage: $0 /path/to/Walkamon.bak" >&2
  exit 2
fi

ENV_FILE=/etc/walkamon/walkamon.env
set -a
source "$ENV_FILE"
set +a

source_backup=$(realpath "$1")
target_backup=/srv/walkamon/mssql/backup/Walkamon_migration.bak
install -o 10001 -g 0 -m 0750 -d /srv/walkamon/mssql/backup
install -o 10001 -g 0 -m 660 "$source_backup" "$target_backup"

docker exec walkamon-db /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C -b \
  -Q "IF DB_ID(N'Walkamon') IS NOT NULL BEGIN ALTER DATABASE [Walkamon] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; END; RESTORE DATABASE [Walkamon] FROM DISK=N'/var/opt/mssql/backup/Walkamon_migration.bak' WITH MOVE N'Walkamon' TO N'/var/opt/mssql/data/Walkamon.mdf', MOVE N'Walkamon_log' TO N'/var/opt/mssql/log/Walkamon_log.ldf', REPLACE, RECOVERY; ALTER DATABASE [Walkamon] SET MULTI_USER;"

"$(dirname "$0")/provision-database.sh"
