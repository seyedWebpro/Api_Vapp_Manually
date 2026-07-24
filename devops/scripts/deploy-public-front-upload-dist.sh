#!/usr/bin/env bash
# Build Public_Vapp on Mac and upload dist to server with progress, resume and stall detection.
#
# Usage (from Api_Vapp_Manually root):
#   SERVER=vapp-prod bash devops/scripts/deploy-public-front-upload-dist.sh
#   SKIP_BUILD=1 SERVER=vapp-prod bash devops/scripts/deploy-public-front-upload-dist.sh
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=lib/deploy-progress.sh
source "$SCRIPT_DIR/lib/deploy-progress.sh"

LOCAL_API_DIR="${LOCAL_API_DIR:-$(cd "$SCRIPT_DIR/../.." && pwd)}"
LOCAL_PUBLIC_DIR="${LOCAL_PUBLIC_DIR:-$(cd "$LOCAL_API_DIR/../Public_Vapp" && pwd)}"
REMOTE_API_DIR="${REMOTE_API_DIR:-/root/Api_Vapp_Manually}"
PUBLIC_STATIC_ROOT="${PUBLIC_STATIC_ROOT:-/var/www/vapp-public}"
FRONT_STATIC_ROOT="${FRONT_STATIC_ROOT:-/var/www/vapp-admin}"
SERVER="${SERVER:-vapp-prod}"
VITE_API_URL="${VITE_API_URL:-}"
SKIP_BUILD="${SKIP_BUILD:-0}"
DEPLOY_STEP_TOTAL=4

# SSH options to avoid silent hangs and detect dead connections quickly.
SSH_OPTS=(
  -o ServerAliveInterval=15
  -o ServerAliveCountMax=4
  -o TCPKeepAlive=yes
  -o ConnectTimeout=30
  -o BatchMode=no
)

HAS_RSYNC=$(command -v rsync || true)

deploy_log "=== deploy-public-front-upload-dist ==="
deploy_log "Build: $LOCAL_PUBLIC_DIR"
deploy_log "Server: $SERVER:$PUBLIC_STATIC_ROOT"
deploy_log "Tools: rsync=${HAS_RSYNC:-none}"

[[ -d "$LOCAL_PUBLIC_DIR" ]] || { deploy_log "ERROR: $LOCAL_PUBLIC_DIR not found" >&2; exit 1; }
cd "$LOCAL_PUBLIC_DIR"

if [[ "$SKIP_BUILD" == "1" ]]; then
  deploy_step "Validate existing dist"
  [[ -f dist/index.html ]] || { deploy_log "ERROR: dist/ not found — run without SKIP_BUILD first" >&2; exit 1; }
else
  deploy_step "Build Public_Vapp locally"
  npm ci --no-audit --no-fund 2>/dev/null || npm install --no-audit --no-fund
  VITE_API_URL="$VITE_API_URL" npm run build
fi

[[ -d dist ]] || { deploy_log "ERROR: dist/ not found" >&2; exit 1; }

deploy_step "Test SSH → $SERVER"
if ! ssh "${SSH_OPTS[@]}" -o BatchMode=yes "$SERVER" "echo ok" >/dev/null 2>&1; then
  deploy_log "ERROR: SSH to $SERVER failed." >&2
  deploy_log "  dist ready: $LOCAL_PUBLIC_DIR/dist" >&2
  deploy_log "  retry: rsync -avz --delete $LOCAL_PUBLIC_DIR/dist/ $SERVER:$PUBLIC_STATIC_ROOT/" >&2
  exit 1
fi

deploy_step "Upload dist → $SERVER:$PUBLIC_STATIC_ROOT"
ssh "${SSH_OPTS[@]}" "$SERVER" "mkdir -p $PUBLIC_STATIC_ROOT"
if [[ -n "$HAS_RSYNC" ]]; then
  rsync -ah --progress --partial --delete \
    --timeout=300 \
    dist/ "$SERVER:$PUBLIC_STATIC_ROOT/"
else
  deploy_log "WARN: rsync not found — using scp fallback (no resume)"
  scp -r dist/* "$SERVER:$PUBLIC_STATIC_ROOT/"
fi

deploy_step "Apply nginx (Admin + Public)"
ssh "${SSH_OPTS[@]}" "$SERVER" "FRONT_STATIC_ROOT=$FRONT_STATIC_ROOT PUBLIC_STATIC_ROOT=$PUBLIC_STATIC_ROOT SERVER_IP=${SERVER#*@} bash $REMOTE_API_DIR/devops/scripts/apply-nginx.sh"

deploy_log "Done:"
deploy_log "  Public form:  http://${SERVER#*@}/form/{slug}"
deploy_log "  Public wheel: http://${SERVER#*@}/wheel/{slug}"
