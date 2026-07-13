#!/usr/bin/env bash
# Build Public_Vapp روی Mac و آپلود dist به سرور (لینک‌های SMS فرم/گردونه)
#
# Usage (روی Mac):
#   SERVER=vapp-prod bash devops/scripts/deploy-public-front-upload-dist.sh
#   SKIP_BUILD=1 SERVER=vapp-prod bash devops/scripts/deploy-public-front-upload-dist.sh
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
LOCAL_API_DIR="$(cd "$SCRIPT_DIR/../.." && pwd)"
LOCAL_PUBLIC_DIR="${LOCAL_PUBLIC_DIR:-$(cd "$LOCAL_API_DIR/../Public_Vapp" && pwd)}"
REMOTE_API_DIR="${REMOTE_API_DIR:-/root/Api_Vapp_Manually}"
PUBLIC_STATIC_ROOT="${PUBLIC_STATIC_ROOT:-/var/www/vapp-public}"
FRONT_STATIC_ROOT="${FRONT_STATIC_ROOT:-/var/www/vapp-admin}"
SERVER="${SERVER:-vapp-prod}"
VITE_API_URL="${VITE_API_URL:-}"
SKIP_BUILD="${SKIP_BUILD:-0}"

log() {
  echo "[$(date '+%Y-%m-%dT%H:%M:%S')] $*"
}

[[ -d "$LOCAL_PUBLIC_DIR" ]] || { echo "ERROR: $LOCAL_PUBLIC_DIR not found" >&2; exit 1; }

cd "$LOCAL_PUBLIC_DIR"

if [[ "$SKIP_BUILD" == "1" ]]; then
  log "SKIP_BUILD=1 — using existing dist/"
  [[ -f dist/index.html ]] || { echo "ERROR: dist/ not found — run without SKIP_BUILD first" >&2; exit 1; }
else
  log "Building Public_Vapp locally..."
  npm ci --no-audit --no-fund 2>/dev/null || npm install --no-audit --no-fund
  VITE_API_URL="$VITE_API_URL" npm run build
fi

[[ -d dist ]] || { echo "ERROR: dist/ not found" >&2; exit 1; }

log "Testing SSH → $SERVER ..."
if ! ssh -o ConnectTimeout=15 -o BatchMode=yes "$SERVER" "echo ok" >/dev/null 2>&1; then
  echo "ERROR: SSH to $SERVER failed." >&2
  echo "  dist آماده: $LOCAL_PUBLIC_DIR/dist" >&2
  echo "  بعداً: rsync -avz --delete $LOCAL_PUBLIC_DIR/dist/ $SERVER:$PUBLIC_STATIC_ROOT/" >&2
  exit 1
fi

log "Uploading dist → $SERVER:$PUBLIC_STATIC_ROOT"
ssh "$SERVER" "mkdir -p $PUBLIC_STATIC_ROOT"
rsync -avz --delete dist/ "$SERVER:$PUBLIC_STATIC_ROOT/"

log "Applying nginx (Admin + Public)..."
ssh "$SERVER" "FRONT_STATIC_ROOT=$FRONT_STATIC_ROOT PUBLIC_STATIC_ROOT=$PUBLIC_STATIC_ROOT SERVER_IP=${SERVER#*@} bash $REMOTE_API_DIR/devops/scripts/apply-nginx.sh"

log "Done:"
log "  Public form:  http://${SERVER#*@}/form/{slug}"
log "  Public wheel: http://${SERVER#*@}/wheel/{slug}"
