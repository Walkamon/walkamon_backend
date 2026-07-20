#!/usr/bin/env bash
set -Eeuo pipefail

if [[ $EUID -ne 0 ]]; then
  echo "Run this script as root." >&2
  exit 1
fi

ENV_FILE=/etc/walkamon/walkamon.env
SOURCE_ROOT=/home/walkamon/walkamon-source/deploy

for credential in \
  /etc/walkamon/secrets/firebase-service-account.json \
  /etc/walkamon/secrets/play-integrity-service-account.json
do
  jq empty "$credential"
  mode=$(stat -c '%u:%g:%a' "$credential")
  if [[ "$mode" != "0:1654:640" && "$mode" != "0:1654:440" ]]; then
    echo "Unexpected credential ownership or mode for $credential: $mode" >&2
    exit 1
  fi
done

install -m 0644 "$SOURCE_ROOT/compose.prod.yml" /opt/walkamon/compose.prod.yml
install -m 0755 -d /opt/walkamon/caddy /srv/walkamon/deploy /srv/walkamon/gateway
install -m 0644 "$SOURCE_ROOT/caddy/Caddyfile.template" \
  /opt/walkamon/caddy/Caddyfile.template
install -m 0750 "$SOURCE_ROOT"/scripts/*.sh /opt/walkamon/scripts/
install -m 0644 "$SOURCE_ROOT"/systemd/*.service /etc/systemd/system/
install -m 0644 "$SOURCE_ROOT"/systemd/*.timer /etc/systemd/system/
systemctl daemon-reload

docker compose \
  --env-file "$ENV_FILE" \
  -f /opt/walkamon/compose.prod.yml \
  config --quiet
docker compose \
  --env-file "$ENV_FILE" \
  -f /opt/walkamon/compose.prod.yml \
  up -d --force-recreate db

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
  echo "SQL Server did not become healthy after configuration update." >&2
  exit 1
fi

/opt/walkamon/scripts/verify-migration-baseline.sh
rm -f \
  /home/walkamon/firebase-service-account.json \
  /home/walkamon/play-integrity-service-account.json \
  /home/walkamon/compose.prod.yml.new

echo "PRODUCTION_APP_CONFIG_APPLIED"
echo "Remaining placeholders:"
grep -E '^[A-Z0-9_]+=.*REPLACE' "$ENV_FILE" | cut -d= -f1 || true
