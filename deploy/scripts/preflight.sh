#!/usr/bin/env bash
set -Eeuo pipefail

ENV_FILE=/etc/walkamon/walkamon.env
COMPOSE_FILE=/opt/walkamon/compose.prod.yml
mode=${1:-full}

if [[ $EUID -ne 0 ]]; then
  echo "Run preflight as root." >&2
  exit 1
fi
if [[ "$mode" != full && "$mode" != --core ]]; then
  echo "Usage: $0 [--core]" >&2
  exit 2
fi

if [[ ! -f "$ENV_FILE" || ! -f "$COMPOSE_FILE" ]]; then
  echo "Missing production environment or Compose file." >&2
  exit 1
fi

env_mode=$(stat -c '%u:%g:%a' "$ENV_FILE")
if [[ "$env_mode" != "0:0:600" ]]; then
  echo "$ENV_FILE must be root:root mode 600; found $env_mode." >&2
  exit 1
fi

set -a
source "$ENV_FILE"
set +a

required=(
  API_IMAGE
  MSSQL_SA_PASSWORD
  WALKAMON_DB_PASSWORD
  JWT_KEY
  SMTP_USERNAME
  SMTP_APP_PASSWORD
  CLOUDINARY_CLOUD_NAME
  CLOUDINARY_API_KEY
  CLOUDINARY_API_SECRET
  GOOGLE_AUTH_CLIENT_ID
  FIREBASE_PROJECT_ID
  ANDROID_PACKAGE_NAME
  ANDROID_CERTIFICATE_SHA256
)
if [[ "$mode" == full ]]; then
  required+=(
    CLOUDFLARE_TUNNEL_TOKEN
    RESTIC_REPOSITORY
    RESTIC_PASSWORD
    AWS_ACCESS_KEY_ID
    AWS_SECRET_ACCESS_KEY
  )
fi

for key in "${required[@]}"; do
  value=${!key:-}
  if [[ -z "$value" || "$value" == *REPLACE* ]]; then
    echo "Production value is missing or still a placeholder: $key" >&2
    exit 1
  fi
done

if [[ -n "${GHCR_USERNAME:-}" || -n "${GHCR_TOKEN:-}" ]]; then
  if [[ -z "${GHCR_USERNAME:-}" || -z "${GHCR_TOKEN:-}" \
        || "$GHCR_USERNAME" == *REPLACE* || "$GHCR_TOKEN" == *REPLACE* ]]; then
    echo "Set both GHCR_USERNAME and GHCR_TOKEN, or leave both empty for a public package." >&2
    exit 1
  fi
fi

if (( ${#MSSQL_SA_PASSWORD} < 32 || ${#WALKAMON_DB_PASSWORD} < 32 )); then
  echo "Both SQL passwords must contain at least 32 characters." >&2
  exit 1
fi

if [[ "$MSSQL_SA_PASSWORD" == "$WALKAMON_DB_PASSWORD" ]]; then
  echo "The SQL sa and application passwords must be different." >&2
  exit 1
fi

if (( ${#JWT_KEY} < 64 )); then
  echo "JWT_KEY must contain at least 64 characters." >&2
  exit 1
fi

certificate_sha=${ANDROID_CERTIFICATE_SHA256//:/}
if [[ ! "$certificate_sha" =~ ^[[:xdigit:]]{64}$ ]]; then
  echo "ANDROID_CERTIFICATE_SHA256 must be a 64-character SHA-256 hex value." >&2
  exit 1
fi

for secret_file in \
  /etc/walkamon/secrets/play-integrity-service-account.json \
  /etc/walkamon/secrets/firebase-service-account.json
do
  if [[ ! -f "$secret_file" ]]; then
    echo "Missing credential file: $secret_file" >&2
    exit 1
  fi

  secret_mode=$(stat -c '%u:%g:%a' "$secret_file")
  if [[ "$secret_mode" != "0:1654:640" && "$secret_mode" != "0:1654:440" ]]; then
    echo "$secret_file must be root:1654 mode 640 or 440; found $secret_mode." >&2
    exit 1
  fi

  jq empty "$secret_file"
done

available_kb=$(df --output=avail /srv/walkamon | awk 'NR==2 {print $1}')
if (( available_kb < 15 * 1024 * 1024 )); then
  echo "Less than 15 GB is free on the filesystem containing /srv/walkamon." >&2
  exit 1
fi

docker compose --env-file "$ENV_FILE" -f "$COMPOSE_FILE" config --quiet
echo "Walkamon production preflight passed ($mode)."
