#!/usr/bin/env bash
set -Eeuo pipefail

ENV_FILE=/etc/walkamon/walkamon.env
set -a
source "$ENV_FILE"
set +a

latest=$(find /srv/walkamon/mssql/backup -type f -name 'Walkamon_*.bak' -printf '%T@ %f\n' \
  | sort -nr | awk 'NR==1 {print $2}')
if [[ -z "$latest" ]]; then
  echo "No local Walkamon backup is available for restore testing." >&2
  exit 1
fi

backup_path="/var/opt/mssql/backup/${latest}"
sqlcmd=(docker exec walkamon-db /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C -b)

"${sqlcmd[@]}" -Q "IF DB_ID(N'WalkamonRestoreVerify') IS NOT NULL BEGIN ALTER DATABASE [WalkamonRestoreVerify] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [WalkamonRestoreVerify]; END; RESTORE DATABASE [WalkamonRestoreVerify] FROM DISK=N'${backup_path}' WITH MOVE N'Walkamon' TO N'/var/opt/mssql/data/WalkamonRestoreVerify.mdf', MOVE N'Walkamon_log' TO N'/var/opt/mssql/log/WalkamonRestoreVerify_log.ldf', RECOVERY; DBCC CHECKDB(N'WalkamonRestoreVerify') WITH NO_INFOMSGS; ALTER DATABASE [WalkamonRestoreVerify] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [WalkamonRestoreVerify];"
