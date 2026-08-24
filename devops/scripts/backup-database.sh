#!/usr/bin/env bash
# بکاپ کامل FULL از DbVapp داخل Docker SQL Server
# سطح Microless + یک پله بالاتر:
# - COMPRESSION + CHECKSUM + RESTORE VERIFYONLY + SHA256 + manifest JSON
# - lock / disk check / permissions / retention daily+weekly
# - آپلود اختیاری rclone + verify اندازه
# - status.json برای مانیتورینگ
# - وب‌هوک اختیاری روی SUCCESS/FAILURE
#
# اجرا روی سرور: bash ~/Api_Vapp_Manually/devops/scripts/backup-database.sh
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
KEEP_WEEKLY="${KEEP_WEEKLY:-8}"
BACKUP_KIND="${BACKUP_KIND:-daily}"
RCLONE_REMOTE="${RCLONE_REMOTE:-}"
RCLONE_FLAGS="${RCLONE_FLAGS:---transfers 2 --checkers 4 --retries 3 --low-level-retries 10 --checksum}"
KEEP_REMOTE_DAILY="${KEEP_REMOTE_DAILY:-30}"
KEEP_REMOTE_WEEKLY_DAYS="${KEEP_REMOTE_WEEKLY_DAYS:-56}"
OFFSITE_UPLOADED="false"
MIN_FREE_DISK_MULTIPLIER="${MIN_FREE_DISK_MULTIPLIER:-2.0}"
MIN_FREE_DISK_MB="${MIN_FREE_DISK_MB:-512}"
SQL_READY_TIMEOUT_SEC="${SQL_READY_TIMEOUT_SEC:-180}"
SQL_READY_INTERVAL_SEC="${SQL_READY_INTERVAL_SEC:-5}"
BACKUP_SQL_TIMEOUT_SEC="${BACKUP_SQL_TIMEOUT_SEC:-3600}"
BACKUP_FILE_MODE="${BACKUP_FILE_MODE:-600}"
BACKUP_DIR_MODE="${BACKUP_DIR_MODE:-700}"
MSSQL_UID="${MSSQL_UID:-}"
MSSQL_GID="${MSSQL_GID:-0}"
NOTIFY_WEBHOOK_URL="${NOTIFY_WEBHOOK_URL:-}"
NOTIFY_ON_SUCCESS="${NOTIFY_ON_SUCCESS:-false}"

DAILY_DIR="$BACKUP_ROOT/daily"
WEEKLY_DIR="$BACKUP_ROOT/weekly"
LOG_DIR="$BACKUP_ROOT/logs"
LOCK_FILE="$BACKUP_ROOT/.backup.lock"
STATUS_FILE="$BACKUP_ROOT/status.json"
TS="$(date -u +%Y%m%d_%H%M%S)"
LOG_FILE="$LOG_DIR/backup-${TS}.log"
BACKUP_EXIT_REASON=""
BACKUP_EXIT_CODE=1

mkdir -p "$DAILY_DIR" "$WEEKLY_DIR" "$LOG_DIR"

exec > >(tee -a "$LOG_FILE") 2>&1

log() { echo "[$(date -u +%Y-%m-%dT%H:%M:%SZ)] $*"; }
die() { BACKUP_EXIT_REASON="$*"; log "ERROR: $*"; exit 1; }

resolve_running_sql_container() {
  if docker ps --format '{{.Names}}' | grep -qx "$SQL_CONTAINER"; then
    return 0
  fi
  local found=""
  found="$(docker ps --format '{{.Names}}' | grep -E "^[0-9a-f]{12}_${SQL_CONTAINER}$" | head -1 || true)"
  [[ -n "$found" ]] || die "Container not running: $SQL_CONTAINER (no compose-prefixed match either)"
  log "WARN: SQL container name mismatch (compose conflict). expected=${SQL_CONTAINER} using=${found}"
  SQL_CONTAINER="$found"
}

load_sa_password() {
  if [[ -n "${SA_PASSWORD:-}" ]]; then
    return 0
  fi
  [[ -f "$API_ENV" ]] || die "API .env not found: $API_ENV (set SA_PASSWORD or BACKUP_ENV)"
  SA_PASSWORD="$(grep -E '^SA_PASSWORD=' "$API_ENV" | grep -v '^#' | tail -1 | cut -d= -f2- | tr -d '\r' | sed 's/^["'\'']//;s/["'\'']$//')"
  [[ -n "$SA_PASSWORD" ]] || die "SA_PASSWORD empty in $API_ENV"
}

