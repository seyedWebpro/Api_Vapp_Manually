#!/usr/bin/env bash
# بازیابی FULL production از فایل .bak — فقط با --confirm RESTORE
# بعد از RESTORE همیشه DB ONLINE + sample query چک می‌شود (timeout ≠ موفقیت خاموش)
#
#   bash restore-database.sh --file /path/to/DbVapp_full_....bak --confirm RESTORE
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DEVOPS_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
API_ROOT="${API_ROOT:-$HOME/Api_Vapp_Manually}"

BACKUP_ENV="${BACKUP_ENV:-$DEVOPS_ROOT/backup/backup.env}"
if [[ -f "$BACKUP_ENV" ]]; then
  set -a
  # shellcheck disable=SC1090
  source "$BACKUP_ENV"
  set +a
fi

API_ENV="${API_ENV:-$API_ROOT/docker/.env}"
SQL_CONTAINER="${SQL_CONTAINER:-vapp_sqlserver_prod}"
DB_NAME="${DB_NAME:-DbVapp}"
BACKUP_ROOT="${BACKUP_ROOT:-$API_ROOT/backups}"
COMPOSE_FILE="${COMPOSE_FILE:-$API_ROOT/docker/docker-compose.production.yml}"
COMPOSE_ENV="${COMPOSE_ENV:-$API_ROOT/docker/.env}"
API_HEALTH_URL="${API_HEALTH_URL:-http://127.0.0.1/health}"
API_HEALTH_TIMEOUT_SEC="${API_HEALTH_TIMEOUT_SEC:-120}"

BAK_FILE=""
CONFIRM=""
DRY_RUN="false"

usage() {
  cat <<EOF
Usage:
  $0 --file PATH --confirm RESTORE
  $0 --file PATH --dry-run

  --file     مسیر فایل .bak روی host
  --confirm  باید دقیقاً RESTORE باشد (برای بازیابی واقعی)
  --dry-run  فقط SHA256 + VERIFYONLY — DbVapp دست نمی‌خورد
EOF
  exit 1
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --file) BAK_FILE="$2"; shift 2 ;;
    --confirm) CONFIRM="$2"; shift 2 ;;
    --dry-run) DRY_RUN="true"; shift ;;
    -h|--help) usage ;;
    *) echo "Unknown arg: $1" >&2; usage ;;
  esac
done

[[ -n "$BAK_FILE" && -f "$BAK_FILE" ]] || { echo "ERROR: backup file not found: $BAK_FILE" >&2; exit 1; }
if [[ "$DRY_RUN" != "true" ]]; then
  [[ "$CONFIRM" == "RESTORE" ]] || { echo "ERROR: pass --confirm RESTORE (or --dry-run)" >&2; exit 1; }
fi

log() { echo "[$(date -u +%Y-%m-%dT%H:%M:%SZ)] $*"; }
die() { log "ERROR: $*"; exit 1; }

resolve_running_sql_container() {
  if docker ps --format '{{.Names}}' | grep -qx "$SQL_CONTAINER"; then
    return 0
  fi
  local found=""
  found="$(docker ps --format '{{.Names}}' | grep -E "^[0-9a-f]{12}_${SQL_CONTAINER}$" | head -1 || true)"
  [[ -n "$found" ]] || die "Container not running: $SQL_CONTAINER"
  SQL_CONTAINER="$found"
}

load_sa_password() {
  if [[ -n "${SA_PASSWORD:-}" ]]; then return 0; fi
  SA_PASSWORD="$(grep -E '^SA_PASSWORD=' "$API_ENV" | grep -v '^#' | tail -1 | cut -d= -f2- | tr -d '\r' | sed 's/^["'\'']//;s/["'\'']$//')"
  [[ -n "$SA_PASSWORD" ]] || die "SA_PASSWORD empty"
}

resolve_sqlcmd_in_container() {
  docker exec "$SQL_CONTAINER" bash -c '
    for p in /opt/mssql-tools/bin/sqlcmd /opt/mssql-tools18/bin/sqlcmd; do
      [[ -x "$p" ]] && echo "$p" && exit 0
    done
    exit 1
  ' || die "sqlcmd not found"
}

# rc=124 (timeout/hang) را فقط وقتی قبول می‌کنیم که caller بعداً نتیجه را مستقل چک کند
run_sql_maybe_hang() {
  local query="$1"
  local timeout_sec="${2:-300}"
  local rc=0
  timeout "$timeout_sec" docker exec \
    -e SA_PASSWORD="$SA_PASSWORD" \
    -e SQLCMD="$SQLCMD" \
    -e "SQLQUERY=SET NOCOUNT ON; ${query}" \
    "$SQL_CONTAINER" bash -c \
    '"$SQLCMD" -S localhost -U sa -P "$SA_PASSWORD" -C -b -l 120 -t 0 -Q "$SQLQUERY"' || rc=$?
  if [[ "$rc" -eq 0 || "$rc" -eq 124 ]]; then
    return 0
  fi
  return "$rc"
}

run_sql_scalar() {
  local query="$1"
  local timeout_sec="${2:-60}"
  timeout "$timeout_sec" docker exec \
    -e SA_PASSWORD="$SA_PASSWORD" \
    -e SQLCMD="$SQLCMD" \
    -e "SQLQUERY=SET NOCOUNT ON; ${query}" \
    "$SQL_CONTAINER" bash -c \
    '"$SQLCMD" -S localhost -U sa -P "$SA_PASSWORD" -C -b -l 60 -h-1 -W -Q "$SQLQUERY"' 2>/dev/null \
    | tr -d '\r' | awk 'NF && $0 !~ /^Msg / && $0 !~ /^Sqlcmd:/ {print; exit}' | xargs
}

