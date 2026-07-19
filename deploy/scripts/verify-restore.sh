#!/usr/bin/env bash
set -Eeuo pipefail

exec 9>/run/lock/walkamon-backup.lock
flock -w 3600 9

ENV_FILE=/etc/walkamon/walkamon.env
set -a
source "$ENV_FILE"
set +a

latest_path=$(
  restic ls latest \
    | grep -E '^/srv/walkamon/mssql/backup/Walkamon_[0-9]{8}T[0-9]{6}Z\.bak$' \
    | sort \
    | tail -n 1 \
    || true
)
if [[ -z "$latest_path" ]]; then
  echo "No timestamped Walkamon backup is available in the latest R2 snapshot." >&2
  exit 1
fi

restore_host=$(mktemp /srv/walkamon/mssql/backup/Walkamon_R2RestoreVerify_XXXXXX.bak)
restore_name=$(basename "$restore_host")
backup_path="/var/opt/mssql/backup/${restore_name}"
sqlcmd=(docker exec walkamon-db /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C -b)

cleanup() {
  "${sqlcmd[@]}" -Q "IF DB_ID(N'WalkamonRestoreVerify') IS NOT NULL BEGIN ALTER DATABASE [WalkamonRestoreVerify] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [WalkamonRestoreVerify]; END;" >/dev/null 2>&1 || true
  rm -f "$restore_host"
}
trap cleanup EXIT

restic dump latest "$latest_path" > "$restore_host"
if [[ ! -s "$restore_host" ]]; then
  echo "R2 restore produced an empty SQL backup: $latest_path" >&2
  exit 1
fi
chown 10001:0 "$restore_host"
chmod 0640 "$restore_host"

"${sqlcmd[@]}" -Q "RESTORE VERIFYONLY FROM DISK=N'${backup_path}' WITH CHECKSUM; IF DB_ID(N'WalkamonRestoreVerify') IS NOT NULL BEGIN ALTER DATABASE [WalkamonRestoreVerify] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [WalkamonRestoreVerify]; END; RESTORE DATABASE [WalkamonRestoreVerify] FROM DISK=N'${backup_path}' WITH MOVE N'Walkamon' TO N'/var/opt/mssql/data/WalkamonRestoreVerify.mdf', MOVE N'Walkamon_log' TO N'/var/opt/mssql/log/WalkamonRestoreVerify_log.ldf', RECOVERY; DBCC CHECKDB(N'WalkamonRestoreVerify') WITH NO_INFOMSGS;"

echo "R2 restore verification passed: $latest_path"