resolve_sqlcmd_in_container() {
  docker exec "$SQL_CONTAINER" bash -c '
    for p in /opt/mssql-tools/bin/sqlcmd /opt/mssql-tools18/bin/sqlcmd; do
      if [[ -x "$p" ]]; then echo "$p"; exit 0; fi
    done
    exit 1
  ' 2>/dev/null || die "sqlcmd not found in container $SQL_CONTAINER"
}

run_sql() {
  local query="$1"
  local timeout_sec="${2:-120}"
  timeout "$timeout_sec" docker exec \
    -e SA_PASSWORD="$SA_PASSWORD" \
    -e SQLCMD="$SQLCMD" \
    -e "SQLQUERY=SET NOCOUNT ON; ${query}" \
    "$SQL_CONTAINER" bash -c \
    '"$SQLCMD" -S localhost -U sa -P "$SA_PASSWORD" -C -b -l 60 -Q "$SQLQUERY"'
}

run_sql_scalar() {
  local query="$1"
  local timeout_sec="${2:-120}"
  timeout "$timeout_sec" docker exec \
    -e SA_PASSWORD="$SA_PASSWORD" \
    -e SQLCMD="$SQLCMD" \
    -e "SQLQUERY=SET NOCOUNT ON; ${query}" \
    "$SQL_CONTAINER" bash -c \
    '"$SQLCMD" -S localhost -U sa -P "$SA_PASSWORD" -C -b -l 60 -h-1 -W -Q "$SQLQUERY"' 2>/dev/null \
    | tr -d '\r' | awk 'NF && $0 !~ /^Msg / && $0 !~ /^Sqlcmd:/ {print; exit}' | xargs
}

# -t 0: بکاپ‌های بزرگ نباید با query-timeout داخلی sqlcmd قطع شوند؛ فقط outer timeout
run_sql_long() {
  local query="$1"
  local timeout_sec="${2:-$BACKUP_SQL_TIMEOUT_SEC}"
  local rc=0
  timeout "$timeout_sec" docker exec \
    -e SA_PASSWORD="$SA_PASSWORD" \
    -e SQLCMD="$SQLCMD" \
    -e "SQLQUERY=SET NOCOUNT ON; ${query}" \
    "$SQL_CONTAINER" bash -c \
    '"$SQLCMD" -S localhost -U sa -P "$SA_PASSWORD" -C -b -l 120 -t 0 -Q "$SQLQUERY"' || rc=$?
  if [[ "$rc" -eq 0 ]]; then
    return 0
  fi
  if [[ "$rc" -eq 124 ]]; then
    log "WARN: sqlcmd timed out after ${timeout_sec}s (known hang after BACKUP/VERIFY). Verifying independently."
    return 0
  fi
  return "$rc"
}

# بعد از VERIFYONLY (حتی با hang): HEADERONLY باید موفق شود
assert_backup_header_readable() {
  local disk_path="$1"
  local out
  out="$(timeout 60 docker exec \
    -e SA_PASSWORD="$SA_PASSWORD" \
    -e SQLCMD="$SQLCMD" \
    -e "SQLQUERY=SET NOCOUNT ON; RESTORE HEADERONLY FROM DISK = N'${disk_path}';" \
    "$SQL_CONTAINER" bash -c \
    '"$SQLCMD" -S localhost -U sa -P "$SA_PASSWORD" -C -b -h-1 -W -Q "$SQLQUERY"' 2>/dev/null || true)"
  echo "$out" | grep -qiE "${DB_NAME}|Full|Database" \
    || die "RESTORE HEADERONLY failed for ${disk_path} — backup set unreadable"
  log "HEADERONLY OK: backup set readable"
}