compose_stop_api() {
  (cd "$(dirname "$COMPOSE_FILE")" && docker compose -f "$(basename "$COMPOSE_FILE")" --env-file "$COMPOSE_ENV" stop api) \
    || docker stop vapp_api_prod 2>/dev/null || true
}

compose_start_api() {
  (cd "$(dirname "$COMPOSE_FILE")" && docker compose -f "$(basename "$COMPOSE_FILE")" --env-file "$COMPOSE_ENV" up -d api) \
    || docker start vapp_api_prod
}

acquire_restore_lock() {
  mkdir -p "$BACKUP_ROOT"
  exec 9>"$BACKUP_ROOT/.backup.lock"
  if ! flock -n 9; then
    die "Another backup/restore is running (lock: $BACKUP_ROOT/.backup.lock)"
  fi
}

verify_db_online() {
  local state user_count
  state="$(run_sql_scalar "SELECT state_desc FROM sys.databases WHERE name = N'${DB_NAME}';" 30 || true)"
  [[ "$state" == "ONLINE" ]] || die "DB ${DB_NAME} state=${state:-unknown} (expected ONLINE)"
  user_count="$(run_sql_scalar "SELECT COUNT(*) FROM [${DB_NAME}].dbo.Users;" 30 || true)"
  [[ -n "$user_count" && "$user_count" =~ ^[0-9]+$ ]] || die "Post-restore query failed on ${DB_NAME}.dbo.Users"
  log "Post-restore OK: ${DB_NAME} ONLINE, Users=${user_count}"
}

wait_api_healthy() {
  local elapsed=0 code
  log "Waiting for API health (up to ${API_HEALTH_TIMEOUT_SEC}s) ..."
  while (( elapsed < API_HEALTH_TIMEOUT_SEC )); do
    code="$(curl -sS -m5 -o /dev/null -w '%{http_code}' "$API_HEALTH_URL" 2>/dev/null || echo 000)"
    if [[ "$code" == "200" ]]; then
      log "API healthy (HTTP 200)"
      return 0
    fi
    sleep 5
    elapsed=$((elapsed + 5))
  done
  log "WARN: API health not 200 yet (last=${code:-none}) — check docker logs vapp_api_prod"
}

# --- main ---
BAK_FILE="$(readlink -f "$BAK_FILE" 2>/dev/null || realpath "$BAK_FILE")"
log "=== PRODUCTION restore start ==="
log "File: $BAK_FILE"
log "Target DB: $DB_NAME (WILL BE REPLACED)"

load_sa_password
resolve_running_sql_container
acquire_restore_lock
SQLCMD="$(resolve_sqlcmd_in_container)"

case "$BAK_FILE" in
  "$BACKUP_ROOT"/*)
    bak_container="/backups/${BAK_FILE#$BACKUP_ROOT/}"
    ;;
  *)
    mkdir -p "$BACKUP_ROOT/restore"
    MSSQL_UID="$(docker exec "$SQL_CONTAINER" id -u mssql 2>/dev/null || echo 10001)"
    MSSQL_GID="$(docker exec "$SQL_CONTAINER" id -g mssql 2>/dev/null || echo 0)"
    cp -f "$BAK_FILE" "$BACKUP_ROOT/restore/$(basename "$BAK_FILE")"
    chown "${MSSQL_UID}:${MSSQL_GID}" "$BACKUP_ROOT/restore/$(basename "$BAK_FILE")" 2>/dev/null || true
    bak_container="/backups/restore/$(basename "$BAK_FILE")"
    ;;
esac

# SHA256 اگر کنار فایل باشد
if [[ -f "${BAK_FILE}.sha256" ]]; then
  log "Verifying SHA256 ..."
  (cd "$(dirname "$BAK_FILE")" && sha256sum -c "$(basename "${BAK_FILE}.sha256")") \
    || die "SHA256 mismatch — refuse to restore corrupt file"
fi

log "VERIFYONLY ..."
verify_out="$(timeout 180 docker exec \
  -e SA_PASSWORD="$SA_PASSWORD" \
  -e SQLCMD="$SQLCMD" \
  -e "SQLQUERY=SET NOCOUNT ON; RESTORE VERIFYONLY FROM DISK = N'${bak_container}' WITH CHECKSUM;" \
  "$SQL_CONTAINER" bash -c \
  '"$SQLCMD" -S localhost -U sa -P "$SA_PASSWORD" -C -b -l 120 -t 0 -Q "$SQLQUERY"' 2>&1 || true)"
printf '%s\n' "$verify_out"
echo "$verify_out" | grep -qi 'is valid' \
  || die "VERIFYONLY failed — aborting restore"

if [[ "$DRY_RUN" == "true" ]]; then
  log "OK: dry-run passed (SHA256 + VERIFYONLY) — production DB untouched"
  exit 0
fi

log "Stopping API to release DB connections ..."
compose_stop_api
sleep 2

log "SET SINGLE_USER ..."
run_sql_maybe_hang "ALTER DATABASE [${DB_NAME}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;" 60

log "RESTORE DATABASE REPLACE ..."
run_sql_maybe_hang "RESTORE DATABASE [${DB_NAME}] FROM DISK = N'${bak_container}' WITH REPLACE, RECOVERY, STATS = 10;" 600

log "SET MULTI_USER ..."
run_sql_maybe_hang "ALTER DATABASE [${DB_NAME}] SET MULTI_USER;" 30

verify_db_online

log "Starting API ..."
compose_start_api

wait_api_healthy

log "OK: production restore completed from $(basename "$BAK_FILE")"
