#!/usr/bin/env bash
# بازیابی دائمی API وقتی health=000 — sanitize + rebuild با فیکس listen-first
#
# Usage (روی سرور):
#   bash ~/Api_Vapp_Manually/devops/scripts/recover-api-forever.sh
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
API_DIR="$(cd "$SCRIPT_DIR/../.." && pwd)"
ENV_FILE="$API_DIR/docker/.env"
COMPOSE="$API_DIR/docker/docker-compose.production.yml"

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

echo "── rebuild API image (listen-first Program.cs) ──"
cd "$API_DIR"
SKIP_GIT_PULL=1 SKIP_BUILD=0 DB_READY_ATTEMPTS=48 \
  bash "$SCRIPT_DIR/deploy-api.sh"

echo "── verify zohal (non-fatal) ──"
bash "$SCRIPT_DIR/verify-zohal.sh" || true

echo "=== recover-api-forever DONE ==="
curl -sS -m 5 http://127.0.0.1:8080/health || true
echo
curl -sS -m 5 http://127.0.0.1:8080/health/ready || true
echo
