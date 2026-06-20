#!/usr/bin/env bash
# بکاپ FULL از DbVapp داخل Docker SQL Server
#
# Usage:
#   bash ~/Api_Vapp_Manually/devops/scripts/backup-database.sh
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
KEEP_DAILY="${KEEP_DAILY:-14}"
BACKUP_KIND="${BACKUP_KIND:-daily}"

DAILY_DIR="$BACKUP_ROOT/daily"
WEEKLY_DIR="$BACKUP_ROOT/weekly"
LOG_DIR="$BACKUP_ROOT/logs"
TS="$(date -u +%Y%m%d_%H%M%S)"
LOG_FILE="$LOG_DIR/backup-${TS}.log"

mkdir -p "$DAILY_DIR" "$WEEKLY_DIR" "$LOG_DIR"
exec > >(tee -a "$LOG_FILE") 2>&1

log() { echo "[$(date -u +%Y-%m-%dT%H:%M:%SZ)] $*"; }
die() { log "ERROR: $*"; exit 1; }

load_sa_password() {
  [[ -n "${SA_PASSWORD:-}" ]] && return 0
  [[ -f "$API_ENV" ]] || die "docker/.env not found: $API_ENV"
  SA_PASSWORD="$(grep -E '^SA_PASSWORD=' "$API_ENV" | grep -v '^#' | tail -1 | cut -d= -f2- | tr -d '\r' | sed 's/^["'\'']//;s/["'\'']$//')"
  [[ -n "$SA_PASSWORD" ]] || die "SA_PASSWORD empty in $API_ENV"
}

resolve_sqlcmd_in_container() {
  docker exec "$SQL_CONTAINER" bash -c '
    for p in /opt/mssql-tools/bin/sqlcmd /opt/mssql-tools18/bin/sqlcmd; do
      [[ -x "$p" ]] && echo "$p" && exit 0
    done
    exit 1
  ' 2>/dev/null || die "sqlcmd not found in $SQL_CONTAINER"
}

run_sql() {
  local query="$1"
  printf '%s\n' "SET NOCOUNT ON; $query" | docker exec -i -e SA_PASSWORD="$SA_PASSWORD" -e SQLCMD="$SQLCMD" "$SQL_CONTAINER" bash -c \
    '"$SQLCMD" -S localhost -U sa -P "$SA_PASSWORD" -C -b'
}

log "=== DbVapp full backup start (kind=$BACKUP_KIND) ==="
load_sa_password
docker ps --format '{{.Names}}' | grep -qx "$SQL_CONTAINER" || die "Container not running: $SQL_CONTAINER"

SQLCMD="$(resolve_sqlcmd_in_container)"
target_dir="$DAILY_DIR"
[[ "$BACKUP_KIND" == "weekly" ]] && target_dir="$WEEKLY_DIR"

base_name="${DB_NAME}_full_${TS}"
bak_host="$target_dir/${base_name}.bak"
bak_container="/backups/$(basename "$target_dir")/${base_name}.bak"

log "Backup target: $bak_host"
run_sql "BACKUP DATABASE [${DB_NAME}] TO DISK = N'${bak_container}' WITH COMPRESSION, CHECKSUM, INIT, STATS = 10, NAME = N'${DB_NAME}-Full-${TS}';"
[[ -f "$bak_host" ]] || die "Backup file missing: $bak_host"

if command -v sha256sum >/dev/null 2>&1; then
  (cd "$(dirname "$bak_host")" && sha256sum "$(basename "$bak_host")" > "$(basename "$bak_host").sha256")
fi

ln -sfn "$bak_host" "$BACKUP_ROOT/latest.bak"
find "$DAILY_DIR" -maxdepth 1 -type f -name "${DB_NAME}_full_*.bak" -mtime +"$KEEP_DAILY" -delete 2>/dev/null || true
log "SUCCESS: $(basename "$bak_host")"
log "Log: $LOG_FILE"
