#!/usr/bin/env bash
# تست بازیابی واقعی بدون دست زدن به DbVapp تولید
# بکاپ را در DbVapp_RestoreTest باز می‌کند، چک می‌کند، بعد پاک می‌کند
#
#   bash test-restore-database.sh --file ~/Api_Vapp_Manually/backups/daily/DbVapp_full_....bak
#   bash test-restore-database.sh --file ... --keep
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
SOURCE_DB="${DB_NAME:-DbVapp}"
TEST_DB="${RESTORE_TEST_DB:-DbVapp_RestoreTest}"
BACKUP_ROOT="${BACKUP_ROOT:-$API_ROOT/backups}"

BAK_FILE=""
KEEP_TEST_DB="false"

usage() {
  cat <<EOF
Usage:
  \$0 --file PATH [--keep]

  --file  مسیر .bak روی host
  --keep  بعد از تست، ${TEST_DB} را حذف نکن

این اسکریپت ${SOURCE_DB} اصلی (production) را دست نمی‌زند.
EOF
  exit 1
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --file) BAK_FILE="$2"; shift 2 ;;
    --keep) KEEP_TEST_DB="true"; shift ;;
    -h|--help) usage ;;
    *) echo "Unknown arg: $1" >&2; usage ;;
  esac
done

[[ -n "$BAK_FILE" && -f "$BAK_FILE" ]] || { echo "ERROR: backup file not found: $BAK_FILE" >&2; exit 1; }

log() { echo "[$(date -u +%Y-%m-%dT%H:%M:%SZ)] $*"; }
die() { log "ERROR: $*"; exit 1; }

load_sa_password() {
  if [[ -n "${SA_PASSWORD:-}" ]]; then return 0; fi
  SA_PASSWORD="$(grep -E '^SA_PASSWORD=' "$API_ENV" | tail -1 | cut -d= -f2- | tr -d '\r' | sed 's/^["'\'']//;s/["'\'']$//')"
  [[ -n "$SA_PASSWORD" ]] || die "SA_PASSWORD empty in $API_ENV"
}

resolve_sqlcmd_in_container() {
  docker exec "$SQL_CONTAINER" bash -c '
    for p in /opt/mssql-tools/bin/sqlcmd /opt/mssql-tools18/bin/sqlcmd; do
      [[ -x "$p" ]] && echo "$p" && exit 0
    done
    exit 1
  '
}

run_sql() {
  local query="$1"
  local timeout_sec="${2:-120}"
  local rc=0
  timeout "$timeout_sec" docker exec \
    -e SA_PASSWORD="$SA_PASSWORD" \
    -e SQLCMD="$SQLCMD" \
    -e "SQLQUERY=SET NOCOUNT ON; ${query}" \
    "$SQL_CONTAINER" bash -c \
    '"$SQLCMD" -S localhost -U sa -P "$SA_PASSWORD" -C -b -l 120 -t 0 -Q "$SQLQUERY"' || rc=$?
  # sqlcmd گاهی بعد از RESTORE/VERIFY خارج نمی‌شود
  if [[ "$rc" -eq 0 || "$rc" -eq 124 ]]; then
    return 0
  fi
  return "$rc"
}

run_sql_in_db() {
  local db="$1"
  local query="$2"
  local timeout_sec="${3:-30}"
  local rc=0
  local out
  out="$(timeout "$timeout_sec" docker exec \
    -e SA_PASSWORD="$SA_PASSWORD" \
    -e SQLCMD="$SQLCMD" \
    -e SQLDB="$db" \
    -e "SQLQUERY=SET NOCOUNT ON; ${query}" \
    "$SQL_CONTAINER" bash -c \
    '"$SQLCMD" -S localhost -U sa -P "$SA_PASSWORD" -d "$SQLDB" -C -s"|" -h-1 -W -Q "$SQLQUERY"' 2>/dev/null)" || rc=$?
  if [[ "$rc" -ne 0 && "$rc" -ne 124 ]]; then
    return "$rc"
  fi
  printf '%s\n' "$out"
}

load_sa_password
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

log "=== Restore test start (production DB '${SOURCE_DB}' untouched) ==="
log "Backup file: $BAK_FILE"
log "Test database: $TEST_DB"

docker ps --format '{{.Names}}' | grep -qx "$SQL_CONTAINER" || die "Container not running: $SQL_CONTAINER"

log "Step 1/5: VERIFYONLY ..."
verify_out="$(timeout 90 docker exec \
  -e SA_PASSWORD="$SA_PASSWORD" \
  -e SQLCMD="$SQLCMD" \
  -e "SQLQUERY=SET NOCOUNT ON; RESTORE VERIFYONLY FROM DISK = N'${bak_container}' WITH CHECKSUM;" \
  "$SQL_CONTAINER" bash -c \
  '"$SQLCMD" -S localhost -U sa -P "$SA_PASSWORD" -C -b -l 120 -t 0 -Q "$SQLQUERY"' 2>&1 || true)"
printf '%s\n' "$verify_out"
echo "$verify_out" | grep -qi 'is valid' || die "VERIFYONLY failed — backup not restorable"

