#!/usr/bin/env bash
# ★ یک‌خطی امن آپدیت سرور — بدون خطای «local changes would be overwritten»
#
# Usage (روی سرور):
#   bash ~/Api_Vapp_Manually/devops/scripts/server-update.sh
#   bash ~/Api_Vapp_Manually/devops/scripts/server-update.sh --api-only          # پیش‌فرض: بدون rebuild
#   bash ~/Api_Vapp_Manually/devops/scripts/server-update.sh --api-only --rebuild # تغییر C# → docker build
#   bash ~/Api_Vapp_Manually/devops/scripts/server-update.sh --full
#   bash ~/Api_Vapp_Manually/devops/scripts/server-update.sh --front-only
#   bash ~/Api_Vapp_Manually/devops/scripts/server-update.sh --public-only
#   bash ~/Api_Vapp_Manually/devops/scripts/server-update.sh --api-only --set-zohal-token 'TOKEN' --verify-zohal
#   bash ~/Api_Vapp_Manually/devops/scripts/server-update.sh --pull-only
#
# هرگز خام «git pull» نزنید.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
API_DIR="$(cd "$SCRIPT_DIR/../.." && pwd)"
# shellcheck source=lib/load-server-conf.sh
source "$SCRIPT_DIR/lib/load-server-conf.sh"
# shellcheck source=lib/deploy-fail.sh
source "$SCRIPT_DIR/lib/deploy-fail.sh"
# shellcheck source=lib/deploy-progress.sh
source "$SCRIPT_DIR/lib/deploy-progress.sh"

MODE="--api-only"
SET_ZOHAL_TOKEN=""
DO_VERIFY_ZOHAL=0
DO_REBUILD=0
START=$SECONDS

usage() {
  sed -n '3,16p' "$0" | sed 's/^# \?//'
  exit "${1:-0}"
}

fail() {
  deploy_fail "$@" || true
  echo "  bash $SCRIPT_DIR/diagnose-deploy.sh" >&2
  exit 1
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --api-only|--front-only|--public-only|--fast|--full|--pull-only) MODE="$1" ;;
    --verify-zohal) DO_VERIFY_ZOHAL=1 ;;
    --rebuild|--build) DO_REBUILD=1 ;;
    --no-build) DO_REBUILD=0 ;;
    --set-zohal-token)
      shift
      SET_ZOHAL_TOKEN="${1:-}"
      [[ -n "$SET_ZOHAL_TOKEN" ]] || fail "--set-zohal-token needs a value"
      ;;
    --set-zohal-token=*)
      SET_ZOHAL_TOKEN="${1#*=}"
      [[ -n "$SET_ZOHAL_TOKEN" ]] || fail "--set-zohal-token= needs a value"
      ;;
    -h|--help) usage 0 ;;
    *) fail "unknown option: $1" "bash $0 --help" ;;
  esac
  shift
done

# --fast/--full imply rebuild (code deploy). --api-only defaults to no-build.
if [[ "$MODE" == "--fast" || "$MODE" == "--full" ]]; then
  DO_REBUILD=1
fi

deploy_log "=== server-update mode=$MODE rebuild=$DO_REBUILD ==="
deploy_log "API_DIR=$API_DIR SERVER_IP=$SERVER_IP"

deploy_log "── safe git sync (keeps docker/.env secrets uploads log backups) ──"
API_REPO_DIR="$API_DIR" API_BRANCH="${API_BRANCH:-main}" \
  bash "$SCRIPT_DIR/sync-api-repo-safe.sh" \
  || fail "API sync failed" "cd $API_DIR && git status" "bash $SCRIPT_DIR/sync-api-repo-safe.sh"

sync_other_repo() {
  local dir="$1" branch="$2" name="$3"
  if [[ ! -d "$dir/.git" ]]; then
    deploy_log "WARN: $name missing at $dir — skip"
    return 0
  fi
  deploy_log "safe sync $name → origin/$branch"
  (
    cd "$dir"
    git fetch origin "$branch"
    git checkout -B "$branch" "origin/$branch" 2>/dev/null || git checkout "$branch"
    git reset --hard "origin/$branch"
    # Keep build caches; deploy scripts refresh when needed
    git clean -fd -e node_modules -e dist -e .env -e .env.* || true
  ) || fail "$name sync failed" "cd $dir && git status"
}

if [[ "$MODE" != "--api-only" ]]; then
  sync_other_repo "${REMOTE_FRONT_REPO:-$HOME/Admin_Vapp}" "${FRONT_GIT_BRANCH:-main}" "Admin"
fi
if [[ "$MODE" == "--public-only" || "$MODE" == "--fast" || "$MODE" == "--full" || "$MODE" == "--pull-only" ]]; then
  sync_other_repo "${REMOTE_PUBLIC_REPO:-$HOME/Public_Vapp}" "${PUBLIC_GIT_BRANCH:-main}" "Public"
fi

if [[ "$MODE" == "--pull-only" ]]; then
  deploy_ok_box "pull-only done ($(_deploy_elapsed "$START"))"
  exit 0
fi

# Zohal token: فقط بنویس؛ restart را deploy-api یک‌بار انجام می‌دهد (نه دوبار)
if [[ -n "$SET_ZOHAL_TOKEN" ]]; then
  bash "$SCRIPT_DIR/ensure-zohal-token.sh" --set "$SET_ZOHAL_TOKEN"
