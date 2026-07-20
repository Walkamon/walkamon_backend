#!/usr/bin/env bash
set -Eeuo pipefail

if [[ $EUID -ne 0 || $# -ne 1 ]]; then
  echo "Usage: sudo $0 UBUNTU_USER" >&2
  exit 2
fi

operator=$1
if ! id "$operator" >/dev/null 2>&1; then
  echo "User does not exist: $operator" >&2
  exit 1
fi

home_dir=$(getent passwd "$operator" | cut -d: -f6)
authorized_keys="${home_dir}/.ssh/authorized_keys"
if [[ ! -s "$authorized_keys" ]]; then
  echo "Refusing to disable password login because $authorized_keys is missing or empty." >&2
  echo "Install and test the Windows host public key first." >&2
  exit 1
fi

install -d -m 0700 -o "$operator" -g "$operator" "${home_dir}/.ssh"
chown "$operator:$operator" "$authorized_keys"
chmod 0600 "$authorized_keys"

# OpenSSH keeps the first value it reads for most scalar options. Cloud-init
# writes 50-cloud-init.conf, so this drop-in must sort before it.
config=/etc/ssh/sshd_config.d/00-walkamon-hardening.conf
cat >"$config" <<'EOF'
PermitRootLogin no
PasswordAuthentication no
KbdInteractiveAuthentication no
PubkeyAuthentication yes
AuthenticationMethods publickey
EOF
chmod 0644 "$config"
rm -f /etc/ssh/sshd_config.d/99-walkamon-hardening.conf

/usr/sbin/sshd -t
systemctl reload ssh.service

echo "SSH key-only authentication is enabled for non-root users."
echo "Keep the current session open and verify a second login before disconnecting."
