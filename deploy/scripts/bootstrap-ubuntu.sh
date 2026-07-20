#!/usr/bin/env bash
set -Eeuo pipefail

if [[ $EUID -ne 0 ]]; then
  echo "Run this script with sudo." >&2
  exit 1
fi

repo_root=$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)
operator=${SUDO_USER:-$(logname 2>/dev/null || echo root)}
host_only_source=${WALKAMON_HOST_ONLY_SOURCE:-192.168.120.1}

timedatectl set-timezone Asia/Ho_Chi_Minh
apt-get update
apt-get install --yes ca-certificates curl gnupg jq openssl restic ufw unattended-upgrades

install -m 0755 -d /etc/apt/keyrings
curl -fsSL https://download.docker.com/linux/ubuntu/gpg \
  | gpg --dearmor --yes -o /etc/apt/keyrings/docker.gpg
chmod a+r /etc/apt/keyrings/docker.gpg

. /etc/os-release
arch=$(dpkg --print-architecture)
echo "deb [arch=${arch} signed-by=/etc/apt/keyrings/docker.gpg] https://download.docker.com/linux/ubuntu ${VERSION_CODENAME} stable" \
  > /etc/apt/sources.list.d/docker.list
apt-get update
apt-get install --yes docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin

install -m 0750 -d \
  /etc/walkamon \
  /etc/walkamon/secrets \
  /opt/walkamon/scripts \
  /opt/walkamon/caddy
install -m 0750 -o 10001 -g 0 -d \
  /srv/walkamon/mssql/data \
  /srv/walkamon/mssql/log \
  /srv/walkamon/mssql/backup
install -m 0750 -d /srv/walkamon/dozzle
install -m 0755 -d /srv/walkamon/deploy /srv/walkamon/gateway

install -m 0644 "$repo_root/deploy/compose.prod.yml" /opt/walkamon/compose.prod.yml
install -m 0644 "$repo_root/deploy/caddy/Caddyfile.template" \
  /opt/walkamon/caddy/Caddyfile.template
if [[ ! -e /srv/walkamon/gateway/Caddyfile ]]; then
  sed 's/__UPSTREAM__/api-blue/g' \
    "$repo_root/deploy/caddy/Caddyfile.template" \
    > /srv/walkamon/gateway/Caddyfile
  chmod 0644 /srv/walkamon/gateway/Caddyfile
fi
install -m 0750 "$repo_root"/deploy/scripts/*.sh /opt/walkamon/scripts/
install -m 0600 "$repo_root/deploy/walkamon.env.example" /etc/walkamon/walkamon.env.example

if [[ ! -e /etc/walkamon/walkamon.env ]]; then
  install -m 0600 "$repo_root/deploy/walkamon.env.example" /etc/walkamon/walkamon.env
  echo "Created /etc/walkamon/walkamon.env. Replace every REPLACE_ME value before starting the stack."
fi

install -m 0644 "$repo_root"/deploy/systemd/*.service /etc/systemd/system/
install -m 0644 "$repo_root"/deploy/systemd/*.timer /etc/systemd/system/

usermod -aG docker "$operator"
ufw default deny incoming
ufw default allow outgoing
ufw allow from "$host_only_source" to any port 22 proto tcp comment 'SSH from Windows host only'
ufw allow from "$host_only_source" to 192.168.120.10 port 8081 proto tcp comment 'Dozzle from Windows host only'
ufw allow from "$host_only_source" to 192.168.120.10 port 8082 proto tcp comment 'DbGate from Windows host only'
ufw --force enable

systemctl enable --now docker unattended-upgrades
systemctl daemon-reload
systemctl enable \
  walkamon-stack.service \
  walkamon-update.timer \
  walkamon-backup.timer \
  walkamon-restore-verify.timer \
  walkamon-db-size.timer

echo "Bootstrap complete. Next: configure the host-only NIC, install and test the Windows SSH key, run harden-ssh.sh, populate /etc/walkamon, copy credentials, then run deploy.sh."
