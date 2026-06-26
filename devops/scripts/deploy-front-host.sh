#!/usr/bin/env bash
# Deploy front روی host — npm build + nginx static (بدون Docker Hub)
# reuse: FRONT_DIR، FRONT_STATIC_ROOT، NPM_REGISTRY، SERVER_IP
#
# Usage:
#   bash deploy-front-host.sh
#   SERVER_IP=185.116.162.233 bash deploy-front-host.sh
#   SKIP_NPM_CI=1 bash deploy-front-host.sh   # اگر node_modules از قبل نصب است
#   NPM_LOGLEVEL=info bash deploy-front-host.sh   # خروجی کمتر
set -euo pipefail

export DEBIAN_FRONTEND=noninteractive
export NEEDRESTART_MODE=a

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=lib/deploy-progress.sh
source "$SCRIPT_DIR/lib/deploy-progress.sh"

FRONT_DIR="${FRONT_DIR:-$HOME/Admin_Vapp}"
FRONT_BRANCH="${FRONT_BRANCH:-main}"
FRONT_STATIC_ROOT="${FRONT_STATIC_ROOT:-/var/www/vapp-admin}"
SERVER_IP="${SERVER_IP:-185.116.162.233}"
VITE_API_URL="${VITE_API_URL:-}"
NPM_REGISTRY="${NPM_REGISTRY:-https://npm.iranserver.com/repository/npm/}"
NPM_REGISTRY_FALLBACK="${NPM_REGISTRY_FALLBACK:-https://registry.npmjs.org}"
DEPLOY_STEP_TOTAL=6

apply_iran_build_mirrors() {
  if [[ -x "$SCRIPT_DIR/apply-build-mirrors-iranserver.sh" ]]; then
    if [[ "$(id -u)" -eq 0 ]]; then
      bash "$SCRIPT_DIR/apply-build-mirrors-iranserver.sh"
    else
      sudo bash "$SCRIPT_DIR/apply-build-mirrors-iranserver.sh" 2>/dev/null || true
    fi
  fi
}

ensure_node() {
  if command -v node >/dev/null 2>&1 && [[ "$(node -p 'process.versions.node.split(".")[0]')" -ge 18 ]]; then
    deploy_log "Node $(node -v) OK"
    return 0
  fi
  deploy_step "Installing Node.js 22"
  curl -fsSL https://deb.nodesource.com/setup_22.x | bash -
  apt-get install -y -qq nodejs
  deploy_log "Node $(node -v) installed"
}

apply_nginx_static() {
  FRONT_STATIC_ROOT="$FRONT_STATIC_ROOT" SERVER_IP="$SERVER_IP" bash "$SCRIPT_DIR/apply-nginx.sh"
}

[[ -d "$FRONT_DIR" ]] || { echo "ERROR: $FRONT_DIR not found" >&2; exit 1; }

deploy_log "=== deploy-front-host started ==="
deploy_log "FRONT_DIR=$FRONT_DIR  STATIC=$FRONT_STATIC_ROOT  VITE_API_URL=${VITE_API_URL:-<same-origin>}"

cd "$FRONT_DIR"

deploy_step "git pull ($FRONT_BRANCH)"
if [[ -d .git ]]; then
  git pull origin "$FRONT_BRANCH"
fi

deploy_step "Node.js check"
ensure_node

apply_iran_build_mirrors

deploy_step "npm registry"
if [[ -n "$NPM_REGISTRY" ]]; then
  npm config set registry "$NPM_REGISTRY"
fi
deploy_log "npm $(npm -v) | node $(node -v)"

if [[ "${SKIP_NPM_CI:-}" == "1" ]] && [[ -d node_modules ]]; then
  deploy_step "npm install (skipped — SKIP_NPM_CI=1)"
  deploy_log "Using existing node_modules ($(du -sh node_modules | cut -f1))"
else
  deploy_step "npm install (dependencies — iranserver npm + fallback)"
  rm -rf node_modules
  deploy_run_npm_deps
fi

deploy_step "vite build (production)"
deploy_run_vite_build "$VITE_API_URL"

[[ -d dist ]] || { echo "ERROR: dist/ not found after build" >&2; exit 1; }
deploy_log "dist size: $(du -sh dist | cut -f1) | files: $(find dist -type f | wc -l | tr -d ' ')"

deploy_step "copy static files → $FRONT_STATIC_ROOT"
mkdir -p "$FRONT_STATIC_ROOT"
rm -rf "${FRONT_STATIC_ROOT:?}/"*
cp -a dist/. "$FRONT_STATIC_ROOT/"
chmod -R a+rX "$FRONT_STATIC_ROOT"

deploy_step "nginx reload"
apply_nginx_static

front_code="$(curl -sS -m 15 -o /dev/null -w '%{http_code}' "http://127.0.0.1/" 2>/dev/null || echo "000")"
deploy_log "FRONT (nginx static): HTTP $front_code"
deploy_log "Admin: http://${SERVER_IP}/auth"
deploy_log "=== deploy-front-host done ==="

[[ "$front_code" == "200" ]] || exit 1
