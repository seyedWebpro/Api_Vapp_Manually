#!/usr/bin/env bash
# ★ آپدیت Vapp روی خود سرور (git pull + build) — الگوی CaspianEdu روی همین دیتاسنتر
#
# Usage (روی سرور):
#   bash ~/Api_Vapp_Manually/vapp-iran-update.sh --test
#   bash ~/Api_Vapp_Manually/vapp-iran-update.sh --pull-only
#   bash ~/Api_Vapp_Manually/vapp-iran-update.sh --api-only
#   bash ~/Api_Vapp_Manually/vapp-iran-update.sh --front-only
#   bash ~/Api_Vapp_Manually/vapp-iran-update.sh --fast
#   bash ~/Api_Vapp_Manually/vapp-iran-update.sh --full
#   bash ~/Api_Vapp_Manually/vapp-iran-update.sh --host        # Admin روی host npm
#   bash ~/Api_Vapp_Manually/vapp-iran-update.sh --mirror      # فقط اگر --test fail شد
#
# میرور ایران‌سرور پیش‌فرض خاموش است. این دیتاسنتر به Docker Hub / npm / MCR وصل است.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DEVOPS="$ROOT/devops/scripts"
# shellcheck source=devops/scripts/lib/load-server-conf.sh
source "$DEVOPS/lib/load-server-conf.sh"

API_DIR="${API_DIR:-$ROOT}"
FRONT_DIR="${FRONT_DIR:-$REMOTE_FRONT_REPO}"

usage() {
  sed -n '3,16p' "$0" | sed 's/^# \?//'
  exit "${1:-0}"
}

MODE="${1:---fast}"
shift || true
APPLY_MIRROR=0
DEPLOY_STYLE="${DEPLOY_STYLE:-host}"

while [[ $# -gt 0 ]]; do
  case "$1" in
    --mirror) APPLY_MIRROR=1 ;;
    --host) DEPLOY_STYLE=host ;;
    --docker) DEPLOY_STYLE=docker ;;
    -h|--help) usage 0 ;;
    *) echo "ERROR: unknown option: $1" >&2; usage 1 ;;
  esac
  shift
done

case "$MODE" in
  -h|--help) usage 0 ;;
  --host) DEPLOY_STYLE=host; MODE="--front-only" ;;
  --docker) DEPLOY_STYLE=docker; MODE="--front-only" ;;
  --mirror)
    echo "WARN: IranServer mirrors are for the OLD provider only."
    echo "      This datacenter should use native registries."
    exec sudo bash "$DEVOPS/apply-build-mirrors-iranserver.sh"
    ;;
  --test)
    exec bash "$DEVOPS/server-net-check.sh"
    ;;
  --pull-only|--front-only|--fast|--api-only|--full) ;;
  *)
    echo "ERROR: unknown mode: $MODE" >&2
    usage 1
    ;;
esac

echo "[$(date -Is)] === vapp-iran-update mode=$MODE mirrors=$APPLY_MIRROR ==="
echo "[$(date -Is)] SERVER_IP=$SERVER_IP"

if [[ "$APPLY_MIRROR" == "1" ]]; then
  echo "[$(date -Is)] apply IranServer mirrors (explicit --mirror)"
  if [[ "$(id -u)" -eq 0 ]]; then
    bash "$DEVOPS/apply-build-mirrors-iranserver.sh"
  else
    sudo bash "$DEVOPS/apply-build-mirrors-iranserver.sh"
  fi
else
  echo "[$(date -Is)] mirrors: OFF — native registries"
  if [[ -f /etc/docker/daemon.json ]] && grep -q 'docker.iranserver.com' /etc/docker/daemon.json 2>/dev/null; then
    echo "[$(date -Is)] leftover IranServer docker mirror found — clearing"
    if [[ "$(id -u)" -eq 0 ]]; then
      bash "$DEVOPS/clear-iranserver-mirrors.sh"
    else
      sudo bash "$DEVOPS/clear-iranserver-mirrors.sh"
    fi
  fi
fi

if [[ "$MODE" == "--pull-only" ]]; then
  cd "$API_DIR" && git pull origin "${API_BRANCH:-main}"
  [[ -d "$FRONT_DIR/.git" ]] && cd "$FRONT_DIR" && git pull origin "${FRONT_GIT_BRANCH:-main}"
  echo "OK: git pull done"
  exit 0
fi

cd "$API_DIR" && git pull origin "${API_BRANCH:-main}"
[[ -d "$FRONT_DIR/.git" ]] && cd "$FRONT_DIR" && git pull origin "${FRONT_GIT_BRANCH:-main}"

unset FRONT_DEPLOY_MODE
export FRONT_DEPLOY_MODE="${DEPLOY_STYLE:-host}"
export SERVER_IP
if [[ "$FRONT_DEPLOY_MODE" == "docker" ]]; then
  FRONT_STATIC_ROOT= bash "$DEVOPS/apply-nginx.sh" 2>/dev/null || true
fi
exec bash "$DEVOPS/deploy-server-visible.sh" "$MODE"
