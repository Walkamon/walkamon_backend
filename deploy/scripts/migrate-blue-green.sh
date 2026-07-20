#!/usr/bin/env bash
set -Eeuo pipefail

if [[ $EUID -ne 0 ]]; then
  echo "Run this script as root." >&2
  exit 1
fi

exec 9>/run/lock/walkamon-deploy.lock
flock -n 9 || {
  echo "Another Walkamon deployment is running." >&2
  exit 1
}

ENV_FILE=/etc/walkamon/walkamon.env
COMPOSE_FILE=/opt/walkamon/compose.prod.yml
LEGACY_COMPOSE_FILE=/opt/walkamon/compose.pre-blue-green.yml
ACTIVE_SLOT_FILE=/srv/walkamon/deploy/active-slot
CADDY_TEMPLATE=/opt/walkamon/caddy/Caddyfile.template
CADDY_FILE=/srv/walkamon/gateway/Caddyfile
CADDY_IMAGE=caddy:2.11.4-alpine

if [[ -e "$ACTIVE_SLOT_FILE" ]]; then
  echo "Blue-green deployment is already initialized." >&2
  exit 1
fi
if docker inspect walkamon-api >/dev/null 2>&1 \
   && [[ ! -r "$LEGACY_COMPOSE_FILE" ]]; then
  echo "Save the previous Compose file as $LEGACY_COMPOSE_FILE before migration." >&2
  exit 1
fi

/opt/walkamon/scripts/preflight.sh --core
set -a
source "$ENV_FILE"
set +a

compose=(docker compose --env-file "$ENV_FILE" -f "$COMPOSE_FILE")
old_compose=(docker compose --env-file "$ENV_FILE" -f "$LEGACY_COMPOSE_FILE")
legacy_present=false
if docker inspect walkamon-api >/dev/null 2>&1; then
  legacy_present=true
fi

wait_for_health() {
  local container=$1
  for _ in {1..60}; do
    status=$(docker inspect \
      --format '{{if .State.Health}}{{.State.Health.Status}}{{else}}{{.State.Status}}{{end}}' \
      "$container" 2>/dev/null || true)
    [[ "$status" == healthy ]] && return 0
    [[ "$status" == unhealthy || "$status" == exited || "$status" == dead ]] && return 1
    sleep 2
  done
  return 1
}

rollback_legacy() {
  echo "Blue-green migration failed; restoring the legacy API." >&2
  "${compose[@]}" stop --timeout 20 api worker api-blue >/dev/null 2>&1 || true
  docker rm --force walkamon-api-gateway >/dev/null 2>&1 || true
  if [[ "$legacy_present" == true ]]; then
    "${old_compose[@]}" up -d db api cloudflared || true
  fi
}

install -m 0755 -d /srv/walkamon/deploy /srv/walkamon/gateway
candidate=$(mktemp /srv/walkamon/gateway/Caddyfile.XXXXXX)
trap 'rm -f "$candidate"' EXIT
sed 's/__UPSTREAM__/api-blue/g' "$CADDY_TEMPLATE" > "$candidate"
chmod 0644 "$candidate"

if [[ -n "${GHCR_USERNAME:-}" && -n "${GHCR_TOKEN:-}" ]]; then
  printf '%s' "$GHCR_TOKEN" \
    | docker login ghcr.io --username "$GHCR_USERNAME" --password-stdin >/dev/null
fi

"${compose[@]}" pull api-blue worker api
"${compose[@]}" up -d db
if ! wait_for_health walkamon-db; then
  echo "SQL Server is not healthy." >&2
  exit 1
fi

"${compose[@]}" up -d --no-deps --force-recreate api-blue
if ! wait_for_health walkamon-api-blue; then
  "${compose[@]}" logs --tail=150 api-blue >&2 || true
  exit 1
fi

docker run --rm --network none \
  --volume "$candidate:/etc/caddy/Caddyfile:ro" \
  "$CADDY_IMAGE" \
  caddy validate --config /etc/caddy/Caddyfile --adapter caddyfile >/dev/null
install -m 0644 "$candidate" "$CADDY_FILE"

if ! "${compose[@]}" up -d --no-deps --force-recreate api; then
  rollback_legacy
  exit 1
fi
if ! wait_for_health walkamon-api-gateway; then
  "${compose[@]}" logs --tail=150 api >&2 || true
  rollback_legacy
  exit 1
fi

"${compose[@]}" up -d --no-deps --force-recreate worker
if ! wait_for_health walkamon-worker; then
  "${compose[@]}" logs --tail=150 worker >&2 || true
  rollback_legacy
  exit 1
fi

"${compose[@]}" up -d --no-deps --force-recreate cloudflared
if ! curl --retry 12 --retry-delay 2 --retry-all-errors \
  --fail --silent --max-time 10 \
  https://api.walkamon.xyz/health/ready >/dev/null; then
  rollback_legacy
  exit 1
fi

printf 'blue\n' > "$ACTIVE_SLOT_FILE"
chmod 0644 "$ACTIVE_SLOT_FILE"

# Older Walkamon installations created DbGate outside Compose. Remove only
# that verified unmanaged container; its named data volume remains intact.
if docker inspect walkamon-dbgate >/dev/null 2>&1; then
  dbgate_service_label=$(docker inspect \
    --format '{{index .Config.Labels "com.docker.compose.service"}}' \
    walkamon-dbgate 2>/dev/null || true)
  if [[ -z "$dbgate_service_label" ]]; then
    docker rm --force walkamon-dbgate >/dev/null
  fi
fi

"${compose[@]}" up -d dozzle dbgate
echo "BLUE_GREEN_MIGRATION_COMPLETE active=blue"
