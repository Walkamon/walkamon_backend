#!/usr/bin/env bash
set -Eeuo pipefail

if [[ $EUID -ne 0 ]]; then
  echo "Run this script with sudo." >&2
  exit 1
fi

ENV_FILE=/etc/walkamon/walkamon.env
if [[ ! -f "$ENV_FILE" ]]; then
  echo "Missing $ENV_FILE. Run bootstrap-ubuntu.sh first." >&2
  exit 1
fi

umask 077

random_urlsafe() {
  local bytes=$1
  openssl rand -base64 "$bytes" \
    | tr -d '\n=' \
    | tr '+/' '-_'
}

replace_if_placeholder() {
  local key=$1
  local value=$2
  local current
  current=$(grep -E "^${key}=" "$ENV_FILE" | head -n 1 || true)

  if [[ -z "$current" ]]; then
    echo "Missing key in $ENV_FILE: $key" >&2
    exit 1
  fi

  if [[ "$current" != *REPLACE* ]]; then
    echo "Kept existing $key."
    return
  fi

  sed -i "s|^${key}=.*$|${key}=${value}|" "$ENV_FILE"
  echo "Generated $key."
}

replace_if_placeholder MSSQL_SA_PASSWORD "$(random_urlsafe 48)"
replace_if_placeholder WALKAMON_DB_PASSWORD "$(random_urlsafe 48)"
replace_if_placeholder JWT_KEY "$(random_urlsafe 96)"
replace_if_placeholder RESTIC_PASSWORD "$(random_urlsafe 48)"

chown root:root "$ENV_FILE"
chmod 0600 "$ENV_FILE"

echo "Internal production secrets are initialized without printing their values."
echo "Store the RESTIC_PASSWORD recovery value in a password manager before deployment."
