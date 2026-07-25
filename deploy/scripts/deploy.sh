#!/usr/bin/env bash
set -Eeuo pipefail

exec 9>/run/lock/walkamon-deploy.lock
flock -n 9 || exit 0

ENV_FILE=/etc/walkamon/walkamon.env
COMPOSE_FILE=/opt/walkamon/compose.prod.yml
ACTIVE_SLOT_FILE=/srv/walkamon/deploy/active-slot
CADDY_TEMPLATE=/opt/walkamon/caddy/Caddyfile.template
CADDY_FILE=/srv/walkamon/gateway/Caddyfile
CADDY_IMAGE=caddy:2.11.4-alpine
force_slot_swap=false
drain_seconds=300

usage() {
  echo "Usage: $0 [--force-slot-swap] [--drain-seconds 0..300]" >&2
}

while (( $# > 0 )); do
  case "$1" in
    --force-slot-swap)
      force_slot_swap=true
      shift
      ;;
    --drain-seconds)
      [[ $# -ge 2 && "$2" =~ ^[0-9]+$ && "$2" -le 300 ]] || {
        usage
        exit 2
      }
      drain_seconds=$2
      shift 2
      ;;
    *)
      usage
      exit 2
      ;;
  esac
done

if [[ ! -r "$ENV_FILE" ]]; then
  echo "Missing $ENV_FILE" >&2
  exit 1
fi
if [[ ! -r "$ACTIVE_SLOT_FILE" ]]; then
  echo "Blue-green deployment is not initialized. Run migrate-blue-green.sh once." >&2
  exit 1
fi

/opt/walkamon/scripts/preflight.sh --core

set -a
source "$ENV_FILE"
set +a

API_IMAGE=${API_IMAGE:-ghcr.io/walkamon/walkamon_backend:main}
compose=(docker compose --env-file "$ENV_FILE" -f "$COMPOSE_FILE")

active_slot=$(tr -d '[:space:]' < "$ACTIVE_SLOT_FILE")
case "$active_slot" in
  blue) inactive_slot=green ;;
  green) inactive_slot=blue ;;
  *)
    echo "Invalid active slot: $active_slot" >&2
    exit 1
    ;;
esac

active_service="api-$active_slot"
inactive_service="api-$inactive_slot"
active_container="walkamon-api-$active_slot"
inactive_container="walkamon-api-$inactive_slot"

wait_for_container_health() {
  local container=$1
  local attempts=${2:-60}
  local status

  for ((i = 0; i < attempts; i++)); do
    status=$(docker inspect \
      --format '{{if .State.Health}}{{.State.Health.Status}}{{else}}{{.State.Status}}{{end}}' \
      "$container" 2>/dev/null || true)
    if [[ "$status" == healthy ]]; then
      return 0
    fi
    if [[ "$status" == unhealthy || "$status" == exited || "$status" == dead ]]; then
      return 1
    fi
    sleep 2
  done
  return 1
}

render_caddy_config() {
  local slot=$1
  local destination=$2
  sed "s/__UPSTREAM__/api-${slot}/g" "$CADDY_TEMPLATE" > "$destination"
  chmod 0644 "$destination"
}

validate_caddy_config() {
  local candidate=$1
  docker run --rm --network none \
    --volume "$candidate:/etc/caddy/Caddyfile:ro" \
    "$CADDY_IMAGE" \
    caddy validate --config /etc/caddy/Caddyfile --adapter caddyfile >/dev/null
}

reload_gateway() {
  docker exec walkamon-api-gateway \
    caddy reload --config /etc/caddy/Caddyfile --adapter caddyfile >/dev/null
}

# Caddy bind-mounts this single file. Write through the existing inode so the
# running container sees the new contents; replacing the file would leave the
# bind mount attached to the old inode.
write_gateway_config() {
  local source=$1
  cat "$source" > "$CADDY_FILE"
  chmod 0644 "$CADDY_FILE"
}

