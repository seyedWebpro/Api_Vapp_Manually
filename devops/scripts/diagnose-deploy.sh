#!/usr/bin/env bash
# تشخیص یک‌جا: API / DB / Admin / Public / Docker — با دلیل و دستور بعدی
#
# Usage (روی سرور):
#   bash devops/scripts/diagnose-deploy.sh
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=lib/load-server-conf.sh
source "$SCRIPT_DIR/lib/load-server-conf.sh" 2>/dev/null || true
# shellcheck source=lib/deploy-fail.sh
source "$SCRIPT_DIR/lib/deploy-fail.sh"

API_DIR="${API_DIR:-${REMOTE_API_REPO:-$HOME/Api_Vapp_Manually}}"
ENV_FILE="${ENV_FILE:-$API_DIR/docker/.env}"
SQL_CONTAINER="${SQL_CONTAINER:-vapp_sqlserver_prod}"
API_CONTAINER="${API_CONTAINER:-vapp_api_prod}"
DB_NAME="${DB_NAME:-DbVapp}"
SERVER_IP="${SERVER_IP:-195.24.237.132}"

code() { curl -sS -m 12 -o /dev/null -w '%{http_code}' "$1" 2>/dev/null || echo 000; }

echo "=== Vapp diagnose $(date -Is) ==="
echo "Host: $(hostname) | SERVER_IP=$SERVER_IP"
echo ""

echo "── Docker ──"
if command -v docker >/dev/null 2>&1; then
  docker ps --format 'table {{.Names}}\t{{.Status}}\t{{.Ports}}' 2>/dev/null | head -20 || echo "(docker ps failed)"
  if ! docker compose version >/dev/null 2>&1; then
    echo "FAIL: 'docker compose' (v2) missing — install docker-compose-plugin from Docker official repo"
  else
    echo "OK: $(docker compose version 2>/dev/null | head -1)"
  fi
else
  echo "FAIL: docker not installed"
fi
echo ""

echo "── HTTP ──"
H="$(code http://127.0.0.1:8080/health)"
A="$(code 'http://127.0.0.1:8080/api/AppVersion/check?platform=android&currentVersion=1.0.0')"
S="$(code http://127.0.0.1:8080/swagger/index.html)"
N="$(code http://127.0.0.1/)"
F="$(code http://127.0.0.1/form/)"
W="$(code http://127.0.0.1/wheel/)"
C="$(code http://127.0.0.1/card/)"
B="$(code http://127.0.0.1/book/)"
printf 'health=%s appver=%s swagger=%s nginx_root=%s form=%s wheel=%s card=%s book=%s\n' "$H" "$A" "$S" "$N" "$F" "$W" "$C" "$B"
echo ""

echo "── Static files ──"
[[ -f /var/www/vapp-admin/index.html ]] && echo "OK: /var/www/vapp-admin/index.html" || echo "MISS: Admin static — bash devops/scripts/deploy-front-host.sh"
[[ -f /var/www/vapp-public/index.html ]] && echo "OK: /var/www/vapp-public/index.html" || echo "MISS: Public static — bash devops/scripts/deploy-public-front-host.sh"
echo ""

echo "── SQL / DbVapp ──"
if docker inspect "$SQL_CONTAINER" >/dev/null 2>&1 && [[ -f "$ENV_FILE" ]]; then
  SA="$(grep -E '^SA_PASSWORD=' "$ENV_FILE" | head -1 | cut -d= -f2-)"
  SQLCMD="$(docker exec "$SQL_CONTAINER" sh -c 'command -v sqlcmd || ls /opt/mssql-tools/bin/sqlcmd /opt/mssql-tools18/bin/sqlcmd 2>/dev/null | head -1' 2>/dev/null || true)"
  if [[ -n "$SA" && -n "$SQLCMD" ]]; then
    docker exec "$SQL_CONTAINER" "$SQLCMD" -S localhost -U sa -P "$SA" -C -Q \
      "SET NOCOUNT ON; SELECT name, state_desc FROM sys.databases WHERE name IN (N'master', N'$DB_NAME') ORDER BY name;" 2>&1 | tail -20 || true
    docker exec "$SQL_CONTAINER" "$SQLCMD" -S localhost -U sa -P "$SA" -C -d "$DB_NAME" -Q \
      "SET NOCOUNT ON;
       IF OBJECT_ID(N'dbo.__EFMigrationsHistory') IS NULL PRINT 'MISS: __EFMigrationsHistory';
       ELSE SELECT COUNT(*) AS ef_migrations FROM dbo.__EFMigrationsHistory;
       IF OBJECT_ID(N'dbo.AppVersionPolicies') IS NULL PRINT 'MISS: AppVersionPolicies';
       ELSE SELECT COUNT(*) AS appversion_rows FROM dbo.AppVersionPolicies;" 2>&1 | tail -30 || true
  else
    echo "WARN: cannot run sqlcmd (SA or sqlcmd missing)"
  fi
else
  echo "WARN: SQL container or .env missing"
fi
echo ""

echo "── Recent migrate / AppVersion errors (log file) ──"
LOGF="$API_DIR/log/log-$(date +%Y%m%d).txt"
if [[ -f "$LOGF" ]]; then
  grep -E 'Migration completed|Pending migrations|PendingModelChanges|Cannot open database|Database .* ensured|error occurred while migrating|خطا در چک آپدیت' "$LOGF" | tail -25 || echo "(no matching lines today)"
else
  echo "MISS: $LOGF"
  docker logs --tail 80 "$API_CONTAINER" 2>&1 | grep -iE 'Migration|Cannot open|PendingModel|AppVersion|ensured' | tail -25 || true
fi
echo ""

echo "── Verdict / NEXT ──"
problems=0
if [[ "$H" != "200" ]]; then
  problems=1
  echo "• API /health != 200 → docker logs $API_CONTAINER ; bash devops/scripts/deploy-api.sh"
fi
if [[ "$A" != "200" ]]; then
  problems=1
  echo "• AppVersion != 200 → معمولاً DbVapp یا Migrate:"
  echo "    bash devops/scripts/ensure-dbvapp.sh --restart-api --wait"
  echo "    bash devops/scripts/fix-fresh-db-and-rebuild-api.sh --rebuild"
fi
if [[ "$N" != "200" ]]; then
  problems=1
  echo "• Admin/nginx root != 200 → bash devops/scripts/deploy-front-host.sh"
fi
if [[ "$F" != "200" || "$W" != "200" || "$C" != "200" || "$B" != "200" ]]; then
  problems=1
  echo "• Public form/wheel/card/book != 200 → bash devops/scripts/deploy-public-front-host.sh"
fi

if [[ "$problems" -eq 0 ]]; then
  deploy_ok_box "همه چک‌ها سبز — سیستم آماده است"
  exit 0
fi

echo ""
echo "Update commands:"
echo "  bash ~/Api_Vapp_Manually/vapp-iran-update.sh --api-only"
echo "  bash ~/Api_Vapp_Manually/vapp-iran-update.sh --front-only"
echo "  bash ~/Api_Vapp_Manually/vapp-iran-update.sh --public-only"
echo "  bash ~/Api_Vapp_Manually/vapp-iran-update.sh --full"
exit 1
