#!/usr/bin/env bash
# Orchestrator deploy — API + Admin + Public (+ nginx) با خطای واضح
#
# Usage (on server after SSH):
#   bash deploy-server.sh --fast
#   bash deploy-server.sh --fast --wait
#   bash deploy-server.sh --full
#   bash deploy-server.sh --api-only
#   bash deploy-server.sh --front-only --foreground
#   bash deploy-server.sh --public-only
#   bash deploy-server.sh --pull-only
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=lib/deploy-progress.sh
source "$SCRIPT_DIR/lib/deploy-progress.sh"
# shellcheck source=lib/deploy-fail.sh
source "$SCRIPT_DIR/lib/deploy-fail.sh"
# shellcheck source=lib/load-server-conf.sh
source "$SCRIPT_DIR/lib/load-server-conf.sh"

API_REPO_DIR="${API_REPO_DIR:-$HOME/Api_Vapp_Manually}"
FRONT_DIR="${FRONT_DIR:-$HOME/Admin_Vapp}"
PUBLIC_DIR="${PUBLIC_DIR:-$HOME/Public_Vapp}"
LAST_FRONT_LOG="${LAST_FRONT_DEPLOY_LOG:-$HOME/.vapp-last-front-deploy.log}"
DEPLOY_STEP_TOTAL=5
FRONT_DEPLOY_MODE="${FRONT_DEPLOY_MODE:-host}"
PUBLIC_DEPLOY_MODE="${PUBLIC_DEPLOY_MODE:-host}"

usage() {
  sed -n '3,14p' "$0" | sed 's/^# \?//'
  exit "${1:-0}"
}

fail() {
  deploy_fail "$@" || true
  echo "  bash $SCRIPT_DIR/diagnose-deploy.sh" >&2
  exit 1
}

MODE="${1:-}"
WAIT_FOR_FRONT=0
FRONT_BG=1

shift || true
while [[ $# -gt 0 ]]; do
  case "$1" in
    --wait) WAIT_FOR_FRONT=1 ;;
    --foreground) FRONT_BG=0 ;;
    *)
      fail "unknown option: $1" "bash $0 --help"
      ;;
  esac
  shift
done

[[ -z "$MODE" || "$MODE" == "-h" || "$MODE" == "--help" ]] && usage 0

deploy_log "=== deploy-server mode=$MODE FRONT_DEPLOY_MODE=$FRONT_DEPLOY_MODE ==="

run_api() {
  local reload_nginx="${1:-0}" allow_slow="${2:-0}"
  bash "$SCRIPT_DIR/ensure-dbvapp.sh" || deploy_log "WARN: ensure-dbvapp non-fatal"
  if ! ALLOW_SLOW_START="$allow_slow" RELOAD_NGINX="$reload_nginx" \
    bash "$SCRIPT_DIR/deploy-api.sh"; then
    fail "API deploy/Migrate failed" \
      "bash $SCRIPT_DIR/ensure-dbvapp.sh --restart-api --wait" \
      "bash $SCRIPT_DIR/diagnose-deploy.sh"
  fi
}

run_front() {
  export FRONT_DEPLOY_MODE
  if [[ "$FRONT_DEPLOY_MODE" == "host" ]]; then
    bash "$SCRIPT_DIR/deploy-front-host.sh" || fail "Admin host deploy failed" "bash $SCRIPT_DIR/diagnose-deploy.sh"
    return
  fi
  if [[ "$FRONT_BG" == "1" ]]; then
    bash "$SCRIPT_DIR/deploy-front.sh" --background
  else
    bash "$SCRIPT_DIR/deploy-front.sh" --foreground || fail "Admin docker deploy failed"
  fi
}

run_public() {
  [[ -d "$PUBLIC_DIR" ]] || fail "Public missing: $PUBLIC_DIR" \
    "git clone git@github.com:seyedWebpro/PublicWeb_Vapp.git $PUBLIC_DIR"
  if [[ "${PUBLIC_DEPLOY_MODE:-host}" == "host" ]]; then
    bash "$SCRIPT_DIR/deploy-public-front-host.sh" || fail "Public deploy failed" "bash $SCRIPT_DIR/diagnose-deploy.sh"
  else
    bash "$SCRIPT_DIR/deploy-public-front.sh" --foreground || fail "Public docker deploy failed"
  fi
}

apply_nginx_all() {
  local env_args=()
  if [[ "${FRONT_DEPLOY_MODE:-host}" == "host" ]]; then
    env_args+=(FRONT_STATIC_ROOT="${FRONT_STATIC_ROOT:-/var/www/vapp-admin}")
  fi
  if [[ "${PUBLIC_DEPLOY_MODE:-host}" == "host" ]]; then
    env_args+=(PUBLIC_STATIC_ROOT="${PUBLIC_STATIC_ROOT:-/var/www/vapp-public}")
  fi
  env "${env_args[@]}" SERVER_IP="${SERVER_IP}" bash "$SCRIPT_DIR/apply-nginx.sh" \
    || deploy_log "WARN: apply-nginx failed" >&2
}

case "$MODE" in
  --pull-only)
    deploy_step "safe sync API"
    API_REPO_DIR="$API_REPO_DIR" API_BRANCH="${API_BRANCH:-main}" bash "$SCRIPT_DIR/sync-api-repo-safe.sh"
    deploy_step "safe sync Admin"
    if [[ -d "$FRONT_DIR/.git" ]]; then
      (cd "$FRONT_DIR" && git fetch origin "${FRONT_BRANCH:-main}" && git reset --hard "origin/${FRONT_BRANCH:-main}" && git clean -fd)
    fi
    if [[ -d "$PUBLIC_DIR/.git" ]]; then
      deploy_step "safe sync Public"
      (cd "$PUBLIC_DIR" && git fetch origin "${PUBLIC_BRANCH:-main}" && git reset --hard "origin/${PUBLIC_BRANCH:-main}" && git clean -fd)
    fi
    deploy_log "OK: safe sync done for API + Admin + Public"
    ;;
  --api-only) run_api 0 0 ;;
  --front-only) run_front ;;
  --public-only) run_public ;;
  --fast)
    run_api 0 1
    run_front
    run_public
    ;;
  --full)
    run_api 1 1
    run_front
    run_public
    ;;
  *)
    fail "unknown mode: $MODE" "bash $0 --help"
    ;;
esac

if [[ "$MODE" == "--fast" || "$MODE" == "--full" || "$MODE" == "--front-only" || "$MODE" == "--public-only" ]]; then
  apply_nginx_all
fi

if [[ "$WAIT_FOR_FRONT" == "1" && "$FRONT_BG" == "1" && "$FRONT_DEPLOY_MODE" != "host" ]]; then
  bash "$SCRIPT_DIR/wait-front-deploy.sh" || true
fi

if [[ "$MODE" != "--pull-only" ]]; then
  if ! HEALTH_ATTEMPTS=3 HEALTH_SLEEP=8 bash "$SCRIPT_DIR/health-check.sh"; then
    fail "post-deploy health-check failed" "bash $SCRIPT_DIR/diagnose-deploy.sh"
  fi
fi

deploy_ok_box "deploy-server $MODE finished"
deploy_log "=== deploy-server finished ==="
