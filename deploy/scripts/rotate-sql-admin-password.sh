#!/usr/bin/env bash
set -Eeuo pipefail

if [[ $EUID -ne 0 ]]; then
  echo "Run this script as root." >&2
  exit 1
fi

ENV_FILE=/etc/walkamon/walkamon.env
COMPOSE_FILE=/opt/walkamon/compose.prod.yml
temporary_env=$(mktemp /etc/walkamon/walkamon.env.rotate.XXXXXX)
api_stopped=false
compose=(docker compose --env-file "$ENV_FILE" -f "$COMPOSE_FILE")

cleanup() {
  rm -f "$temporary_env"
  if [[ "$api_stopped" == true ]]; then
    "${compose[@]}" up -d api >/dev/null 2>&1 || true
  fi
}
trap cleanup EXIT

set -a
source "$ENV_FILE"
set +a

new_password=$(
  openssl rand -base64 48 \
    | tr -d '\n=' \
    | tr '+/' '-_'
)

sed -E \
  "s|^MSSQL_SA_PASSWORD=.*$|MSSQL_SA_PASSWORD=${new_password}|" \
  "$ENV_FILE" >"$temporary_env"
if ! grep -qxF "MSSQL_SA_PASSWORD=${new_password}" "$temporary_env"; then
  echo "Could not prepare the updated production environment." >&2
  exit 1
fi

"${compose[@]}" stop --timeout 45 api
api_stopped=true

docker exec walkamon-db /bin/bash -lc \
  'exec /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C -b "$@"' \
  walkamon-sqlcmd \
  -v "NEW_PASSWORD=${new_password}" \
  -Q "ALTER LOGIN [sa] WITH PASSWORD=N'\$(NEW_PASSWORD)', CHECK_POLICY=ON;"

install -o root -g root -m 0600 "$temporary_env" "$ENV_FILE"
MSSQL_SA_PASSWORD=$new_password
export MSSQL_SA_PASSWORD

"${compose[@]}" up -d --no-deps --force-recreate db

for _ in {1..60}; do
  if [[ "$(docker inspect --format '{{if .State.Health}}{{.State.Health.Status}}{{end}}' walkamon-db 2>/dev/null)" == "healthy" ]]; then
    "${compose[@]}" up -d api
    for _ in {1..36}; do
      if [[ "$(docker inspect --format '{{if .State.Health}}{{.State.Health.Status}}{{end}}' walkamon-api 2>/dev/null)" == "healthy" ]]; then
        api_stopped=false
        echo "SQL_ADMIN_PASSWORD_ROTATED"
        exit 0
      fi
      sleep 5
    done
    echo "The API did not become healthy after rotating the SQL admin password." >&2
    exit 1
  fi
  sleep 5
done

echo "SQL Server did not become healthy after rotating the admin password." >&2
exit 1
