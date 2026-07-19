#!/usr/bin/env bash
set -Eeuo pipefail

if [[ $# -ne 1 || ! -f "$1" ]]; then
  echo "Usage: sudo $0 /path/to/approved-schema-change.sql" >&2
  exit 2
fi

script_dir=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
source_sql=$(realpath "$1")
container_sql="/tmp/walkamon-schema-$$.sql"
ENV_FILE=/etc/walkamon/walkamon.env

if [[ ! -r "$ENV_FILE" ]]; then
  echo "Missing or unreadable production environment: $ENV_FILE" >&2
  exit 1
fi

set -a
source "$ENV_FILE"
set +a

"${script_dir}/backup.sh"

docker cp "$source_sql" "walkamon-db:${container_sql}"
cleanup() {
  docker exec --user root walkamon-db rm -f "$container_sql" >/dev/null 2>&1 || true
}
trap cleanup EXIT

docker exec walkamon-db /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C -b \
  -d Walkamon -i "$container_sql"

echo "Schema script completed after a verified encrypted backup: $source_sql"
