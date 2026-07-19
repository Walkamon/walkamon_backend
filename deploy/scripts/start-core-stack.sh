#!/usr/bin/env bash
set -Eeuo pipefail

if [[ $EUID -ne 0 ]]; then
  echo "Run this script as root." >&2
  exit 1
fi

ENV_FILE=/etc/walkamon/walkamon.env
COMPOSE_FILE=/opt/walkamon/compose.prod.yml
script_dir=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)

"$script_dir/preflight.sh" --core

docker compose \
  --env-file "$ENV_FILE" \
  -f "$COMPOSE_FILE" \
  up -d db api

for _ in {1..36}; do
  if curl --fail --silent http://127.0.0.1:8080/health/ready >/dev/null; then
    echo "WALKAMON_CORE_STACK_HEALTHY"
    exit 0
  fi
  sleep 5
done

docker compose \
  --env-file "$ENV_FILE" \
  -f "$COMPOSE_FILE" \
  logs --tail=150 api >&2 || true
echo "The Walkamon API did not become ready." >&2
exit 1
