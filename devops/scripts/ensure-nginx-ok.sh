#!/usr/bin/env bash
# Re-apply nginx vapp site (localhost + default_server + static roots) and verify.
# Use when Public static exists but /form returns 502 — no npm rebuild needed.
#
# Usage (روی سرور):
#   bash ~/Api_Vapp_Manually/devops/scripts/ensure-nginx-ok.sh
#   SERVER_IP=195.24.237.132 bash ~/Api_Vapp_Manually/devops/scripts/ensure-nginx-ok.sh
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=lib/load-server-conf.sh
source "$SCRIPT_DIR/lib/load-server-conf.sh"
# shellcheck source=lib/nginx-http.sh
source "$SCRIPT_DIR/lib/nginx-http.sh"

SERVER_IP="${SERVER_IP:-195.24.237.132}"
FRONT_STATIC_ROOT="${FRONT_STATIC_ROOT:-}"
PUBLIC_STATIC_ROOT="${PUBLIC_STATIC_ROOT:-}"

[[ -f /var/www/vapp-admin/index.html ]] && FRONT_STATIC_ROOT="${FRONT_STATIC_ROOT:-/var/www/vapp-admin}"
[[ -f /var/www/vapp-public/index.html ]] && PUBLIC_STATIC_ROOT="${PUBLIC_STATIC_ROOT:-/var/www/vapp-public}"

echo "=== ensure-nginx-ok $(date -Is) ==="
echo "SERVER_IP=$SERVER_IP"
echo "admin_index=$([ -f /var/www/vapp-admin/index.html ] && echo yes || echo NO)"
echo "public_index=$([ -f /var/www/vapp-public/index.html ] && echo yes || echo NO)"

if [[ ! -f /var/www/vapp-public/index.html ]]; then
  echo "ERROR: /var/www/vapp-public/index.html missing — build Public first:" >&2
  echo "  SERVER_IP=$SERVER_IP bash $SCRIPT_DIR/deploy-public-front-host.sh" >&2
  exit 1
fi

SERVER_IP="$SERVER_IP" \
  FRONT_STATIC_ROOT="$FRONT_STATIC_ROOT" \
  PUBLIC_STATIC_ROOT="$PUBLIC_STATIC_ROOT" \
  DOMAIN_HOST="${DOMAIN_HOST:-}" \
  bash "$SCRIPT_DIR/apply-nginx.sh"

if ! verify_public_routes "$SERVER_IP"; then
  echo "ERROR: Public routes still not 200 after nginx reload" >&2
  echo "  grep -nE 'server_name|form|vapp-public|3006|default_server' /etc/nginx/sites-available/vapp | head -40" >&2
  echo "  curl -v -H 'Host: $SERVER_IP' http://127.0.0.1/form/x" >&2
  exit 1
fi

echo "OK: nginx Public routes healthy"
