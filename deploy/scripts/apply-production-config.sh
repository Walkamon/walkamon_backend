#!/usr/bin/env bash
set -Eeuo pipefail

if [[ $EUID -ne 0 ]]; then
  echo "Run this script as root." >&2
  exit 1
fi

ENV_FILE=/etc/walkamon/walkamon.env
SOURCE_ROOT=/home/walkamon/walkamon-source/deploy

read -r smtp_username
read -r smtp_app_password
read -r cloudinary_cloud_name
read -r cloudinary_api_key
read -r cloudinary_api_secret
read -r google_auth_client_id
read -r firebase_project_id
read -r android_package_name
read -r android_certificate_sha256

config_names=(
  smtp_username
  smtp_app_password
  cloudinary_cloud_name
  cloudinary_api_key
  cloudinary_api_secret
  google_auth_client_id
  firebase_project_id
  android_package_name
  android_certificate_sha256
)
for config_name in "${config_names[@]}"; do
  printf -v "$config_name" '%s' "${!config_name%$'\r'}"
done

values=(
  "$smtp_username"
  "$smtp_app_password"
  "$cloudinary_cloud_name"
  "$cloudinary_api_key"
  "$cloudinary_api_secret"
  "$google_auth_client_id"
  "$firebase_project_id"
  "$android_package_name"
  "$android_certificate_sha256"
)
for value in "${values[@]}"; do
  if [[ -z "$value" || "$value" == *$'\n'* || "$value" == *$'\r'* ]]; then
    echo "A required production value is empty or contains a newline." >&2
    exit 1
  fi
done
if [[ ! "$android_certificate_sha256" =~ ^[[:xdigit:]]{64}$ ]]; then
  echo "Android certificate SHA-256 must contain exactly 64 hexadecimal characters." >&2
  exit 1
fi

escape_sed_replacement() {
  sed 's/[&|\\]/\\&/g' <<<"$1"
}

replace_env_value() {
  local key=$1
  local value
  value=$(escape_sed_replacement "$2")
  if ! grep -qE "^${key}=" "$ENV_FILE"; then
    echo "Missing environment key: $key" >&2
    exit 1
  fi
  sed -i "s|^${key}=.*$|${key}=${value}|" "$ENV_FILE"
}

cp -p "$ENV_FILE" "${ENV_FILE}.before-app-config"

replace_env_value SMTP_USERNAME "$smtp_username"
replace_env_value SMTP_APP_PASSWORD "$smtp_app_password"
replace_env_value CLOUDINARY_CLOUD_NAME "$cloudinary_cloud_name"
replace_env_value CLOUDINARY_API_KEY "$cloudinary_api_key"
replace_env_value CLOUDINARY_API_SECRET "$cloudinary_api_secret"
replace_env_value GOOGLE_AUTH_CLIENT_ID "$google_auth_client_id"
replace_env_value FIREBASE_PROJECT_ID "$firebase_project_id"
replace_env_value ANDROID_PACKAGE_NAME "$android_package_name"
replace_env_value ANDROID_CERTIFICATE_SHA256 "$android_certificate_sha256"

chown root:root "$ENV_FILE" "${ENV_FILE}.before-app-config"
chmod 0600 "$ENV_FILE" "${ENV_FILE}.before-app-config"

install -o root -g 1654 -m 0640 \
  /home/walkamon/firebase-service-account.json \
  /etc/walkamon/secrets/firebase-service-account.json
install -o root -g 1654 -m 0640 \
  /home/walkamon/play-integrity-service-account.json \
  /etc/walkamon/secrets/play-integrity-service-account.json

jq empty /etc/walkamon/secrets/firebase-service-account.json
jq empty /etc/walkamon/secrets/play-integrity-service-account.json

install -m 0644 "$SOURCE_ROOT/compose.prod.yml" /opt/walkamon/compose.prod.yml
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
