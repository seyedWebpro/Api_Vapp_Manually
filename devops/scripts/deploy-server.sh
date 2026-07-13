#!/usr/bin/env bash
# Orchestrator deploy — API + front (+ nginx)
# reuse: مسیر repoها، branch، modeها؛ سرویس اضافه (مثلاً worker) را اینجا وصل کن
#
# Usage (روی سرور بعد از SSH):
#   bash deploy-server.sh --fast
#   bash deploy-server.sh --fast --wait
#   bash deploy-server.sh --full
#   bash deploy-server.sh --api-only
#   bash deploy-server.sh --front-only
#   bash deploy-server.sh --public-only
#   bash deploy-server.sh --front-only --foreground   # progress زنده در همان SSH
#   bash deploy-server.sh --pull-only
#
# با درصد پیشرفت (پیشنهادی — مثل vamyab):
#   bash deploy-server-visible.sh --front-only
#   bash deploy-server-visible.sh --fast
#
# بعد از deploy:
#   bash health-check.sh
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
API_REPO_DIR="${API_REPO_DIR:-$HOME/Api_Vapp_Manually}"
FRONT_DIR="${FRONT_DIR:-$HOME/Admin_Vapp}"
PUBLIC_DIR="${PUBLIC_DIR:-$HOME/Public_Vapp}"
LAST_FRONT_LOG="${LAST_FRONT_DEPLOY_LOG:-$HOME/.vapp-last-front-deploy.log}"

usage() {
  sed -n '3,14p' "$0" | sed 's/^# \?//'
  exit "${1:-0}"
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
      echo "ERROR: unknown option: $1" >&2
      usage 1
      ;;
  esac
  shift
done

[[ -z "$MODE" || "$MODE" == "-h" || "$MODE" == "--help" ]] && usage 0

echo "=== deploy-server mode=$MODE $(date -Is) ==="

run_api() {
  local reload_nginx="${1:-0}" allow_slow="${2:-0}"
  ALLOW_SLOW_START="$allow_slow" RELOAD_NGINX="$reload_nginx" \
    bash "$SCRIPT_DIR/deploy-api.sh"
}

run_front() {
  if [[ "$FRONT_BG" == "1" ]]; then
    bash "$SCRIPT_DIR/deploy-front.sh" --background
  else
    bash "$SCRIPT_DIR/deploy-front.sh" --foreground
  fi
}

run_public() {
  if [[ "${PUBLIC_DEPLOY_MODE:-host}" == "host" ]]; then
    bash "$SCRIPT_DIR/deploy-public-front-host.sh"
  else
    bash "$SCRIPT_DIR/deploy-public-front.sh" --foreground
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
  env "${env_args[@]}" bash "$SCRIPT_DIR/apply-nginx.sh" || echo "WARN: apply-nginx failed" >&2
}

case "$MODE" in
  --pull-only)
    cd "$API_REPO_DIR" && git pull origin "${API_BRANCH:-main}"
    cd "$FRONT_DIR" && git pull origin "${FRONT_BRANCH:-main}"
    if [[ -d "$PUBLIC_DIR/.git" ]]; then
      cd "$PUBLIC_DIR" && git pull origin "${PUBLIC_BRANCH:-main}"
    fi
    echo "OK: git pull done for API + Admin + Public"
    ;;
  --api-only)
    run_api 0 0
    ;;
  --front-only)
    run_front
    ;;
  --public-only)
    run_public
    ;;
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
    echo "ERROR: unknown mode: $MODE" >&2
    usage 1
    ;;
esac

if [[ "$MODE" == "--fast" || "$MODE" == "--full" || "$MODE" == "--front-only" || "$MODE" == "--public-only" ]]; then
  log_hint=""
  [[ -f "$LAST_FRONT_LOG" ]] && log_hint="$(cat "$LAST_FRONT_LOG")"
  echo ""
  if [[ -n "$log_hint" ]]; then
    echo "Front log: tail -f $log_hint"
  else
    echo "Front log: tail -f /root/vapp-front-deploy-*.log"
  fi
  if [[ "$FRONT_BG" == "1" && "$MODE" != "--public-only" ]]; then
    echo "When done: bash $SCRIPT_DIR/health-check.sh"
  fi
  apply_nginx_all
fi

if [[ "$WAIT_FOR_FRONT" == "1" && "$FRONT_BG" == "1" ]]; then
  bash "$SCRIPT_DIR/wait-front-deploy.sh" || true
elif [[ "$MODE" == "--public-only" || ( "$MODE" != "--fast" && "$MODE" != "--full" && "$MODE" != "--front-only" ) ]]; then
  bash "$SCRIPT_DIR/health-check.sh" || true
fi

echo "=== deploy-server finished $(date -Is) ==="
