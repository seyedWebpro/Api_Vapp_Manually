#!/usr/bin/env bash
# node_modules را روی Mac pack می‌کند، به سرور می‌فرستد، vite build روی سرور
# وقتی npm registry روی سرور کار نمی‌کند (ETIMEDOUT / ECONNREFUSED)
#
# Usage (روی Mac):
#   SERVER=root@185.116.162.233 bash devops/scripts/deploy-front-upload-node-modules.sh
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
LOCAL_API_DIR="$(cd "$SCRIPT_DIR/../.." && pwd)"
LOCAL_FRONT_DIR="${LOCAL_FRONT_DIR:-$(cd "$LOCAL_API_DIR/../Admin_Vapp" && pwd)}"
REMOTE_FRONT_DIR="${REMOTE_FRONT_DIR:-/root/Admin_Vapp}"
REMOTE_API_DIR="${REMOTE_API_DIR:-/root/Api_Vapp_Manually}"
SERVER="${SERVER:-root@185.116.162.233}"
VITE_API_URL="${VITE_API_URL:-}"
TARBALL="${TARBALL:-/tmp/vapp-admin-node_modules.tgz}"

log() { echo "[$(date '+%Y-%m-%dT%H:%M:%S')] $*"; }

[[ -d "$LOCAL_FRONT_DIR" ]] || { echo "ERROR: Admin_Vapp not found" >&2; exit 1; }

log "1/4 npm ci on Mac..."
cd "$LOCAL_FRONT_DIR"
if [[ ! -d node_modules ]] || [[ "${FORCE_NPM_CI:-}" == "1" ]]; then
  npm ci --no-audit --no-fund
fi

log "2/4 pack node_modules → $TARBALL"
tar czf "$TARBALL" -C "$LOCAL_FRONT_DIR" node_modules
log "tarball: $(du -sh "$TARBALL" | cut -f1)"

log "3/4 upload → $SERVER:$REMOTE_FRONT_DIR/"
ssh -o ConnectTimeout=20 "$SERVER" "mkdir -p $REMOTE_FRONT_DIR"
scp "$TARBALL" "$SERVER:$REMOTE_FRONT_DIR/node_modules.tgz"

log "4/4 vite build on server (no npm registry)..."
ssh "$SERVER" "cd $REMOTE_FRONT_DIR && git pull origin main && rm -rf node_modules && tar xzf node_modules.tgz && rm -f node_modules.tgz && printf 'VITE_API_URL=\n' > .env.production && SKIP_NPM_CI=1 VITE_API_URL=$VITE_API_URL bash $REMOTE_API_DIR/devops/scripts/deploy-front-host.sh"

log "Done: http://${SERVER#*@}/auth"
