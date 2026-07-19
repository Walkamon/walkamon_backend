#!/usr/bin/env bash
set -Eeuo pipefail

docker exec walkamon-db bash -lc '
  /opt/mssql-tools18/bin/sqlcmd \
    -S localhost \
    -U sa \
    -P "$MSSQL_SA_PASSWORD" \
    -C \
    -b \
    -d Walkamon \
    -W \
    -w 65535 \
    -s "|" \
    -Q "
      SET NOCOUNT ON;
      SELECT
        s.name + NCHAR(46) + t.name AS TableName,
        SUM(p.rows) AS RowsInTable
      FROM sys.tables AS t
      INNER JOIN sys.schemas AS s
        ON s.schema_id = t.schema_id
      INNER JOIN sys.partitions AS p
        ON p.object_id = t.object_id
       AND p.index_id IN (0, 1)
      WHERE t.is_ms_shipped = 0
      GROUP BY s.name, t.name
      ORDER BY SUM(p.rows) DESC, s.name, t.name;
    "
'
