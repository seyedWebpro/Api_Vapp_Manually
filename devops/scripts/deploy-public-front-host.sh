#!/usr/bin/env bash
# Deploy Public_Vapp روی host — npm build + nginx static (لینک SMS)
#
# Usage (روی سرور):
#   bash deploy-public-front-host.sh
#   SKIP_NPM_CI=1 bash deploy-public-front-host.sh
set -euo pipefail

export DEBIAN_FRONTEND=noninteractive
export NEEDRESTART_MODE=a

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=lib/deploy-progress.sh
source "$SCRIPT_DIR/lib/deploy-progress.sh"
# shellcheck source=lib/load-server-conf.sh
source "$SCRIPT_DIR/lib/load-server-conf.sh"

PUBLIC_DIR="${PUBLIC_DIR:-$HOME/Public_Vapp}"
PUBLIC_BRANCH="${PUBLIC_BRANCH:-main}"
PUBLIC_STATIC_ROOT="${PUBLIC_STATIC_ROOT:-/var/www/vapp-public}"
FRONT_STATIC_ROOT="${FRONT_STATIC_ROOT:-/var/www/vapp-admin}"
SERVER_IP="${SERVER_IP:-195.24.237.132}"
VITE_API_URL="${VITE_API_URL:-}"
NPM_REGISTRY="${NPM_REGISTRY:-https://registry.npmjs.org}"
DEPLOY_STEP_TOTAL=6

apply_nginx_all() {
  FRONT_STATIC_ROOT="$FRONT_STATIC_ROOT" \
    PUBLIC_STATIC_ROOT="$PUBLIC_STATIC_ROOT" \
    SERVER_IP="$SERVER_IP" \
    bash "$SCRIPT_DIR/apply-nginx.sh"
}

ensure_node() {
  if command -v node >/dev/null 2>&1 && [[ "$(node -p 'process.versions.node.split(".")[0]')" -ge 18 ]]; then
    deploy_log "Node $(node -v) OK"
    return 0
  fi
  deploy_step "Installing Node.js 22"
  curl -fsSL https://deb.nodesource.com/setup_22.x | bash -
  apt-get install -y -qq nodejs
}

[[ -d "$PUBLIC_DIR" ]] || { echo "ERROR: $PUBLIC_DIR not found — clone یا rsync از Mac" >&2; exit 1; }

deploy_log "=== deploy-public-front-host started ==="
deploy_log "PUBLIC_DIR=$PUBLIC_DIR  STATIC=$PUBLIC_STATIC_ROOT"

cd "$PUBLIC_DIR"

deploy_step "git sync ($PUBLIC_BRANCH)"
if [[ -d .git ]]; then
  # Avoid dirty-tree pull failures (same pattern as sync-api-repo-safe)
  git fetch origin "$PUBLIC_BRANCH"
  git checkout -B "$PUBLIC_BRANCH" "origin/$PUBLIC_BRANCH"
  git reset --hard "origin/$PUBLIC_BRANCH"
  git clean -fd -e node_modules -e dist -e .env -e .env.* || true
fi

deploy_step "Node.js check"
ensure_node

if [[ -n "$NPM_REGISTRY" ]]; then
  npm config set registry "$NPM_REGISTRY"
fi

if [[ "${SKIP_NPM_CI:-}" == "1" ]] && [[ -d node_modules ]]; then
  deploy_step "npm install (skipped — SKIP_NPM_CI=1)"
else
  deploy_step "npm install"
  rm -rf node_modules
  deploy_run_npm_deps
fi

deploy_step "vite build"
deploy_run_vite_build "$VITE_API_URL"

[[ -d dist ]] || { echo "ERROR: dist/ not found" >&2; exit 1; }

deploy_step "copy static → $PUBLIC_STATIC_ROOT"
mkdir -p "$PUBLIC_STATIC_ROOT"
rm -rf "${PUBLIC_STATIC_ROOT:?}/"*
cp -a dist/. "$PUBLIC_STATIC_ROOT/"
chmod -R a+rX "$PUBLIC_STATIC_ROOT"

deploy_step "nginx reload"
apply_nginx_all

# shellcheck source=lib/nginx-http.sh
source "$SCRIPT_DIR/lib/nginx-http.sh"

idx_ok=0
[[ -f "${PUBLIC_STATIC_ROOT}/index.html" ]] && idx_ok=1
deploy_log "PUBLIC index.html present=$idx_ok"
deploy_log "Form:  http://${SERVER_IP}/form/{slug}"
deploy_log "Wheel: http://${SERVER_IP}/wheel/{slug}"
deploy_log "=== deploy-public-front-host done ==="

if [[ "$idx_ok" != "1" ]]; then
  echo "ERROR: ${PUBLIC_STATIC_ROOT}/index.html missing after copy" >&2
  exit 1
fi
# apply-nginx already verifies routes when static; double-check for clear exit
if ! verify_public_routes "$SERVER_IP"; then
  echo "ERROR: public routes not 200 — try: bash $SCRIPT_DIR/ensure-nginx-ok.sh" >&2
  exit 1
fi
echo "OK: Public static live"
