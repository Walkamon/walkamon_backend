#!/usr/bin/env bash
set -Eeuo pipefail

exec 9>/run/lock/walkamon-deploy.lock
flock -n 9 || exit 0

ENV_FILE=/etc/walkamon/walkamon.env
COMPOSE_FILE=/opt/walkamon/compose.prod.yml

if [[ ! -r "$ENV_FILE" ]]; then
  echo "Missing $ENV_FILE" >&2
  exit 1
fi

/opt/walkamon/scripts/preflight.sh --core

set -a
source "$ENV_FILE"
set +a

API_IMAGE=${API_IMAGE:-ghcr.io/walkamon/walkamon_backend:main}
compose=(docker compose --env-file "$ENV_FILE" -f "$COMPOSE_FILE")

if [[ -n "${GHCR_USERNAME:-}" && -n "${GHCR_TOKEN:-}" ]]; then
  printf '%s' "$GHCR_TOKEN" | docker login ghcr.io --username "$GHCR_USERNAME" --password-stdin >/dev/null
fi

old_image_id=$(docker image inspect "$API_IMAGE" --format '{{.Id}}' 2>/dev/null || true)
"${compose[@]}" pull api
new_image_id=$(docker image inspect "$API_IMAGE" --format '{{.Id}}')

"${compose[@]}" up -d db
"${compose[@]}" up -d --no-deps api

healthy=false
for _ in {1..24}; do
  if curl --fail --silent http://127.0.0.1:8080/health/ready >/dev/null; then
    healthy=true
    break
  fi
  sleep 5
done

if [[ "$healthy" != true ]]; then
  echo "The new API image did not become ready." >&2
  "${compose[@]}" logs --tail=100 api >&2 || true

  if [[ -n "$old_image_id" && "$old_image_id" != "$new_image_id" ]]; then
    docker tag "$old_image_id" "$API_IMAGE"
    "${compose[@]}" up -d --no-deps --force-recreate api
    echo "Rolled back API to $old_image_id" >&2
  fi
  exit 1
fi

"${compose[@]}" up -d cloudflared
docker image prune --force --filter "until=168h" >/dev/null
echo "Walkamon is healthy on image $new_image_id"
