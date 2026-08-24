#!/usr/bin/env bash
# تست اتصال offsite (rclone) قبل/بعد از فعال‌سازی بکاپ ابری
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DEVOPS_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

BACKUP_ENV="${BACKUP_ENV:-$DEVOPS_ROOT/backup/backup.env}"
if [[ -f "$BACKUP_ENV" ]]; then
  set -a
  # shellcheck disable=SC1090
  source "$BACKUP_ENV"
  set +a
fi

RCLONE_REMOTE="${RCLONE_REMOTE:-}"
DB_NAME="${DB_NAME:-DbVapp}"

die() { echo "ERROR: $*" >&2; exit 1; }
ok() { echo "OK: $*"; }

[[ -n "$RCLONE_REMOTE" ]] || die "RCLONE_REMOTE is empty. Set it in $BACKUP_ENV (example: vapp-backups:DbVapp)"
command -v rclone >/dev/null 2>&1 || die "rclone not installed. Run: sudo apt-get install -y rclone"

if [[ -f "${HOME}/.config/rclone/rclone.conf" ]]; then
  chmod 600 "${HOME}/.config/rclone/rclone.conf" 2>/dev/null || true
fi

echo "=== rclone offsite verify ==="
echo "Remote: $RCLONE_REMOTE"

rclone lsd "${RCLONE_REMOTE}/" >/dev/null 2>&1 \
  || rclone mkdir "${RCLONE_REMOTE}/" 2>/dev/null \
  || die "Cannot access remote: $RCLONE_REMOTE (run: rclone config)"

TS="$(date -u +%Y%m%d_%H%M%S)"
PROBE_LOCAL="/tmp/${DB_NAME}_rclone_probe_${TS}.txt"
PROBE_NAME="$(basename "$PROBE_LOCAL")"
PROBE_REMOTE="${RCLONE_REMOTE}/_probe/${PROBE_NAME}"

printf 'vapp-offsite-probe %s\n' "$TS" >"$PROBE_LOCAL"
chmod 600 "$PROBE_LOCAL"

rclone copyto "$PROBE_LOCAL" "$PROBE_REMOTE" --checksum
REMOTE_SIZE="$(rclone lsl "$PROBE_REMOTE" 2>/dev/null | awk '{print $1}')"
LOCAL_SIZE="$(wc -c <"$PROBE_LOCAL" | tr -d ' ')"

[[ -n "$REMOTE_SIZE" && "$REMOTE_SIZE" == "$LOCAL_SIZE" ]] \
  || die "Probe upload size mismatch (local=$LOCAL_SIZE remote=$REMOTE_SIZE)"

rclone delete "$PROBE_REMOTE" 2>/dev/null || true
rm -f "$PROBE_LOCAL"

ok "remote reachable + upload/delete probe passed"
ok "daily path: ${RCLONE_REMOTE}/daily/"
ok "weekly path: ${RCLONE_REMOTE}/weekly/"
echo "Next: bash \"$SCRIPT_DIR/backup-database.sh\" and check log for 'Uploading to rclone' + 'Offsite verify passed'"
