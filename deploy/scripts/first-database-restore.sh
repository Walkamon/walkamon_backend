#!/usr/bin/env bash
set -Eeuo pipefail

if [[ $EUID -ne 0 || $# -ne 1 || ! -f "$1" ]]; then
  echo "Usage: sudo $0 /path/to/Walkamon_migration.bak" >&2
  exit 2
fi

ENV_FILE=/etc/walkamon/walkamon.env
COMPOSE_FILE=/opt/walkamon/compose.prod.yml
script_dir=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)

"$script_dir/initialize-production-secrets.sh"

compose=(
  docker compose
  --env-file "$ENV_FILE"
  -f "$COMPOSE_FILE"
)

"${compose[@]}" up -d db

database_healthy=false
for _ in {1..60}; do
  status=$(docker inspect --format '{{if .State.Health}}{{.State.Health.Status}}{{else}}none{{end}}' walkamon-db 2>/dev/null || true)
  if [[ "$status" == healthy ]]; then
    database_healthy=true
    break
  fi
  if [[ "$status" == unhealthy ]]; then
    break
  fi
  sleep 5
done

if [[ "$database_healthy" != true ]]; then
  echo "SQL Server did not become healthy." >&2
  "${compose[@]}" logs --tail=150 db >&2 || true
  exit 1
fi

"$script_dir/restore-database.sh" "$1"
"$script_dir/verify-migration-baseline.sh"
echo "The application login walkamon_app was provisioned separately from sa."