elif [[ "${ZOHAL_API_TOKEN:-}" != "" ]]; then
  bash "$SCRIPT_DIR/ensure-zohal-token.sh" --set "$ZOHAL_API_TOKEN"
else
  bash "$SCRIPT_DIR/ensure-zohal-token.sh" --check || true
fi

export FRONT_DEPLOY_MODE="${FRONT_DEPLOY_MODE:-host}"
export PUBLIC_DEPLOY_MODE="${PUBLIC_DEPLOY_MODE:-host}"
export SERVER_IP
export API_DIR
export SKIP_GIT_PULL=1
export SKIP_BUILD=1
[[ "$DO_REBUILD" == "1" ]] && export SKIP_BUILD=0

run_api_safe() {
  local reload="${1:-0}"
  deploy_log "── API deploy SKIP_BUILD=$SKIP_BUILD ──"
  bash "$SCRIPT_DIR/ensure-dbvapp.sh" || true
  bash "$SCRIPT_DIR/repair-zohal-migration-history.sh" || true
  if ! ALLOW_SLOW_START=0 RELOAD_NGINX="$reload" SKIP_GIT_PULL=1 SKIP_BUILD="$SKIP_BUILD" \
    bash "$SCRIPT_DIR/deploy-api.sh"; then
    fail "API deploy failed" \
      "docker logs --tail 200 vapp_api_prod" \
      "SKIP_BUILD=1 bash $SCRIPT_DIR/deploy-api.sh" \
      "bash $SCRIPT_DIR/diagnose-deploy.sh"
  fi
}

run_front() {
  deploy_log "── Admin front (host static) ──"
  if [[ -d "${REMOTE_FRONT_REPO:-$HOME/Admin_Vapp}" ]]; then
    SERVER_IP="$SERVER_IP" bash "$SCRIPT_DIR/deploy-front-host.sh" \
      || fail "Admin front deploy failed" "bash $SCRIPT_DIR/deploy-front-host.sh"
  else
    deploy_log "WARN: Admin_Vapp missing — skip"
  fi
}

run_public() {
  deploy_log "── Public front (host static) ──"
  if [[ -d "${REMOTE_PUBLIC_REPO:-$HOME/Public_Vapp}" ]]; then
    SERVER_IP="$SERVER_IP" bash "$SCRIPT_DIR/deploy-public-front-host.sh" \
      || fail "Public front deploy failed" \
        "bash $SCRIPT_DIR/ensure-nginx-ok.sh" \
        "SERVER_IP=$SERVER_IP bash $SCRIPT_DIR/deploy-public-front-host.sh"
  else
    fail "Public_Vapp missing at ${REMOTE_PUBLIC_REPO:-$HOME/Public_Vapp}"
  fi
}

case "$MODE" in
  --api-only)
    run_api_safe 0
    ;;
  --front-only)
    run_front
    ;;
  --public-only)
    run_public
    ;;
  --fast)
    run_api_safe 0
    run_front
    run_public
    ;;
  --full)
    run_api_safe 1
    run_front
    run_public
    ;;
esac

if [[ "$DO_VERIFY_ZOHAL" == "1" ]]; then
  bash "$SCRIPT_DIR/verify-zohal.sh" || fail "Zohal verify failed" \
    "bash $SCRIPT_DIR/ensure-zohal-token.sh --set 'YOUR_TOKEN' --restart" \
    "Confirm IP $SERVER_IP is allowlisted at dashboard.zohal.io"
fi

# Always smoke-test so false "done" never happens again
deploy_log "── health-check ──"
if [[ "$MODE" == "--api-only" ]]; then
  HEALTH_ATTEMPTS=3 HEALTH_SLEEP=5 bash "$SCRIPT_DIR/health-check.sh" --api-only \
    || fail "API health-check failed" "bash $SCRIPT_DIR/diagnose-deploy.sh"
  # If Public static already live, keep nginx OK (cheap)
  if [[ -f /var/www/vapp-public/index.html ]]; then
    bash "$SCRIPT_DIR/ensure-nginx-ok.sh" || true
  fi
else
  HEALTH_ATTEMPTS=3 HEALTH_SLEEP=5 bash "$SCRIPT_DIR/health-check.sh" \
    || fail "health-check failed" \
      "bash $SCRIPT_DIR/ensure-nginx-ok.sh" \
      "SERVER_IP=$SERVER_IP bash $SCRIPT_DIR/deploy-public-front-host.sh" \
      "bash $SCRIPT_DIR/diagnose-deploy.sh"
fi

deploy_ok_box "server-update $MODE finished in $(_deploy_elapsed "$START")"
echo "Next time (no rebuild):  bash ~/Api_Vapp_Manually/devops/scripts/server-update.sh --api-only"
echo "After C# change:         bash ~/Api_Vapp_Manually/devops/scripts/server-update.sh --api-only --rebuild"
echo "Public only:             bash ~/Api_Vapp_Manually/devops/scripts/server-update.sh --public-only"
echo "Nginx 502 quick fix:     bash ~/Api_Vapp_Manually/devops/scripts/ensure-nginx-ok.sh"
