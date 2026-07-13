#!/usr/bin/env bash
# Build Admin روی Mac و آپلود dist به سرور (سریع‌تر از npm ci روی سرور)
#
# Usage (روی Mac):
#   SERVER=root@185.116.162.233 bash devops/scripts/deploy-front-upload-dist.sh
#   SKIP_BUILD=1 SERVER=root@185.116.162.233 bash devops/scripts/deploy-front-upload-dist.sh
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
LOCAL_API_DIR="$(cd "$SCRIPT_DIR/../.." && pwd)"
LOCAL_FRONT_DIR="${LOCAL_FRONT_DIR:-$(cd "$LOCAL_API_DIR/../Admin_Vapp" && pwd)}"
REMOTE_API_DIR="${REMOTE_API_DIR:-/root/Api_Vapp_Manually}"
FRONT_STATIC_ROOT="${FRONT_STATIC_ROOT:-/var/www/vapp-admin}"
SERVER="${SERVER:-root@185.116.162.233}"
VITE_API_URL="${VITE_API_URL:-}"
SKIP_BUILD="${SKIP_BUILD:-0}"

log() {
  echo "[$(date '+%Y-%m-%dT%H:%M:%S')] $*"
}

[[ -d "$LOCAL_FRONT_DIR" ]] || { echo "ERROR: $LOCAL_FRONT_DIR not found" >&2; exit 1; }

cd "$LOCAL_FRONT_DIR"

if [[ "$SKIP_BUILD" == "1" ]]; then
  log "SKIP_BUILD=1 — using existing dist/"
  [[ -d dist/index.html || -f dist/index.html ]] || { echo "ERROR: dist/ not found — run without SKIP_BUILD first" >&2; exit 1; }
else
  log "Building Admin locally..."
  npm ci --no-audit --no-fund
  VITE_API_URL="$VITE_API_URL" npm run build
fi

[[ -d dist ]] || { echo "ERROR: dist/ not found" >&2; exit 1; }

log "Testing SSH → $SERVER ..."
if ! ssh -o ConnectTimeout=15 -o BatchMode=yes "$SERVER" "echo ok" >/dev/null 2>&1; then
  echo "ERROR: SSH to $SERVER failed (Connection refused / timeout / no key)." >&2
  echo "  1) From Mac: ssh $SERVER" >&2
  echo "  2) Open port 22 in VPS firewall for your Mac IP" >&2
  echo "  3) dist is ready at: $LOCAL_FRONT_DIR/dist — upload when SSH works:" >&2
  echo "     rsync -avz --delete $LOCAL_FRONT_DIR/dist/ $SERVER:$FRONT_STATIC_ROOT/" >&2
  exit 1
fi

log "Uploading dist → $SERVER:$FRONT_STATIC_ROOT"
ssh "$SERVER" "mkdir -p $FRONT_STATIC_ROOT"
rsync -avz --delete dist/ "$SERVER:$FRONT_STATIC_ROOT/"

log "Applying nginx (Admin + Public)..."
ssh "$SERVER" "FRONT_STATIC_ROOT=$FRONT_STATIC_ROOT PUBLIC_STATIC_ROOT=${PUBLIC_STATIC_ROOT:-/var/www/vapp-public} SERVER_IP=${SERVER#*@} bash $REMOTE_API_DIR/devops/scripts/apply-nginx.sh"

log "Done: http://${SERVER#*@}/auth"
