#!/usr/bin/env bash
# ★ یک‌خطی امن آپدیت سرور — بدون خطای «local changes would be overwritten»
#
# Usage (روی سرور):
#   bash ~/Api_Vapp_Manually/devops/scripts/server-update.sh
#   bash ~/Api_Vapp_Manually/devops/scripts/server-update.sh --api-only
#   bash ~/Api_Vapp_Manually/devops/scripts/server-update.sh --full
#   bash ~/Api_Vapp_Manually/devops/scripts/server-update.sh --front-only
#   bash ~/Api_Vapp_Manually/devops/scripts/server-update.sh --public-only
#   bash ~/Api_Vapp_Manually/devops/scripts/server-update.sh --api-only --set-zohal-token 'TOKEN'
#   bash ~/Api_Vapp_Manually/devops/scripts/server-update.sh --pull-only
#   bash ~/Api_Vapp_Manually/devops/scripts/server-update.sh --verify-zohal
#
# هرگز خام «git pull» نزنید — این اسکریپت با sync-api-repo-safe تغییرات محلی
# Program.cs و غیره را دور می‌اندازد و docker/.env + secrets + uploads را نگه می‌دارد.
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
START=$SECONDS

usage() {
  sed -n '3,18p' "$0" | sed 's/^# \?//'
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

deploy_log "=== server-update mode=$MODE ==="
deploy_log "API_DIR=$API_DIR SERVER_IP=$SERVER_IP"

# 1) Safe sync — never fail on dirty Program.cs
deploy_log "── safe git sync (keeps docker/.env secrets uploads log) ──"
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
    git reset --hard "origin/$branch"
    git clean -fd
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

# 2) Optional Zohal token write
if [[ -n "$SET_ZOHAL_TOKEN" ]]; then
  bash "$SCRIPT_DIR/ensure-zohal-token.sh" --set "$SET_ZOHAL_TOKEN" --restart
elif [[ "${ZOHAL_API_TOKEN:-}" != "" ]]; then
  bash "$SCRIPT_DIR/ensure-zohal-token.sh" --set "$ZOHAL_API_TOKEN" --restart
else
  bash "$SCRIPT_DIR/ensure-zohal-token.sh" --check || true
fi

# 3) Deploy via stable orchestrator (already synced — skip re-pull conflicts)
export FRONT_DEPLOY_MODE="${FRONT_DEPLOY_MODE:-host}"
export PUBLIC_DEPLOY_MODE="${PUBLIC_DEPLOY_MODE:-host}"
export SERVER_IP
export API_DIR
export SKIP_GIT_PULL=1

case "$MODE" in
  --api-only)
    bash "$API_DIR/vapp-iran-update.sh" --api-only
    ;;
  --front-only)
    bash "$API_DIR/vapp-iran-update.sh" --front-only
    ;;
  --public-only)
    bash "$API_DIR/vapp-iran-update.sh" --public-only
    ;;
  --fast)
    bash "$API_DIR/vapp-iran-update.sh" --fast
    ;;
  --full)
    bash "$API_DIR/vapp-iran-update.sh" --full
    ;;
esac

if [[ "$DO_VERIFY_ZOHAL" == "1" ]]; then
  bash "$SCRIPT_DIR/verify-zohal.sh" || fail "Zohal verify failed" \
    "bash $SCRIPT_DIR/ensure-zohal-token.sh --set 'YOUR_TOKEN' --restart" \
    "Confirm IP $SERVER_IP is allowlisted at dashboard.zohal.io"
fi

deploy_ok_box "server-update $MODE finished in $(_deploy_elapsed "$START")"
echo "One-liner next time:"
echo "  bash ~/Api_Vapp_Manually/devops/scripts/server-update.sh --api-only"
