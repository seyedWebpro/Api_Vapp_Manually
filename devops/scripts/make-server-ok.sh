#!/usr/bin/env bash
# ★ یک‌بار برای همیشه: sync + repair EF + rebuild API + wait ready + verify Zohal
#
# Usage (روی سرور):
#   bash ~/Api_Vapp_Manually/devops/scripts/make-server-ok.sh
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
API_DIR="$(cd "$SCRIPT_DIR/../.." && pwd)"
ENV_FILE="$API_DIR/docker/.env"
COMPOSE="$API_DIR/docker/docker-compose.production.yml"
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

log "1/6 safe git sync"
bash "$SCRIPT_DIR/sync-api-repo-safe.sh"
bash "$SCRIPT_DIR/ensure-runtime-files.sh"
ok "HEAD=$(git rev-parse --short HEAD) $(git log -1 --pretty=%s)"

log "2/6 ensure DbVapp + repair Zohal EF history"
bash "$SCRIPT_DIR/ensure-dbvapp.sh"
bash "$SCRIPT_DIR/repair-zohal-migration-history.sh"
ok "DB/history ready"

log "3/6 docker rebuild API (latest Program.cs + idempotent migrations)"
SKIP_GIT_PULL=1 SKIP_BUILD=0 DB_READY_ATTEMPTS=60 \
  bash "$SCRIPT_DIR/deploy-api.sh"
ok "API image rebuilt + restarted"

log "4/6 wait until /health/ready == 200"
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

log "5/6 verify Zohal config"
bash "$SCRIPT_DIR/verify-zohal.sh" || log "WARN: verify-zohal reported issues (token/IP/wallet)"

log "5b/6 deploy Public front (form/wheel/card/book)"
if [[ -d "${PUBLIC_DIR:-$HOME/Public_Vapp}" ]]; then
  SERVER_IP="$SERVER_IP" bash "$SCRIPT_DIR/deploy-public-front-host.sh" \
    || die "Public front deploy failed"
else
  log "WARN: Public_Vapp missing at ~/Public_Vapp — skip"
fi

log "6/6 smoke"
bash "$SCRIPT_DIR/health-check.sh" || die "health-check failed after full update"

elapsed=$((SECONDS - START))
echo ""
echo "════════════════════════════════════════════════════"
echo "  SERVER OK  |  commit $(git rev-parse --short HEAD)  |  ${elapsed}s"
echo "════════════════════════════════════════════════════"
echo "  Health:  http://127.0.0.1:8080/health"
echo "  Ready:   http://127.0.0.1:8080/health/ready"
echo "  AppVer:  http://127.0.0.1:8080/api/AppVersion/check?platform=android&currentVersion=1.0.0"
echo "  Public:  http://195.24.237.132/"
echo ""
echo "  Register test from Flutter should work if Zohal IP+token+wallet OK."
echo "  Next routine updates (no rebuild):"
echo "    bash ~/Api_Vapp_Manually/devops/scripts/server-update.sh --api-only"
echo "  After C# change:"
echo "    bash ~/Api_Vapp_Manually/devops/scripts/server-update.sh --api-only --rebuild"
echo ""