wait_for_gateway() {
  for _ in {1..40}; do
    if curl --fail --silent --max-time 3 \
      http://127.0.0.1:8080/health/ready >/dev/null; then
      return 0
    fi
    sleep 0.25
  done
  return 1
}

if [[ -n "${GHCR_USERNAME:-}" && -n "${GHCR_TOKEN:-}" ]]; then
  printf '%s' "$GHCR_TOKEN" \
    | docker login ghcr.io --username "$GHCR_USERNAME" --password-stdin >/dev/null
fi

"${compose[@]}" pull "$inactive_service" worker api
new_image_id=$(docker image inspect "$API_IMAGE" --format '{{.Id}}')
active_image_id=$(docker inspect --format '{{.Image}}' "$active_container" 2>/dev/null || true)

if [[ "$force_slot_swap" != true && "$active_image_id" == "$new_image_id" ]]; then
  "${compose[@]}" up -d db "$active_service" api worker cloudflared dozzle dbgate
  echo "No API update: $active_slot already runs $new_image_id"
  exit 0
fi

"${compose[@]}" up -d db
"${compose[@]}" up -d --no-deps --force-recreate "$inactive_service"
if ! wait_for_container_health "$inactive_container"; then
  echo "Inactive slot $inactive_slot did not become healthy." >&2
  "${compose[@]}" logs --tail=150 "$inactive_service" >&2 || true
  "${compose[@]}" stop --timeout 20 "$inactive_service" >/dev/null || true
  exit 1
fi

candidate=$(mktemp /srv/walkamon/gateway/Caddyfile.XXXXXX)
previous=$(mktemp /srv/walkamon/gateway/Caddyfile.previous.XXXXXX)
cleanup() {
  rm -f "$candidate" "$previous"
}
trap cleanup EXIT

render_caddy_config "$inactive_slot" "$candidate"
validate_caddy_config "$candidate"
cp -p "$CADDY_FILE" "$previous"
write_gateway_config "$candidate"

if ! reload_gateway || ! wait_for_gateway; then
  echo "Gateway switch failed; restoring slot $active_slot." >&2
  write_gateway_config "$previous"
  reload_gateway || true
  "${compose[@]}" stop --timeout 20 "$inactive_service" >/dev/null || true
  exit 1
fi

if ! curl --fail --silent --max-time 10 \
  https://api.walkamon.xyz/health/ready >/dev/null; then
  echo "Public health failed; restoring slot $active_slot." >&2
  write_gateway_config "$previous"
  reload_gateway || true
  wait_for_gateway || true
  "${compose[@]}" stop --timeout 20 "$inactive_service" >/dev/null || true
  exit 1
fi

printf '%s\n' "$inactive_slot" > "${ACTIVE_SLOT_FILE}.new"
chmod 0644 "${ACTIVE_SLOT_FILE}.new"
mv "${ACTIVE_SLOT_FILE}.new" "$ACTIVE_SLOT_FILE"

old_worker_image_id=$(docker inspect --format '{{.Image}}' walkamon-worker 2>/dev/null || true)
"${compose[@]}" up -d --no-deps --force-recreate worker
if ! wait_for_container_health walkamon-worker; then
  echo "The new worker is unhealthy; rolling the worker back." >&2
  "${compose[@]}" logs --tail=150 worker >&2 || true
  if [[ -n "$old_worker_image_id" ]]; then
    rollback_worker_image="walkamon-worker-rollback:${old_worker_image_id#sha256:}"
    docker tag "$old_worker_image_id" "$rollback_worker_image"
    API_IMAGE="$rollback_worker_image" "${compose[@]}" \
      up -d --no-deps --force-recreate worker
  fi
  exit 1
fi

echo "Gateway now serves $inactive_slot ($new_image_id). Draining $active_slot for ${drain_seconds}s."
sleep "$drain_seconds"
"${compose[@]}" stop --timeout 30 "$active_service" >/dev/null || true

"${compose[@]}" up -d cloudflared dozzle dbgate
docker image prune --force --filter "until=168h" >/dev/null
echo "Walkamon deployment completed on slot $inactive_slot with image $new_image_id"