log "Step 2/5: Read logical file names from backup ..."
filelist_out="$(mktemp)"
timeout 60 docker exec \
  -e SA_PASSWORD="$SA_PASSWORD" \
  -e SQLCMD="$SQLCMD" \
  -e "SQLQUERY=RESTORE FILELISTONLY FROM DISK = N'${bak_container}';" \
  "$SQL_CONTAINER" bash -c \
  '"$SQLCMD" -S localhost -U sa -P "$SA_PASSWORD" -C -y 64 -Y 64 -s"|" -h-1 -Q "$SQLQUERY"' >"$filelist_out" || true

mapfile -t logical_pair < <(python3 - "$filelist_out" <<'PY'
import sys
path = sys.argv[1]
data = log = None
with open(path, encoding="utf-8", errors="replace") as f:
    for raw in f:
        line = raw.strip()
        if not line or set(line) == {"-"} or "affected" in line.lower():
            continue
        parts = [p.strip() for p in line.split("|")]
        if len(parts) < 3:
            continue
        logical, typ = parts[0], parts[2].upper()
        if typ == "D" and not data:
            data = logical
        elif typ == "L" and not log:
            log = logical
if data and log:
    print(data)
    print(log)
PY
)

if [[ ${#logical_pair[@]} -eq 2 ]]; then
  data_logical="${logical_pair[0]}"
  log_logical="${logical_pair[1]}"
else
  data_logical="${SOURCE_DB}"
  log_logical="${SOURCE_DB}_log"
  log "WARN: FILELISTONLY parse failed; fallback logical names: ${data_logical} / ${log_logical}"
  log "DEBUG FILELISTONLY (first 5 lines):"
  head -5 "$filelist_out" | while read -r line; do log "  $line"; done
fi
rm -f "$filelist_out"

log "Logical files: data=${data_logical} log=${log_logical}"

log "Step 3/5: Drop previous test DB if exists ..."
run_sql "IF DB_ID('${TEST_DB}') IS NOT NULL BEGIN ALTER DATABASE [${TEST_DB}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [${TEST_DB}]; END;"

data_physical="/var/opt/mssql/data/${TEST_DB}.mdf"
log_physical="/var/opt/mssql/data/${TEST_DB}_log.ldf"

log "Step 4/5: RESTORE into ${TEST_DB} (side database) ..."
# timeout کوتاه: sqlcmd بعد از RESTORE اغلب hang می‌کند؛ نتیجه را با وجود DB چک می‌کنیم
run_sql "RESTORE DATABASE [${TEST_DB}] FROM DISK = N'${bak_container}' WITH MOVE N'${data_logical}' TO N'${data_physical}', MOVE N'${log_logical}' TO N'${log_physical}', RECOVERY, STATS = 10;" 45
db_id="$(run_sql_in_db master "SELECT DB_ID('${TEST_DB}');" 20 | tr -d '\r' | awk 'NF && $0 !~ /^Msg / {print; exit}' | xargs || true)"
[[ -n "$db_id" && "$db_id" != "NULL" && "$db_id" =~ ^[0-9]+$ ]] \
  || die "RESTORE finished but ${TEST_DB} not found (db_id=${db_id:-none})"
log "RESTORE OK (db_id=${db_id})"

log "Step 5/5: Validate schema + sample data in ${TEST_DB} ..."
validation_line="$(run_sql_in_db "$TEST_DB" "SELECT (SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped = 0), (SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = '__EFMigrationsHistory'), (SELECT COUNT(*) FROM dbo.__EFMigrationsHistory), (SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Users'), (SELECT COUNT(*) FROM dbo.Users), (SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Payments'), (SELECT COUNT(*) FROM dbo.Payments);" 60 | tr -d '\r' | grep -E '^[0-9]+\|' | tail -1)"

IFS='|' read -r tables migrations migration_rows users user_rows payments payment_rows <<< "$validation_line"

log "Validation summary:"
log "  tables (user):                 ${tables:-?}"
log "  __EFMigrationsHistory exists:  ${migrations:-?}"
log "  migration rows:                ${migration_rows:-?}"
log "  Users table exists:            ${users:-?}"
log "  Users rows:                    ${user_rows:-?}"
log "  Payments table exists:         ${payments:-?}"
log "  Payments rows:                 ${payment_rows:-?}"

[[ -n "${tables:-}" && "$tables" =~ ^[0-9]+$ && "$tables" -gt 0 ]] || die "Restore test failed: no user tables found"
[[ "${migrations:-}" == "1" ]] || die "Restore test failed: __EFMigrationsHistory missing"
[[ -n "${migration_rows:-}" && "$migration_rows" =~ ^[0-9]+$ && "$migration_rows" -gt 0 ]] || die "Restore test failed: migration history empty"
[[ "${users:-}" == "1" ]] || die "Restore test failed: Users table missing"
[[ "${payments:-}" == "1" ]] || die "Restore test failed: Payments table missing"

if [[ "$KEEP_TEST_DB" == "true" ]]; then
  log "KEEP: ${TEST_DB} left on server for manual inspection"
else
  log "Cleanup: dropping ${TEST_DB} ..."
  run_sql "ALTER DATABASE [${TEST_DB}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [${TEST_DB}];"
fi

log "SUCCESS: real restore test passed — backup is restorable; ${SOURCE_DB} was NOT modified"
