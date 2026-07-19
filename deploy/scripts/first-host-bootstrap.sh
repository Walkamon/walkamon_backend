#!/usr/bin/env bash
set -Eeuo pipefail

if [[ $EUID -ne 0 ]]; then
  echo "Run this script with sudo." >&2
  exit 1
fi

script_dir=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
operator=${SUDO_USER:-walkamon}
host_only_interface=${WALKAMON_HOST_ONLY_INTERFACE:-ens192}
host_only_cidr=${WALKAMON_HOST_ONLY_CIDR:-192.168.120.10/24}

root_source=$(findmnt -n -o SOURCE /)
if [[ "$root_source" == /dev/mapper/* ]] && command -v lvs >/dev/null 2>&1; then
  free_extents=$(lvs --noheadings -o vg_free_count "$root_source" | xargs)
  if [[ "$free_extents" =~ ^[0-9]+$ ]] && (( free_extents > 0 )); then
    lvextend --resizefs --extents +100%FREE "$root_source"
  fi
fi

"$script_dir/bootstrap-ubuntu.sh"
"$script_dir/configure-hostonly-network.sh" "$host_only_interface" "$host_only_cidr"
"$script_dir/harden-ssh.sh" "$operator"

systemd-run \
  --unit=walkamon-netplan-apply \
  --on-active=5s \
  /usr/sbin/netplan apply

echo "Initial host bootstrap completed."
echo "The host-only address will change to ${host_only_cidr%/*} in about five seconds."
