#!/usr/bin/env bash
# Build Admin روی Mac و آپلود dist به سرور (سریع‌تر از npm ci روی سرور)
#
# Usage (روی Mac):
#   SERVER=root@185.116.162.233 bash devops/scripts/deploy-front-upload-dist.sh
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
LOCAL_API_DIR="$(cd "$SCRIPT_DIR/../.." && pwd)"
LOCAL_FRONT_DIR="${LOCAL_FRONT_DIR:-$(cd "$LOCAL_API_DIR/../Admin_Vapp" && pwd)}"
REMOTE_API_DIR="${REMOTE_API_DIR:-/root/Api_Vapp_Manually}"
FRONT_STATIC_ROOT="${FRONT_STATIC_ROOT:-/var/www/vapp-admin}"
SERVER="${SERVER:-root@185.116.162.233}"
VITE_API_URL="${VITE_API_URL:-}"

log() { echo "[$(date -Is)] $*"; }

[[ -d "$LOCAL_FRONT_DIR" ]] || { echo "ERROR: $LOCAL_FRONT_DIR not found" >&2; exit 1; }

log "Building Admin locally..."
cd "$LOCAL_FRONT_DIR"
npm ci --no-audit --no-fund
VITE_API_URL="$VITE_API_URL" npm run build

[[ -d dist ]] || { echo "ERROR: dist/ not found" >&2; exit 1; }

log "Uploading dist → $SERVER:$FRONT_STATIC_ROOT"
ssh "$SERVER" "mkdir -p $FRONT_STATIC_ROOT"
rsync -avz --delete dist/ "$SERVER:$FRONT_STATIC_ROOT/"

log "Applying nginx..."
ssh "$SERVER" "FRONT_STATIC_ROOT=$FRONT_STATIC_ROOT bash $REMOTE_API_DIR/devops/scripts/apply-nginx.sh"

log "Done: http://${SERVER#*@}/admin"
