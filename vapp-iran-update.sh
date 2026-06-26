#!/usr/bin/env bash
# ★ آپدیت سرور ایران Vapp — یک دستور (الگو: microless iran-update.sh)
#
# Usage (روی سرور):
#   bash ~/Api_Vapp_Manually/vapp-iran-update.sh              # Admin فرانت (host + mirror)
#   bash ~/Api_Vapp_Manually/vapp-iran-update.sh --fast       # API + Admin
#   bash ~/Api_Vapp_Manually/vapp-iran-update.sh --api-only   # فقط API
#   bash ~/Api_Vapp_Manually/vapp-iran-update.sh --test       # تست mirrorها
#   bash ~/Api_Vapp_Manually/vapp-iran-update.sh --mirror       # فقط تنظیم mirror
#
# mirror: https://mirror.iranserver.com/
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DEVOPS="$ROOT/devops/scripts"
API_DIR="${API_DIR:-$ROOT}"
FRONT_DIR="${FRONT_DIR:-$HOME/Admin_Vapp}"

usage() {
  sed -n '3,12p' "$0" | sed 's/^# \?//'
  exit "${1:-0}"
}

MODE="${1:---front-only}"

case "$MODE" in
  -h|--help) usage 0 ;;
  --mirror)
    exec sudo bash "$DEVOPS/apply-build-mirrors-iranserver.sh"
    ;;
  --test)
    bash "$DEVOPS/apply-build-mirrors-iranserver.sh" 2>/dev/null || sudo bash "$DEVOPS/apply-build-mirrors-iranserver.sh"
    exec bash "$DEVOPS/test-iran-build-prereqs.sh"
    ;;
  --pull-only)
    cd "$API_DIR" && git pull origin "${API_BRANCH:-main}"
    [[ -d "$FRONT_DIR/.git" ]] && cd "$FRONT_DIR" && git pull origin "${FRONT_BRANCH:-main}"
    echo "OK: git pull done"
    ;;
  --front-only|--fast|--api-only|--full)
    if [[ "$(id -u)" -eq 0 ]]; then
      bash "$DEVOPS/apply-build-mirrors-iranserver.sh"
    else
      sudo bash "$DEVOPS/apply-build-mirrors-iranserver.sh"
    fi
    cd "$API_DIR" && git pull origin "${API_BRANCH:-main}"
    [[ -d "$FRONT_DIR/.git" ]] && cd "$FRONT_DIR" && git pull origin "${FRONT_BRANCH:-main}"
    unset FRONT_DEPLOY_MODE
    export FRONT_DEPLOY_MODE=host
    exec bash "$DEVOPS/deploy-server-visible.sh" "$MODE"
    ;;
  *)
    echo "ERROR: unknown mode: $MODE" >&2
    usage 1
    ;;
esac
