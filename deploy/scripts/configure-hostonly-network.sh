#!/usr/bin/env bash
set -Eeuo pipefail

if [[ $EUID -ne 0 || $# -lt 1 || $# -gt 2 ]]; then
  echo "Usage: sudo $0 HOST_ONLY_INTERFACE [STATIC_CIDR]" >&2
  echo "Find it with: ip -br link" >&2
  exit 2
fi

interface=$1
static_cidr=${2:-192.168.120.10/24}
if ! ip link show "$interface" >/dev/null 2>&1; then
  echo "Interface does not exist: $interface" >&2
  exit 1
fi
if [[ ! "$static_cidr" =~ ^([0-9]{1,3}\.){3}[0-9]{1,3}/[0-9]{1,2}$ ]]; then
  echo "Invalid IPv4 CIDR: $static_cidr" >&2
  exit 1
fi

config=/etc/netplan/60-walkamon-hostonly.yaml
cat >"$config" <<EOF
network:
  version: 2
  ethernets:
    ${interface}:
      dhcp4: false
      addresses:
        - ${static_cidr}
EOF
chmod 600 "$config"
netplan generate
echo "Generated $config. Run 'sudo netplan try' from the VM console, then accept only if ${static_cidr%/*} is reachable."
