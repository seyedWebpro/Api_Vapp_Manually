#!/usr/bin/env bash
# Wait until API is truly ready: /health + /api/AppVersion/check (proves DbVapp + EF Migrate + seed).
# /health alone is NOT enough — it returns 200 even when DB is missing.
#
# Usage:
#   bash devops/scripts/wait-db-ready.sh
#   MAX_ATTEMPTS=48 INTERVAL_SECS=10 bash devops/scripts/wait-db-ready.sh
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=lib/load-server-conf.sh
source "$SCRIPT_DIR/lib/load-server-conf.sh" 2>/dev/null || true

API_DIR="${API_DIR:-${REMOTE_API_REPO:-$HOME/Api_Vapp_Manually}}"
ENV_FILE="${ENV_FILE:-$API_DIR/docker/.env}"
SQL_CONTAINER="${SQL_CONTAINER:-vapp_sqlserver_prod}"
DB_NAME="${DB_NAME:-DbVapp}"
MAX_ATTEMPTS="${MAX_ATTEMPTS:-48}"
INTERVAL_SECS="${INTERVAL_SECS:-10}"
CHECK_SQL="${CHECK_SQL:-1}"

log() { echo "[$(date -Is)] $*"; }
die() { echo "ERROR: $*" >&2; exit 1; }

sqlcmd_bin() {
  docker exec "$SQL_CONTAINER" sh -c 'command -v sqlcmd || ls /opt/mssql-tools/bin/sqlcmd /opt/mssql-tools18/bin/sqlcmd 2>/dev/null | head -1' 2>/dev/null || true
}

check_sql_migrated() {
  [[ "$CHECK_SQL" == "1" ]] || return 0
  [[ -f "$ENV_FILE" ]] || return 0
  docker inspect "$SQL_CONTAINER" >/dev/null 2>&1 || return 0
  local sa sqlcmd
  sa="$(grep -E '^SA_PASSWORD=' "$ENV_FILE" | head -1 | cut -d= -f2-)"
  sqlcmd="$(sqlcmd_bin)"
  [[ -n "$sa" && -n "$sqlcmd" ]] || return 0

  local state hist policies
  state="$(docker exec "$SQL_CONTAINER" "$sqlcmd" -S localhost -U sa -P "$sa" -C -h -1 -W -Q \
    "SET NOCOUNT ON; SELECT state_desc FROM sys.databases WHERE name=N'$DB_NAME';" 2>/dev/null | tr -d '[:space:]' || true)"
  [[ "$state" == "ONLINE" ]] || { echo "sql_state=$state"; return 1; }

  hist="$(docker exec "$SQL_CONTAINER" "$sqlcmd" -S localhost -U sa -P "$sa" -C -d "$DB_NAME" -h -1 -W -Q \
    "SET NOCOUNT ON; IF OBJECT_ID(N'dbo.__EFMigrationsHistory') IS NULL SELECT 0 ELSE SELECT COUNT(*) FROM dbo.__EFMigrationsHistory;" 2>/dev/null | tr -d '[:space:]' || echo 0)"
  policies="$(docker exec "$SQL_CONTAINER" "$sqlcmd" -S localhost -U sa -P "$sa" -C -d "$DB_NAME" -h -1 -W -Q \
    "SET NOCOUNT ON; IF OBJECT_ID(N'dbo.AppVersionPolicies') IS NULL SELECT 0 ELSE SELECT COUNT(*) FROM dbo.AppVersionPolicies;" 2>/dev/null | tr -d '[:space:]' || echo 0)"

  echo "sql_state=$state ef_migrations=$hist appversion_rows=$policies"
  [[ "${hist:-0}" =~ ^[0-9]+$ && "${hist:-0}" -gt 0 ]] || return 1
  [[ "${policies:-0}" =~ ^[0-9]+$ && "${policies:-0}" -gt 0 ]] || return 1
  return 0
}

log "=== wait-db-ready max=${MAX_ATTEMPTS} interval=${INTERVAL_SECS}s ==="

for i in $(seq 1 "$MAX_ATTEMPTS"); do
  # مهم: از `curl || echo 000` استفاده نکن — اگر curl خودش 000 بنویسد، می‌شود 000000
  health="$(curl -sS -m 10 -o /dev/null -w '%{http_code}' http://127.0.0.1:8080/health 2>/dev/null)" || health="000"
  appver="$(curl -sS -m 20 -o /tmp/vapp-appver.json -w '%{http_code}' \
    'http://127.0.0.1:8080/api/AppVersion/check?platform=android&currentVersion=1.0.0' 2>/dev/null)" || appver="000"
  [[ "$health" =~ ^[0-9]{3}$ ]] || health="000"
  [[ "$appver" =~ ^[0-9]{3}$ ]] || appver="000"

  sql_info=""
  sql_ok=0
  if sql_info="$(check_sql_migrated 2>/dev/null)"; then
    sql_ok=1
  else
    sql_info="${sql_info:-sql_not_ready}"
  fi

  log "try $i/$MAX_ATTEMPTS health=$health appver=$appver $sql_info"

  if [[ "$health" == "200" && "$appver" == "200" && "$sql_ok" == "1" ]]; then
    log "OK: DB migrated + AppVersion ready"
    head -c 300 /tmp/vapp-appver.json 2>/dev/null; echo
    exit 0
  fi

  # health+appver کافی است اگر sqlcmd در دسترس نبود
  if [[ "$health" == "200" && "$appver" == "200" && "$CHECK_SQL" != "1" ]]; then
    log "OK: health+AppVersion (SQL check skipped)"
    exit 0
  fi

  sleep "$INTERVAL_SECS"
done

log "FAIL: API/DB not ready after ${MAX_ATTEMPTS} attempts"
log "Hints:"
log "  bash $SCRIPT_DIR/ensure-dbvapp.sh --restart-api"
log "  docker logs --tail 80 vapp_api_prod | grep -iE 'Migration|PendingModel|Cannot open|ensured'"
log "  grep -E 'Migration completed|PendingModelChanges|Cannot open' $API_DIR/log/log-\$(date +%Y%m%d).txt | tail -20"
exit 1