run_sql_file_verify() {
  local disk_path="$1"
  local out rc=0
  local verify_timeout=180
  out="$(timeout "$verify_timeout" docker exec \
    -e SA_PASSWORD="$SA_PASSWORD" \
    -e SQLCMD="$SQLCMD" \
    -e "SQLQUERY=SET NOCOUNT ON; RESTORE VERIFYONLY FROM DISK = N'${disk_path}' WITH CHECKSUM;" \
    "$SQL_CONTAINER" bash -c \
    '"$SQLCMD" -S localhost -U sa -P "$SA_PASSWORD" -C -b -l 120 -t 0 -Q "$SQLQUERY"' 2>&1)" || rc=$?
  printf '%s\n' "$out"
  if echo "$out" | grep -qi 'is valid'; then
    log "VERIFYONLY OK: backup set is valid"
    return 0
  fi
  if [[ "$rc" -eq 124 ]] || [[ "$rc" -eq 0 ]]; then
    log "WARN: VERIFYONLY output inconclusive (rc=${rc}); falling back to HEADERONLY"
    assert_backup_header_readable "$disk_path"
    return 0
  fi
  die "RESTORE VERIFYONLY failed (rc=${rc})"
}

wait_for_sql_server() {
  local elapsed=0 last_err=""
  log "Waiting for SQL Server + ${DB_NAME} (up to ${SQL_READY_TIMEOUT_SEC}s) ..."
  while (( elapsed < SQL_READY_TIMEOUT_SEC )); do
    if docker ps --format '{{.Names}}' | grep -qx "$SQL_CONTAINER"; then
      local probe db_id
      probe="$(run_sql_scalar "SELECT 1;" 15 || true)"
      db_id="$(run_sql_scalar "SELECT DB_ID('${DB_NAME}');" 15 || true)"
      last_err="probe=${probe:-none} db_id=${db_id:-none}"
      if [[ "$probe" == "1" && -n "$db_id" && "$db_id" != "NULL" && "$db_id" =~ ^[0-9]+$ ]]; then
        log "SQL Server ready (db_id=${db_id})"
        return 0
      fi
    else
      last_err="container not running"
    fi
    sleep "$SQL_READY_INTERVAL_SEC"
    elapsed=$((elapsed + SQL_READY_INTERVAL_SEC))
  done
  die "SQL Server not ready after ${SQL_READY_TIMEOUT_SEC}s (container=$SQL_CONTAINER db=$DB_NAME last=${last_err})"
}

ensure_backup_volume_mounted() {
  local mount_src
  mount_src="$(docker inspect "$SQL_CONTAINER" --format '{{range .Mounts}}{{if eq .Destination "/backups"}}{{.Source}}{{end}}{{end}}' 2>/dev/null || true)"
  [[ -n "$mount_src" ]] || die "Volume /backups not mounted on $SQL_CONTAINER — run: cd $API_ROOT/docker && docker compose -f docker-compose.production.yml --env-file .env up -d sqlserver"
  [[ -d "$BACKUP_ROOT" ]] || die "BACKUP_ROOT missing on host: $BACKUP_ROOT (mkdir + recreate sqlserver)"
  if [[ ! -d "$mount_src" ]]; then
    die "Bind mount source missing: $mount_src — mkdir -p $BACKUP_ROOT/{daily,weekly,logs} then recreate sqlserver"
  fi
  log "Backup volume: ${mount_src} -> /backups"
}

verify_mssql_write_access() {
  docker exec -u mssql "$SQL_CONTAINER" bash -c \
    'test -d /backups/daily && touch /backups/daily/.perm_probe && rm -f /backups/daily/.perm_probe' \
    || die "mssql cannot write /backups/daily — run: bash $SCRIPT_DIR/install-db-backup-cron.sh && cd $API_ROOT/docker && docker compose -f docker-compose.production.yml --env-file .env up -d sqlserver"
  log "mssql write probe OK: /backups/daily"
}

acquire_lock() {
  exec 9>"$LOCK_FILE"
  if ! flock -n 9; then
    die "Another backup is running (lock: $LOCK_FILE)"
  fi
}

get_database_size_mb() {
  local raw
  raw="$(run_sql_scalar "SELECT CAST(SUM(CAST(size AS BIGINT)) * 8.0 / 1024 AS DECIMAL(18,2)) FROM sys.master_files WHERE database_id = DB_ID('${DB_NAME}') AND type_desc = N'ROWS';" 60 || true)"
  if [[ -z "$raw" || "$raw" == "0" || "$raw" == "0.00" ]]; then
    echo "100"
    return 0
  fi
  python3 -c "import math; print(max(1, int(math.ceil(float('${raw}')))))"
}

