#!/usr/bin/env bash
# ★ آپدیت یک‌جای Vapp روی سرور — API + Admin + Public + DB migrate
#
# Usage (روی سرور):
#   bash ~/Api_Vapp_Manually/vapp-iran-update.sh --test          # شبکه / registry
#   bash ~/Api_Vapp_Manually/vapp-iran-update.sh --diagnose      # تشخیص کامل + دلیل خطا
#   bash ~/Api_Vapp_Manually/vapp-iran-update.sh --pull-only
#   bash ~/Api_Vapp_Manually/vapp-iran-update.sh --api-only      # بک‌اند + Migrate/wait
#   bash ~/Api_Vapp_Manually/vapp-iran-update.sh --front-only    # Admin (host npm)
#   bash ~/Api_Vapp_Manually/vapp-iran-update.sh --public-only   # Public form/wheel
#   bash ~/Api_Vapp_Manually/vapp-iran-update.sh --fast          # API + Admin + Public
#   bash ~/Api_Vapp_Manually/vapp-iran-update.sh --full          # مثل --fast + nginx reload در API
#   bash ~/Api_Vapp_Manually/vapp-iran-update.sh --host          # همان --front-only
#
# پیش‌فرض: بدون میرور ایران‌سرور. این دیتاسنتر به Docker Hub / npm / MCR وصل است.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DEVOPS="$ROOT/devops/scripts"
# shellcheck source=devops/scripts/lib/load-server-conf.sh
source "$DEVOPS/lib/load-server-conf.sh"
# shellcheck source=devops/scripts/lib/deploy-fail.sh
source "$DEVOPS/lib/deploy-fail.sh"
# shellcheck source=devops/scripts/lib/deploy-progress.sh
source "$DEVOPS/lib/deploy-progress.sh"

API_DIR="${API_DIR:-$ROOT}"
FRONT_DIR="${FRONT_DIR:-$REMOTE_FRONT_REPO}"
PUBLIC_DIR="${PUBLIC_DIR:-$REMOTE_PUBLIC_REPO}"
START=$SECONDS

usage() {
  sed -n '3,18p' "$0" | sed 's/^# \?//'
  exit "${1:-0}"
}

fail() {
  deploy_fail "$@" || true
  echo "Diagnose now:"
  echo "  bash $DEVOPS/diagnose-deploy.sh"
  exit 1
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
    *) fail "unknown option: $1" "bash $0 --help" ;;
  esac
  shift
done

case "$MODE" in
  -h|--help) usage 0 ;;
  --host) DEPLOY_STYLE=host; MODE="--front-only" ;;
  --docker) DEPLOY_STYLE=docker; MODE="--front-only" ;;
  --mirror)
    echo "WARN: IranServer mirrors فقط برای VPS قدیمی."
    exec sudo bash "$DEVOPS/apply-build-mirrors-iranserver.sh"
    ;;
  --test)
    exec bash "$DEVOPS/server-net-check.sh"
    ;;
  --diagnose)
    exec bash "$DEVOPS/diagnose-deploy.sh"
    ;;
  --pull-only|--front-only|--public-only|--fast|--api-only|--full) ;;
  *)
    fail "unknown mode: $MODE" "bash $0 --help"
    ;;
esac

deploy_log "=== vapp-iran-update mode=$MODE style=$DEPLOY_STYLE mirrors=$APPLY_MIRROR ==="
deploy_log "SERVER_IP=$SERVER_IP API_DIR=$API_DIR"

if [[ "$APPLY_MIRROR" == "1" ]]; then
  deploy_log "apply IranServer mirrors (explicit --mirror)"
  if [[ "$(id -u)" -eq 0 ]]; then
    bash "$DEVOPS/apply-build-mirrors-iranserver.sh"
  else
    sudo bash "$DEVOPS/apply-build-mirrors-iranserver.sh"
  fi
else
  deploy_log "mirrors: OFF — native registries"
  if [[ -f /etc/docker/daemon.json ]] && grep -q 'docker.iranserver.com' /etc/docker/daemon.json 2>/dev/null; then
    deploy_log "leftover IranServer docker mirror — clearing"
    if [[ "$(id -u)" -eq 0 ]]; then
      bash "$DEVOPS/clear-iranserver-mirrors.sh"
    else
      sudo bash "$DEVOPS/clear-iranserver-mirrors.sh"
    fi
  fi
fi

git_pull_repo() {
  local dir="$1" branch="$2" name="$3"
  if [[ ! -d "$dir/.git" ]]; then
    deploy_log "WARN: $name missing git at $dir — skip pull"
    return 0
  fi
  deploy_log "git pull $name ($branch) @ $dir"
  if ! (cd "$dir" && git pull origin "$branch"); then
    fail "git pull failed for $name" \
      "cd $dir && git status" \
      "bash $DEVOPS/sync-api-repo-safe.sh   # فقط برای API" \
      "یا: cd $dir && git fetch origin && git reset --hard origin/$branch"
  fi
}

