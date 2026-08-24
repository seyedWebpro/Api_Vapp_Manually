#!/usr/bin/env bash
# سلامت بکاپ: سن latest، checksum، status.json
# سطح بالاتر از Microless — مانیتورینگ روزانه بعد از cron بکاپ
#
#   bash ~/Api_Vapp_Manually/devops/scripts/backup-health-check.sh
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

DB_NAME="${DB_NAME:-DbVapp}"
BACKUP_ROOT="${BACKUP_ROOT:-$API_ROOT/backups}"
MAX_BACKUP_AGE_HOURS="${MAX_BACKUP_AGE_HOURS:-30}"
NOTIFY_WEBHOOK_URL="${NOTIFY_WEBHOOK_URL:-}"
HEALTH_STATUS_FILE="$BACKUP_ROOT/health-status.json"

log() { echo "[$(date -u +%Y-%m-%dT%H:%M:%SZ)] $*"; }
die() {
  local msg="$1"
  log "ERROR: $msg"
  write_health "False" "$msg"
  notify_failure "$msg"
  exit 1
}

write_health() {
  local ok="$1" message="$2"
  python3 - "$HEALTH_STATUS_FILE" <<PY
import json
meta = {
    "ok": ${ok},
    "message": """${message}""",
    "database": "${DB_NAME}",
    "checked_at_utc": "$(date -u +%Y-%m-%dT%H:%M:%SZ)",
    "max_age_hours": int("${MAX_BACKUP_AGE_HOURS}"),
}
with open("""${HEALTH_STATUS_FILE}""", "w", encoding="utf-8") as f:
    json.dump(meta, f, ensure_ascii=False, indent=2)
    f.write("\n")
PY
  chmod 600 "$HEALTH_STATUS_FILE" 2>/dev/null || true
}

notify_failure() {
  local text="$1"
  [[ -n "$NOTIFY_WEBHOOK_URL" ]] || return 0
  python3 - <<PY || true
import json, urllib.request
payload = {"text": """[vapp-backup-health] failure: ${text}""", "status": "failure", "database": "${DB_NAME}"}
req = urllib.request.Request(
    """${NOTIFY_WEBHOOK_URL}""",
    data=json.dumps(payload).encode("utf-8"),
    headers={"Content-Type": "application/json"},
    method="POST",
)
urllib.request.urlopen(req, timeout=15)
PY
}

log "=== backup health check ==="

[[ -d "$BACKUP_ROOT" ]] || die "BACKUP_ROOT missing: $BACKUP_ROOT"

latest_bak="$BACKUP_ROOT/latest.bak"
[[ -L "$latest_bak" || -f "$latest_bak" ]] || die "latest.bak missing — no successful backup yet"

bak_real="$(readlink -f "$latest_bak" 2>/dev/null || realpath "$latest_bak" 2>/dev/null || echo "$latest_bak")"
[[ -f "$bak_real" ]] || die "latest.bak points to missing file: $bak_real"

age_sec=$(( $(date +%s) - $(stat -c '%Y' "$bak_real" 2>/dev/null || stat -f '%m' "$bak_real") ))
age_hours=$(( age_sec / 3600 ))
max_sec=$(( MAX_BACKUP_AGE_HOURS * 3600 ))

log "Latest: $bak_real"
log "Age: ${age_hours}h (max allowed ${MAX_BACKUP_AGE_HOURS}h)"

if (( age_sec > max_sec )); then
  die "Backup too old: ${age_hours}h > ${MAX_BACKUP_AGE_HOURS}h"
fi

sha_file="${bak_real}.sha256"
[[ -f "$sha_file" ]] || die "Checksum missing: $sha_file"

if command -v sha256sum >/dev/null 2>&1; then
  (cd "$(dirname "$bak_real")" && sha256sum -c "$(basename "$sha_file")") || die "SHA256 verify failed"
elif command -v shasum >/dev/null 2>&1; then
  (cd "$(dirname "$bak_real")" && shasum -a 256 -c "$(basename "$sha_file")") || die "SHA256 verify failed"
else
  die "sha256sum/shasum not found"
fi

size_bytes="$(stat -c '%s' "$bak_real" 2>/dev/null || stat -f '%z' "$bak_real")"
[[ "$size_bytes" -gt 1024 ]] || die "Backup file suspiciously small: ${size_bytes} bytes"

if [[ -f "$BACKUP_ROOT/status.json" ]]; then
  python3 - <<PY || die "status.json reports ok=false"
import json
with open("${BACKUP_ROOT}/status.json", encoding="utf-8") as f:
    s = json.load(f)
if not s.get("ok"):
    raise SystemExit(1)
print("status.json ok=true kind=%s file=%s" % (s.get("kind"), s.get("file")))
PY
fi

msg="healthy: $(basename "$bak_real") age=${age_hours}h size=$((size_bytes / 1024 / 1024))MiB"
write_health "True" "$msg"
log "OK: $msg"
