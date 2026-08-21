#!/usr/bin/env bash
# بازیابی دائمی API وقتی health=000 — sanitize + repair EF + rebuild + ready
#
# Usage (روی سرور):
#   bash ~/Api_Vapp_Manually/devops/scripts/recover-api-forever.sh
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
API_DIR="$(cd "$SCRIPT_DIR/../.." && pwd)"
ENV_FILE="$API_DIR/docker/.env"

echo "=== recover-api-forever $(date -Is) ==="
cd "$API_DIR"

bash "$SCRIPT_DIR/sync-api-repo-safe.sh"
bash "$SCRIPT_DIR/ensure-runtime-files.sh"

echo "── preflight ──"
ls -la "$API_DIR/secrets/firebase-service-account.json" || true
file "$API_DIR/secrets/firebase-service-account.json" || true
grep -E '^(Jwt__Secret|API_PORT_MAPPING|ZOHAL_API_TOKEN)=' "$ENV_FILE" 2>/dev/null | sed -E 's/(TOKEN|Secret)=.*/\1=***/' || true

echo "── docker status before ──"
docker ps -a --filter name=vapp_api_prod --format 'table {{.Names}}\t{{.Status}}\t{{.Ports}}' || true
docker logs --tail 40 vapp_api_prod 2>&1 | tail -40 || true

bash "$SCRIPT_DIR/ensure-dbvapp.sh" || true
bash "$SCRIPT_DIR/repair-zohal-migration-history.sh" || true

echo "── rebuild API image ──"
SKIP_GIT_PULL=1 SKIP_BUILD=0 DB_READY_ATTEMPTS=48 \
  bash "$SCRIPT_DIR/deploy-api.sh"

echo "── wait ready ──"
ready=0
for i in $(seq 1 36); do
  h="$(curl -sS -m 8 -o /dev/null -w '%{http_code}' http://127.0.0.1:8080/health 2>/dev/null || echo 000)"
  r="$(curl -sS -m 8 -o /dev/null -w '%{http_code}' http://127.0.0.1:8080/health/ready 2>/dev/null || echo 000)"
  echo "try $i/36 health=$h ready=$r"
  if [[ "$h" == "200" && "$r" == "200" ]]; then
    ready=1
    break
  fi
  sleep 5
done
[[ "$ready" == "1" ]] || {
  echo "ERROR: API not ready after recover" >&2
  docker logs --tail 80 vapp_api_prod 2>&1 | tail -80 >&2 || true
  exit 1
}

echo "── verify zohal (non-fatal) ──"
bash "$SCRIPT_DIR/verify-zohal.sh" || true

# Keep Public nginx healthy if static already deployed
if [[ -f /var/www/vapp-public/index.html ]]; then
  bash "$SCRIPT_DIR/ensure-nginx-ok.sh" || true
fi

echo "=== recover-api-forever DONE ==="
HEALTH_ATTEMPTS=2 HEALTH_SLEEP=3 bash "$SCRIPT_DIR/health-check.sh" --api-only
echo "OK: API recovered"
