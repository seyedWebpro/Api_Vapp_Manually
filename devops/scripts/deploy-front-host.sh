#!/usr/bin/env bash
# Deploy front روی host — npm build + nginx static (بدون Docker Hub)
# reuse: FRONT_DIR، FRONT_STATIC_ROOT، NPM_REGISTRY، SERVER_IP
#
# Usage:
#   bash deploy-front-host.sh
#   SERVER_IP=185.116.162.233 bash deploy-front-host.sh
#   SKIP_NPM_CI=1 bash deploy-front-host.sh   # اگر node_modules از قبل نصب است
set -euo pipefail

export DEBIAN_FRONTEND=noninteractive
export NEEDRESTART_MODE=a

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
FRONT_DIR="${FRONT_DIR:-$HOME/Admin_Vapp}"
FRONT_BRANCH="${FRONT_BRANCH:-main}"
FRONT_STATIC_ROOT="${FRONT_STATIC_ROOT:-/var/www/vapp-admin}"
SERVER_IP="${SERVER_IP:-185.116.162.233}"
VITE_API_URL="${VITE_API_URL:-}"
NPM_REGISTRY="${NPM_REGISTRY:-https://registry.npmmirror.com}"

log() { echo "[$(date -Is)] $*"; }

ensure_node() {
  if command -v node >/dev/null 2>&1 && [[ "$(node -p 'process.versions.node.split(".")[0]')" -ge 18 ]]; then
    log "Node $(node -v) OK"
    return 0
  fi
  log "Installing Node.js 22..."
  curl -fsSL https://deb.nodesource.com/setup_22.x | bash -
  apt-get install -y -qq nodejs
  log "Node $(node -v) installed"
}

apply_nginx_static() {
  FRONT_STATIC_ROOT="$FRONT_STATIC_ROOT" SERVER_IP="$SERVER_IP" bash "$SCRIPT_DIR/apply-nginx.sh"
}

[[ -d "$FRONT_DIR" ]] || { echo "ERROR: $FRONT_DIR not found" >&2; exit 1; }

cd "$FRONT_DIR"
[[ -d .git ]] && git pull origin "$FRONT_BRANCH"

ensure_node

if [[ -n "$NPM_REGISTRY" ]]; then
  npm config set registry "$NPM_REGISTRY"
fi

export npm_config_fetch_timeout="${NPM_FETCH_TIMEOUT:-600000}"
export npm_config_fetch_retries="${NPM_FETCH_RETRIES:-5}"

if [[ "${SKIP_NPM_CI:-}" == "1" ]] && [[ -d node_modules ]]; then
  log "SKIP_NPM_CI=1 — using existing node_modules"
else
  log "npm ci... (روی سرور ایران ممکن است ۱۵–۳۰ دقیقه طول بکشد — صبر کنید)"
  npm ci --no-audit --no-fund --loglevel=info
fi

log "vite build (VITE_API_URL=${VITE_API_URL:-empty})..."
VITE_API_URL="$VITE_API_URL" npx vite build

[[ -d dist ]] || { echo "ERROR: dist/ not found after build" >&2; exit 1; }

mkdir -p "$FRONT_STATIC_ROOT"
rm -rf "${FRONT_STATIC_ROOT:?}/"*
cp -a dist/. "$FRONT_STATIC_ROOT/"
chmod -R a+rX "$FRONT_STATIC_ROOT"

apply_nginx_static

front_code="$(curl -sS -m 15 -o /dev/null -w '%{http_code}' "http://127.0.0.1/" 2>/dev/null || echo "000")"
log "FRONT (nginx static): $front_code"
log "Admin: http://${SERVER_IP}/admin"
echo "=== deploy-front-host done ==="

[[ "$front_code" == "200" ]] || exit 1
