#!/usr/bin/env bash
set -Eeuo pipefail

ENV_FILE=/etc/walkamon/walkamon.env
set -a
source "$ENV_FILE"
set +a

if [[ "$WALKAMON_DB_PASSWORD" == *"'"* ]]; then
  echo "WALKAMON_DB_PASSWORD must not contain a single quote." >&2
  exit 1
fi

docker exec walkamon-db /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C -b \
  -v APP_PASSWORD="$WALKAMON_DB_PASSWORD" \
  -Q "IF SUSER_ID(N'walkamon_app') IS NULL CREATE LOGIN [walkamon_app] WITH PASSWORD=N'\$(APP_PASSWORD)', CHECK_POLICY=ON; ELSE ALTER LOGIN [walkamon_app] WITH PASSWORD=N'\$(APP_PASSWORD)'; USE [Walkamon]; IF USER_ID(N'walkamon_app') IS NULL CREATE USER [walkamon_app] FOR LOGIN [walkamon_app]; ALTER ROLE [db_datareader] ADD MEMBER [walkamon_app]; ALTER ROLE [db_datawriter] ADD MEMBER [walkamon_app]; GRANT EXECUTE TO [walkamon_app];"
