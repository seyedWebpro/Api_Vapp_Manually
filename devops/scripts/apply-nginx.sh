#!/usr/bin/env bash
# نصب/آپدیت Nginx از devops/deploy/nginx-vapp.conf.example
#
# Usage:
#   bash apply-nginx.sh
#   SERVER_IP=185.116.162.233 bash apply-nginx.sh
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DEVOPS_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
API_REPO_DIR="${API_REPO_DIR:-$HOME/Api_Vapp_Manually}"
SERVER_IP="${SERVER_IP:-185.116.162.233}"

SRC="${DEVOPS_DIR}/deploy/nginx-vapp.conf.example"
DEST="/etc/nginx/sites-available/vapp"

[[ -f "$SRC" ]] || { echo "ERROR: nginx example not found: $SRC" >&2; exit 1; }

if [[ "${EUID:-$(id -u)}" -ne 0 ]]; then
  sudo SERVER_IP="$SERVER_IP" bash "$0"
  exit $?
fi

sed "s/server_name 185.116.162.233;/server_name ${SERVER_IP};/" "$SRC" > "$DEST"
ln -sf "$DEST" /etc/nginx/sites-enabled/vapp
rm -f /etc/nginx/sites-enabled/default
nginx -t
systemctl reload nginx
echo "OK: nginx reloaded — server_name=$SERVER_IP"
