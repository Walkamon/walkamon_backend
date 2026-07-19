#!/usr/bin/env bash
set -Eeuo pipefail

ENV_FILE=/etc/walkamon/walkamon.env
set -a
source "$ENV_FILE"
set +a

threshold_mb=${WALKAMON_DB_WARNING_MB:-7168}
size_mb=$(docker exec walkamon-db /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C -b -h -1 -W \
  -Q "SET NOCOUNT ON; USE [Walkamon]; SELECT COALESCE(SUM(CONVERT(bigint, size)) * 8 / 1024, 0) FROM sys.database_files WHERE type_desc=N'ROWS';" \
  | tr -d '\r[:space:]')

if [[ ! "$size_mb" =~ ^[0-9]+$ ]]; then
  echo "Could not determine the Walkamon data-file size: $size_mb" >&2
  exit 1
fi

if (( size_mb >= threshold_mb )); then
  message="Walkamon SQL data files are ${size_mb} MB, at or above the ${threshold_mb} MB warning threshold. SQL Server Express has a 10 GB per-database data limit."
  logger --priority daemon.warning --tag walkamon-db-size "$message"
  echo "WARNING: $message" >&2
else
  echo "Walkamon SQL data-file size: ${size_mb} MB (warning at ${threshold_mb} MB)."
fi
