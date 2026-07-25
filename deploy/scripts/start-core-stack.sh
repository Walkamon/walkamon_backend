#!/usr/bin/env bash
set -Eeuo pipefail

if [[ $EUID -ne 0 ]]; then
  echo "Run this script as root." >&2
  exit 1
fi

ENV_FILE=/etc/walkamon/walkamon.env
COMPOSE_FILE=/opt/walkamon/compose.prod.yml
ACTIVE_SLOT_FILE=/srv/walkamon/deploy/active-slot
script_dir=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)

"$script_dir/preflight.sh" --core
if [[ ! -r "$ACTIVE_SLOT_FILE" ]]; then
  echo "Blue-green deployment is not initialized. Run migrate-blue-green.sh once." >&2
  exit 1
fi

active_slot=$(tr -d '[:space:]' < "$ACTIVE_SLOT_FILE")
case "$active_slot" in
  blue|green) ;;
  *)
    echo "Invalid active slot: $active_slot" >&2
    exit 1
    ;;
esac

compose=(docker compose --env-file "$ENV_FILE" -f "$COMPOSE_FILE")
active_service="api-$active_slot"

"${compose[@]}" up -d db "$active_service"
for _ in {1..60}; do
  status=$(docker inspect \
    --format '{{if .State.Health}}{{.State.Health.Status}}{{else}}{{.State.Status}}{{end}}' \
    "walkamon-api-$active_slot" 2>/dev/null || true)
  [[ "$status" == healthy ]] && break
  [[ "$status" == unhealthy || "$status" == exited || "$status" == dead ]] && break
  sleep 2
done
if [[ "${status:-}" != healthy ]]; then
  "${compose[@]}" logs --tail=150 "$active_service" >&2 || true
  echo "Active API slot did not become healthy." >&2
  exit 1
fi

"${compose[@]}" up -d api worker cloudflared dozzle dbgate
for _ in {1..60}; do
  if curl --fail --silent --max-time 3 \
    http://127.0.0.1:8080/health/ready >/dev/null; then
    echo "WALKAMON_STACK_HEALTHY active=$active_slot"
    exit 0
  fi
  sleep 2
done

"${compose[@]}" logs --tail=150 api "$active_service" worker >&2 || true
echo "The Walkamon gateway did not become ready." >&2
exit 1