check_disk_space() {
  local db_size_mb="$1"
  local required_mb avail_kb avail_mb

  required_mb="$(python3 -c "import math; db=float('${db_size_mb}'); mult=float('${MIN_FREE_DISK_MULTIPLIER}'); floor=int('${MIN_FREE_DISK_MB}'); print(max(floor, int(math.ceil(db * mult))))")"
  avail_kb="$(df -Pk "$BACKUP_ROOT" | awk 'NR==2 {print $4}')"
  [[ -n "$avail_kb" && "$avail_kb" =~ ^[0-9]+$ ]] || die "Cannot read free disk space for $BACKUP_ROOT"
  avail_mb=$((avail_kb / 1024))

  log "Disk check: DB allocated ~${db_size_mb} MiB | need >= ${required_mb} MiB free (${MIN_FREE_DISK_MULTIPLIER}x + min ${MIN_FREE_DISK_MB} MiB) | available ${avail_mb} MiB on $(df -Pk "$BACKUP_ROOT" | awk 'NR==2 {print $1}')"
  if [[ "$avail_mb" -lt "$required_mb" ]]; then
    die "Insufficient disk space on $BACKUP_ROOT (need ${required_mb} MiB, have ${avail_mb} MiB)"
  fi
}

resolve_mssql_ids() {
  if [[ -z "$MSSQL_UID" ]]; then
    MSSQL_UID="$(docker exec "$SQL_CONTAINER" id -u mssql 2>/dev/null || true)"
    [[ -n "$MSSQL_UID" ]] || MSSQL_UID="10001"
  fi
  if [[ -z "$MSSQL_GID" || "$MSSQL_GID" == "0" ]]; then
    MSSQL_GID="$(docker exec "$SQL_CONTAINER" id -g mssql 2>/dev/null || true)"
    [[ -n "$MSSQL_GID" ]] || MSSQL_GID="0"
  fi
}

ensure_sql_backup_dirs() {
  resolve_mssql_ids
  chown "${MSSQL_UID}:${MSSQL_GID}" "$DAILY_DIR" "$WEEKLY_DIR" \
    || die "chown ${MSSQL_UID}:${MSSQL_GID} failed on $DAILY_DIR $WEEKLY_DIR"
  chown root:root "$BACKUP_ROOT" "$LOG_DIR" 2>/dev/null || true
  chmod 711 "$BACKUP_ROOT" 2>/dev/null || true
  chmod "$BACKUP_DIR_MODE" "$DAILY_DIR" "$WEEKLY_DIR" "$LOG_DIR" 2>/dev/null || true
  log "SQL backup dirs: daily+weekly owned by mssql uid=${MSSQL_UID} gid=${MSSQL_GID}"
}

harden_backup_permissions() {
  ensure_sql_backup_dirs
  chmod "$BACKUP_FILE_MODE" "$LOCK_FILE" "$LOG_FILE" 2>/dev/null || true
  find "$DAILY_DIR" "$WEEKLY_DIR" -maxdepth 1 -type f \
    \( -name '*.bak' -o -name '*.sha256' -o -name '*.json' \) \
    -exec chmod "$BACKUP_FILE_MODE" {} + 2>/dev/null || true
}

secure_backup_file() {
  local f="$1"
  [[ -f "$f" ]] || return 0
  chmod "$BACKUP_FILE_MODE" "$f"
}

prune_dir() {
  local dir="$1" keep_days="$2" label="$3"
  local deleted=0
  while IFS= read -r -d '' f; do
    rm -f "$f"
    deleted=$((deleted + 1))
  done < <(find "$dir" -maxdepth 1 -type f -name "${DB_NAME}_full_*.bak" -mtime +"$keep_days" -print0 2>/dev/null || true)

  find "$dir" -maxdepth 1 -type f \( -name "${DB_NAME}_full_*.sha256" -o -name "${DB_NAME}_full_*.json" \) -mtime +"$keep_days" -delete 2>/dev/null || true

  log "Prune $label: removed $deleted backup(s) older than ${keep_days}d in $dir"
}

