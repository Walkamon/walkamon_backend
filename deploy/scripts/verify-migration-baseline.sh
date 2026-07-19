#!/usr/bin/env bash
set -Eeuo pipefail

if ! docker inspect walkamon-db >/dev/null 2>&1; then
  echo "The walkamon-db container is not available." >&2
  exit 1
fi

result=$(
  docker exec walkamon-db bash -lc '
    /opt/mssql-tools18/bin/sqlcmd \
      -S localhost \
      -U sa \
      -P "$MSSQL_SA_PASSWORD" \
      -C \
      -b \
      -d Walkamon \
      -h -1 \
      -W \
      -w 65535 \
      -s "|" \
      -Q "
        SET NOCOUNT ON;
        SELECT
          (SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped = 0),
          (
            SELECT SUM(p.rows)
            FROM sys.tables AS t
            INNER JOIN sys.partitions AS p
              ON p.object_id = t.object_id
             AND p.index_id IN (0, 1)
            WHERE t.is_ms_shipped = 0
          ),
          CAST(
            (
              SELECT SUM(size) * 8.0 / 1024.0
              FROM sys.database_files
              WHERE type_desc = NCHAR(82) + NCHAR(79) + NCHAR(87) + NCHAR(83)
            )
            AS decimal(18, 2)
          ),
          CASE
            WHEN SUSER_ID(
              NCHAR(119) + NCHAR(97) + NCHAR(108) + NCHAR(107)
              + NCHAR(97) + NCHAR(109) + NCHAR(111) + NCHAR(110)
              + NCHAR(95) + NCHAR(97) + NCHAR(112) + NCHAR(112)
            ) IS NULL THEN 0
            ELSE 1
          END;
      "
  ' | tr -d '\r' | sed '/^[[:space:]]*$/d' | tail -n 1
)

IFS='|' read -r table_count row_count data_mb app_login_exists <<<"$result"
table_count=$(xargs <<<"$table_count")
row_count=$(xargs <<<"$row_count")
data_mb=$(xargs <<<"$data_mb")
app_login_exists=$(xargs <<<"$app_login_exists")

if [[ ! "$table_count" =~ ^[0-9]+$ || ! "$row_count" =~ ^[0-9]+$ ]]; then
  echo "Could not parse database verification result." >&2
  exit 1
fi
if [[ "$table_count" != 50 ]]; then
  echo "Unexpected table count after restore: $table_count (expected 50)." >&2
  exit 1
fi
if (( row_count < 80 )); then
  echo "Unexpected row count after restore: $row_count (expected at least 80)." >&2
  exit 1
fi
if [[ "$app_login_exists" != 1 ]]; then
  echo "The walkamon_app login is missing after provisioning." >&2
  exit 1
fi

echo "DATABASE_RESTORE_OK tables=$table_count rows=$row_count data_mb=$data_mb app_login=present"
