#!/usr/bin/env bash
# ★ یک‌بار برای همیشه: sync + repair EF + rebuild API + Public + nginx + health
#
# Usage (روی سرور):
#   bash ~/Api_Vapp_Manually/devops/scripts/make-server-ok.sh
# Routine later (no rebuild):
#   bash ~/Api_Vapp_Manually/devops/scripts/server-update.sh --api-only
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
API_DIR="$(cd "$SCRIPT_DIR/../.." && pwd)"
# shellcheck source=lib/load-server-conf.sh
source "$SCRIPT_DIR/lib/load-server-conf.sh"
SERVER_IP="${SERVER_IP:-195.24.237.132}"
START=$SECONDS

log() { echo "[$(date -Is)] $*"; }
ok() { echo "✓ $*"; }
die() { echo "ERROR: $*" >&2; exit 1; }

http_code() {
  local code
  code="$(curl -sS -m 10 -o /tmp/vapp-http-body.json -w '%{http_code}' "$1" 2>/dev/null)" || code="000"
  [[ "$code" =~ ^[0-9]{3}$ ]] || code="000"
  printf '%s' "$code"
}

cd "$API_DIR"
log "=== make-server-ok START ==="

log "1/7 safe git sync"
bash "$SCRIPT_DIR/sync-api-repo-safe.sh"
bash "$SCRIPT_DIR/ensure-runtime-files.sh"
ok "HEAD=$(git rev-parse --short HEAD) $(git log -1 --pretty=%s)"

log "2/7 ensure DbVapp + repair Zohal EF history"
bash "$SCRIPT_DIR/ensure-dbvapp.sh"
bash "$SCRIPT_DIR/repair-zohal-migration-history.sh"
ok "DB/history ready"

log "3/7 docker rebuild API"
SKIP_GIT_PULL=1 SKIP_BUILD=0 DB_READY_ATTEMPTS=60 \
  bash "$SCRIPT_DIR/deploy-api.sh"
ok "API image rebuilt + restarted"

log "4/7 wait until /health/ready == 200"
ready=0
for i in $(seq 1 60); do
  h="$(http_code http://127.0.0.1:8080/health)"
  r="$(http_code http://127.0.0.1:8080/health/ready)"
  a="$(http_code 'http://127.0.0.1:8080/api/AppVersion/check?platform=android&currentVersion=1.0.0')"
  log "try $i/60 health=$h ready=$r appver=$a"
  if [[ "$h" == "200" && "$r" == "200" && "$a" == "200" ]]; then
    ready=1
    break
  fi
  sleep 5
done
[[ "$ready" == "1" ]] || {
  echo "── docker logs ──" >&2
  docker logs --tail 80 vapp_api_prod 2>&1 | tail -80 >&2 || true
  die "API not fully ready (health/ready/appver)"
}
ok "health + ready + AppVersion = 200"
cat /tmp/vapp-http-body.json 2>/dev/null | head -c 200; echo

log "5/7 verify Zohal config"
bash "$SCRIPT_DIR/verify-zohal.sh" || log "WARN: verify-zohal reported issues (token/IP/wallet)"

log "6/7 Admin + Public fronts + nginx"
if [[ -d "${REMOTE_FRONT_REPO:-$HOME/Admin_Vapp}" ]]; then
  SERVER_IP="$SERVER_IP" bash "$SCRIPT_DIR/deploy-front-host.sh" \
    || die "Admin front deploy failed"
else
  log "WARN: Admin_Vapp missing — skip admin"
fi
if [[ -d "${REMOTE_PUBLIC_REPO:-$HOME/Public_Vapp}" ]] || [[ -d "${PUBLIC_DIR:-$HOME/Public_Vapp}" ]]; then
  SERVER_IP="$SERVER_IP" bash "$SCRIPT_DIR/deploy-public-front-host.sh" \
    || die "Public front deploy failed"
else
  die "Public_Vapp missing at ~/Public_Vapp"
fi
bash "$SCRIPT_DIR/ensure-nginx-ok.sh" || die "nginx Public verify failed"

log "7/7 full smoke"
HEALTH_ATTEMPTS=3 HEALTH_SLEEP=5 bash "$SCRIPT_DIR/health-check.sh" \
  || die "health-check failed after full update"

elapsed=$((SECONDS - START))
echo ""
echo "════════════════════════════════════════════════════"
echo "  SERVER OK  |  commit $(git rev-parse --short HEAD)  |  ${elapsed}s"
echo "════════════════════════════════════════════════════"
echo "  Health:  http://127.0.0.1:8080/health"
echo "  Ready:   http://127.0.0.1:8080/health/ready"
echo "  AppVer:  http://127.0.0.1:8080/api/AppVersion/check?platform=android&currentVersion=1.0.0"
echo "  Public:  http://${SERVER_IP}/form/{slug}"
echo ""
echo "  Routine (no rebuild):  bash ~/Api_Vapp_Manually/devops/scripts/server-update.sh --api-only"
echo "  After C#:              bash ~/Api_Vapp_Manually/devops/scripts/server-update.sh --api-only --rebuild"
echo "  Public only:           bash ~/Api_Vapp_Manually/devops/scripts/server-update.sh --public-only"
echo "  Nginx quick fix:       bash ~/Api_Vapp_Manually/devops/scripts/ensure-nginx-ok.sh"
echo ""