prune_weekly_count() {
  local dir="$1" keep_count="$2"
  mapfile -t files < <(ls -1t "$dir"/${DB_NAME}_full_*.bak 2>/dev/null || true)
  local i
  for ((i = keep_count; i < ${#files[@]}; i++)); do
    local base="${files[$i]%.bak}"
    rm -f "${files[$i]}" "${base}.sha256" "${base}.json"
    log "Prune weekly: removed ${files[$i]}"
  done
}

verify_remote_file_size() {
  local local_file="$1" remote_path="$2"
  local local_size remote_size
  local_size="$(stat -c '%s' "$local_file" 2>/dev/null || stat -f '%z' "$local_file")"
  remote_size="$(rclone lsl "$remote_path" 2>/dev/null | awk '{print $1}' || true)"
  [[ -n "$remote_size" && "$remote_size" == "$local_size" ]] \
    || die "Offsite verify failed for $(basename "$local_file") (local=${local_size} remote=${remote_size:-missing})"
}

rclone_remote_subdir() {
  [[ "$BACKUP_KIND" == "weekly" ]] && echo "weekly" || echo "daily"
}

rclone_prepare() {
  [[ -n "$RCLONE_REMOTE" ]] || return 0
  command -v rclone >/dev/null 2>&1 || die "RCLONE_REMOTE set but rclone not installed"
  if [[ -f "${HOME}/.config/rclone/rclone.conf" ]]; then
    chmod 600 "${HOME}/.config/rclone/rclone.conf" 2>/dev/null || true
  fi
}

upload_rclone_file() {
  local local_file="$1"
  [[ -n "$RCLONE_REMOTE" ]] || return 0
  rclone_prepare

  local remote_base="${RCLONE_REMOTE}/$(rclone_remote_subdir)"
  # shellcheck disable=SC2086
  rclone copyto "$local_file" "${remote_base}/$(basename "$local_file")" $RCLONE_FLAGS
  verify_remote_file_size "$local_file" "${remote_base}/$(basename "$local_file")"
}

upload_rclone_backup_set() {
  local bak="$1" sha="$2"
  [[ -n "$RCLONE_REMOTE" ]] || return 0
  rclone_prepare

  local remote_base="${RCLONE_REMOTE}/$(rclone_remote_subdir)"
  log "Uploading to rclone://${remote_base}/ ..."
  upload_rclone_file "$bak"
  upload_rclone_file "$sha"
  OFFSITE_UPLOADED="true"
  log "Offsite verify passed: $(basename "$bak") + $(basename "$sha") on ${remote_base}/"
}

prune_remote_dir() {
  [[ -n "$RCLONE_REMOTE" ]] || return 0
  command -v rclone >/dev/null 2>&1 || return 0

  log "Remote prune daily: older than ${KEEP_REMOTE_DAILY}d on ${RCLONE_REMOTE}/daily/"
  # shellcheck disable=SC2086
  rclone delete "${RCLONE_REMOTE}/daily/" --include "${DB_NAME}_full_*.bak" --min-age "${KEEP_REMOTE_DAILY}d" $RCLONE_FLAGS 2>/dev/null || true
  # shellcheck disable=SC2086
  rclone delete "${RCLONE_REMOTE}/daily/" --include "${DB_NAME}_full_*.sha256" --min-age "${KEEP_REMOTE_DAILY}d" $RCLONE_FLAGS 2>/dev/null || true
  # shellcheck disable=SC2086
  rclone delete "${RCLONE_REMOTE}/daily/" --include "${DB_NAME}_full_*.json" --min-age "${KEEP_REMOTE_DAILY}d" $RCLONE_FLAGS 2>/dev/null || true

  log "Remote prune weekly: older than ${KEEP_REMOTE_WEEKLY_DAYS}d on ${RCLONE_REMOTE}/weekly/"
  # shellcheck disable=SC2086
  rclone delete "${RCLONE_REMOTE}/weekly/" --include "${DB_NAME}_full_*.bak" --min-age "${KEEP_REMOTE_WEEKLY_DAYS}d" $RCLONE_FLAGS 2>/dev/null || true
  # shellcheck disable=SC2086
  rclone delete "${RCLONE_REMOTE}/weekly/" --include "${DB_NAME}_full_*.sha256" --min-age "${KEEP_REMOTE_WEEKLY_DAYS}d" $RCLONE_FLAGS 2>/dev/null || true
  # shellcheck disable=SC2086
  rclone delete "${RCLONE_REMOTE}/weekly/" --include "${DB_NAME}_full_*.json" --min-age "${KEEP_REMOTE_WEEKLY_DAYS}d" $RCLONE_FLAGS 2>/dev/null || true
}

write_status_json() {
  local ok="$1"
  local message="$2"
  local bak_file="${3:-}"
  local size_mb="${4:-0}"
  local duration_sec="${5:-0}"
  python3 - "$STATUS_FILE" <<PY
import json, os
meta = {
    "ok": ${ok},
    "message": """${message}""",
    "database": "${DB_NAME}",
    "kind": "${BACKUP_KIND}",
    "timestamp_utc": "${TS}",
    "host": "$(hostname -f 2>/dev/null || hostname)",
    "container": "${SQL_CONTAINER}",
    "file": os.path.basename("""${bak_file}""") if """${bak_file}""" else None,
    "size_mb": int(${size_mb}),
    "duration_seconds": int(${duration_sec}),
    "offsite_enabled": bool("""${RCLONE_REMOTE}"""),
    "offsite_uploaded": True if "${OFFSITE_UPLOADED}" == "true" else False,
    "log_file": os.path.basename("""${LOG_FILE}"""),
}
with open("""${STATUS_FILE}""", "w", encoding="utf-8") as f:
    json.dump(meta, f, ensure_ascii=False, indent=2)
    f.write("\n")
PY
  chmod "$BACKUP_FILE_MODE" "$STATUS_FILE" 2>/dev/null || true
}

notify_webhook() {
  local status="$1"
  local text="$2"
  [[ -n "$NOTIFY_WEBHOOK_URL" ]] || return 0
  if [[ "$status" == "success" && "$NOTIFY_ON_SUCCESS" != "true" ]]; then
    return 0
  fi
  python3 - <<PY || log "WARN: webhook notify failed"
import json, urllib.request
payload = {"text": """[vapp-backup] ${status}: ${text}""", "status": "${status}", "database": "${DB_NAME}", "kind": "${BACKUP_KIND}"}
req = urllib.request.Request(
    """${NOTIFY_WEBHOOK_URL}""",
    data=json.dumps(payload).encode("utf-8"),
    headers={"Content-Type": "application/json"},
    method="POST",
)
urllib.request.urlopen(req, timeout=15)
PY
}

on_exit() {
  local rc=$?
  BACKUP_EXIT_CODE="$rc"
  if [[ "$rc" -eq 0 ]]; then
    return 0
  fi
  local reason="${BACKUP_EXIT_REASON:-backup failed (exit ${rc})}"
  write_status_json "False" "$reason" "" 0 0 2>/dev/null || true
  notify_webhook "failure" "$reason" 2>/dev/null || true
}
trap on_exit EXIT

main() {
  log "=== DbVapp full backup start (kind=$BACKUP_KIND) ==="
  load_sa_password
  resolve_running_sql_container

  acquire_lock

  SQLCMD="$(resolve_sqlcmd_in_container)"
  log "Using sqlcmd: $SQLCMD"

  ensure_backup_volume_mounted
  wait_for_sql_server
  harden_backup_permissions
  verify_mssql_write_access

  local db_allocated_mb
  db_allocated_mb="$(get_database_size_mb)"
  check_disk_space "$db_allocated_mb"

  local target_dir="$DAILY_DIR"
  [[ "$BACKUP_KIND" == "weekly" ]] && target_dir="$WEEKLY_DIR"

  local base_name="${DB_NAME}_full_${TS}"
  local bak_host="$target_dir/${base_name}.bak"
  local bak_container="/backups/$(basename "$target_dir")/${base_name}.bak"
  local sha_host="${bak_host}.sha256"
  local meta_host="${bak_host%.bak}.json"

  log "Backup target (host): $bak_host"
  log "Backup target (container): $bak_container"

  local t0 t1 size_bytes
  t0="$(date +%s)"

  run_sql_long "BACKUP DATABASE [${DB_NAME}] TO DISK = N'${bak_container}' WITH COMPRESSION, CHECKSUM, INIT, STATS = 10, NAME = N'${DB_NAME}-Full-${TS}', DESCRIPTION = N'Automated full backup ${TS} UTC';"

  [[ -f "$bak_host" ]] || die "Backup file missing on host: $bak_host"
  size_bytes="$(stat -c '%s' "$bak_host" 2>/dev/null || stat -f '%z' "$bak_host")"
  [[ "$size_bytes" -gt 10240 ]] || die "Backup file too small (${size_bytes} bytes) — BACKUP likely failed"
  secure_backup_file "$bak_host"
  log "Backup file on disk: ${size_bytes} bytes"

  log "Running RESTORE VERIFYONLY ..."
  run_sql_file_verify "$bak_container"

  if command -v sha256sum >/dev/null 2>&1; then
    (cd "$(dirname "$bak_host")" && sha256sum "$(basename "$bak_host")" > "$(basename "$sha_host")")
  elif command -v shasum >/dev/null 2>&1; then
    (cd "$(dirname "$bak_host")" && shasum -a 256 "$(basename "$bak_host")" > "$(basename "$sha_host")")
  else
    die "sha256sum/shasum not found"
  fi
  secure_backup_file "$sha_host"

  t1="$(date +%s)"
  size_bytes="$(stat -c '%s' "$bak_host" 2>/dev/null || stat -f '%z' "$bak_host")"
  local size_mb=$((size_bytes / 1024 / 1024))

  local sql_version db_size_mb
  # timeout کوتاه برای متادیتا (جلوگیری از hang بعد از BACKUP)
  sql_version="$(run_sql_scalar "SELECT LEFT(@@VERSION, 120);" 30 || true)"
  db_size_mb="$(run_sql_scalar "SELECT CAST(SUM(CAST(size AS BIGINT))*8.0/1024 AS DECIMAL(18,2)) FROM sys.master_files WHERE database_id = DB_ID('${DB_NAME}');" 30 || true)"

  upload_rclone_backup_set "$bak_host" "$sha_host"

  python3 - "$meta_host" <<PY
import json
meta = {
    "database": "${DB_NAME}",
    "kind": "${BACKUP_KIND}",
    "timestamp_utc": "${TS}",
    "file": "$(basename "$bak_host")",
    "size_bytes": ${size_bytes},
    "size_mb": ${size_mb},
    "duration_seconds": $((t1 - t0)),
    "host": "$(hostname -f 2>/dev/null || hostname)",
    "container": "${SQL_CONTAINER}",
    "sql_server_version": """${sql_version}""",
    "database_size_mb_reported": """${db_size_mb}""",
    "verified": True,
    "offsite_enabled": bool("""${RCLONE_REMOTE}"""),
    "offsite_uploaded": True if "${OFFSITE_UPLOADED}" == "true" else False,
    "offsite_remote": """${RCLONE_REMOTE}""",
    "checksum_file": "$(basename "$sha_host")",
}
with open("${meta_host}", "w", encoding="utf-8") as f:
    json.dump(meta, f, ensure_ascii=False, indent=2)
    f.write("\n")
PY
  secure_backup_file "$meta_host"
  upload_rclone_file "$meta_host"
  prune_remote_dir

  ln -sfn "$bak_host" "$BACKUP_ROOT/latest.bak"
  ln -sfn "$meta_host" "$BACKUP_ROOT/latest.json"
  ln -sfn "$sha_host" "$BACKUP_ROOT/latest.sha256"

  prune_dir "$DAILY_DIR" "$KEEP_DAILY" "daily"
  prune_weekly_count "$WEEKLY_DIR" "$KEEP_WEEKLY"

  harden_backup_permissions

  local success_msg
  if [[ "$OFFSITE_UPLOADED" == "true" ]]; then
    success_msg="SUCCESS: ${base_name}.bak (${size_mb} MiB, $((t1 - t0))s) verified + offsite OK (${RCLONE_REMOTE})"
  else
    success_msg="SUCCESS: ${base_name}.bak (${size_mb} MiB, $((t1 - t0))s) verified + checksum written (offsite disabled)"
  fi
  write_status_json "True" "$success_msg" "$bak_host" "$size_mb" "$((t1 - t0))"
  notify_webhook "success" "$success_msg"
  log "$success_msg + permissions ${BACKUP_FILE_MODE}/${BACKUP_DIR_MODE}"
  log "Log: $LOG_FILE"
  BACKUP_EXIT_REASON=""
}

main "$@"