if [[ "$MODE" == "--pull-only" ]]; then
  git_pull_repo "$API_DIR" "${API_BRANCH:-main}" "API"
  git_pull_repo "$FRONT_DIR" "${FRONT_GIT_BRANCH:-main}" "Admin"
  git_pull_repo "$PUBLIC_DIR" "${PUBLIC_GIT_BRANCH:-main}" "Public"
  deploy_ok_box "git pull done ($(_deploy_elapsed "$START"))"
  exit 0
fi

# همیشه قبل از build/deploy، آخرین کد را بکش
git_pull_repo "$API_DIR" "${API_BRANCH:-main}" "API"
[[ "$MODE" != "--api-only" ]] && git_pull_repo "$FRONT_DIR" "${FRONT_GIT_BRANCH:-main}" "Admin"
[[ "$MODE" == "--public-only" || "$MODE" == "--fast" || "$MODE" == "--full" ]] && \
  git_pull_repo "$PUBLIC_DIR" "${PUBLIC_GIT_BRANCH:-main}" "Public"

export FRONT_DEPLOY_MODE="${DEPLOY_STYLE:-host}"
export PUBLIC_DEPLOY_MODE="${PUBLIC_DEPLOY_MODE:-host}"
export SERVER_IP
export API_DIR FRONT_DIR PUBLIC_DIR

run_api() {
  local reload="${1:-0}" slow="${2:-0}"
  deploy_log "── API: ensure DbVapp + deploy + wait migrate ──"
  bash "$DEVOPS/ensure-dbvapp.sh" || deploy_log "WARN: ensure-dbvapp non-fatal — Program.cs will retry"
  if ! ALLOW_SLOW_START="$slow" RELOAD_NGINX="$reload" bash "$DEVOPS/deploy-api.sh"; then
    fail "API deploy / Migrate failed" \
      "bash $DEVOPS/ensure-dbvapp.sh --restart-api --wait" \
      "bash $DEVOPS/diagnose-deploy.sh" \
      "docker logs --tail 100 vapp_api_prod"
  fi
}

run_admin() {
  deploy_log "── Admin front (mode=$FRONT_DEPLOY_MODE) ──"
  if [[ "$FRONT_DEPLOY_MODE" == "host" ]]; then
    if ! SERVER_IP="$SERVER_IP" bash "$DEVOPS/deploy-front-host.sh"; then
      fail "Admin front deploy failed" \
        "SKIP_NPM_CI=1 bash $DEVOPS/deploy-front-host.sh   # اگر node_modules سالم است" \
        "bash $DEVOPS/diagnose-deploy.sh"
    fi
  else
    if ! FRONT_DEPLOY_MODE=docker bash "$DEVOPS/deploy-front.sh" --foreground; then
      fail "Admin Docker front failed" "FRONT_DEPLOY_MODE=host bash $0 --front-only"
    fi
  fi
}

run_public() {
  deploy_log "── Public front (form/wheel/card/book) ──"
  if [[ ! -d "$PUBLIC_DIR" ]]; then
    fail "Public repo missing: $PUBLIC_DIR" \
      "git clone git@github.com:seyedWebpro/PublicWeb_Vapp.git $PUBLIC_DIR" \
      "سپس دوباره: bash $0 --public-only"
  fi
  if ! SERVER_IP="$SERVER_IP" bash "$DEVOPS/deploy-public-front-host.sh"; then
    fail "Public front deploy failed" \
      "ls -la $PUBLIC_DIR" \
      "SKIP_NPM_CI=1 bash $DEVOPS/deploy-public-front-host.sh" \
      "bash $DEVOPS/diagnose-deploy.sh"
  fi
}

case "$MODE" in
  --api-only) run_api 0 0 ;;
  --front-only) run_admin ;;
  --public-only) run_public ;;
  --fast)
    run_api 0 1
    run_admin
    run_public
    ;;
  --full)
    run_api 1 1
    run_admin
    run_public
    ;;
esac

deploy_log "── final verify ──"
if ! HEALTH_ATTEMPTS=3 HEALTH_SLEEP=8 bash "$DEVOPS/health-check.sh"; then
  fail "health-check failed after update" \
    "bash $DEVOPS/diagnose-deploy.sh" \
    "جزئیات HTTP بالا را ببینید (APPVER / FORM / ADMIN)"
fi

deploy_ok_box "vapp-iran-update $MODE finished in $(_deploy_elapsed "$START")"
echo "Admin:  http://${SERVER_IP}/"
echo "AppVer: http://${SERVER_IP}/api/AppVersion/check?platform=android&currentVersion=1.0.0"
echo "Form:   http://${SERVER_IP}/form/"
exit 0
