#!/usr/bin/env bash
# Verify Zohal (شاهکار) config + outbound + recent inquiry outcome
#
# Usage (on server):
#   bash devops/scripts/verify-zohal.sh
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=lib/load-server-conf.sh
source "$SCRIPT_DIR/lib/load-server-conf.sh" 2>/dev/null || true

API_DIR="${API_DIR:-${REMOTE_API_REPO:-$HOME/Api_Vapp_Manually}}"
ENV_FILE="${ENV_FILE:-$API_DIR/docker/.env}"
SQL_CONTAINER="${SQL_CONTAINER:-vapp_sqlserver_prod}"
API_CONTAINER="${API_CONTAINER:-vapp_api_prod}"
SERVER_IP="${SERVER_IP:-195.24.237.132}"

echo "=== verify-zohal $(date -Is) ==="
echo "Expected allowlisted IP in Zohal dashboard: $SERVER_IP"
echo ""

echo "── .env ──"
if [[ ! -f "$ENV_FILE" ]]; then
  echo "FAIL: missing $ENV_FILE"
  exit 1
fi
tok="$(grep -E '^ZOHAL_API_TOKEN=' "$ENV_FILE" | head -1 | cut -d= -f2- || true)"
enabled="$(grep -E '^Zohal__Enabled=' "$ENV_FILE" | head -1 | cut -d= -f2- || echo true)"
base="$(grep -E '^Zohal__BaseUrl=' "$ENV_FILE" | head -1 | cut -d= -f2- || echo 'https://service.zohal.io/api/v0')"
if [[ -z "$tok" || "$tok" == "CHANGE_ME_ZOHAL_TOKEN" || "$tok" == "your-token" ]]; then
  echo "FAIL: ZOHAL_API_TOKEN empty/placeholder in $ENV_FILE"
  echo "NEXT: set token from https://dashboard.zohal.io then:"
  echo "  cd $API_DIR/docker && docker compose -f docker-compose.production.yml --env-file .env up -d --force-recreate --no-build api"
  exit 1
fi
echo "OK: ZOHAL_API_TOKEN set (len=${#tok})"
echo "Zohal__Enabled=${enabled}"
echo "Zohal__BaseUrl=${base}"
echo ""

echo "── container env ──"
docker exec "$API_CONTAINER" printenv Zohal__ApiToken Zohal__Enabled Zohal__BaseUrl 2>/dev/null \
  | sed -E 's/^(Zohal__ApiToken=).*/\1***/' || echo "WARN: cannot read container env"
echo ""

echo "── outbound to Zohal ──"
code="$(curl -sS -m 20 -o /dev/null -w '%{http_code}' "${base%/}/" 2>/dev/null || echo 000)"
echo "HTTP $code from host → ${base}"
if [[ "$code" == "000" ]]; then
  echo "FAIL: cannot reach Zohal from server (firewall/DNS?)"
  exit 1
fi
echo "OK: Zohal reachable (any HTTP code means TCP/TLS works)"
echo ""

echo "── recent ZohalInquiryLogs ──"
if [[ -f "$ENV_FILE" ]] && docker inspect "$SQL_CONTAINER" >/dev/null 2>&1; then
  SA="$(grep -E '^SA_PASSWORD=' "$ENV_FILE" | head -1 | cut -d= -f2-)"
  SQLCMD="$(docker exec "$SQL_CONTAINER" sh -c 'command -v sqlcmd || ls /opt/mssql-tools18/bin/sqlcmd /opt/mssql-tools/bin/sqlcmd 2>/dev/null | head -1' 2>/dev/null || true)"
  if [[ -n "$SA" && -n "$SQLCMD" ]]; then
    docker exec "$SQL_CONTAINER" "$SQLCMD" -S localhost -U sa -P "$SA" -C -d DbVapp -Q "
SET NOCOUNT ON;
IF OBJECT_ID(N'dbo.ZohalInquiryLogs') IS NULL
  PRINT 'MISS: ZohalInquiryLogs table — run migrate / ensure-dbvapp';
ELSE
  SELECT TOP 5 Id, OutcomeStatus, HttpStatusCode, ProviderErrorCode,
         LEFT(ISNULL(ProviderMessage,N''),100) AS Msg, CreatedAt
  FROM dbo.ZohalInquiryLogs ORDER BY Id DESC;" 2>&1 | tail -40 || true
  else
    echo "WARN: cannot query SQL"
  fi
else
  echo "WARN: SQL unavailable"
fi
echo ""

echo "── recent API Shahkar lines ──"
docker logs --tail 250 "$API_CONTAINER" 2>&1 | grep -iE 'Shahkar|Zohal|missing_api|IpNot|Insufficient|ProviderAuth' | tail -20 || echo "(no matching lines)"
echo ""

echo "NEXT if register still 503:"
echo "  1) Confirm $SERVER_IP is in Zohal IP allowlist"
echo "  2) Charge Zohal wallet if InsufficientBalance"
echo "  3) Retry register from app, then re-run this script"
echo "OK: verify-zohal finished"
