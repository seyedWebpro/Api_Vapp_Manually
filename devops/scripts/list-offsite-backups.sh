#!/usr/bin/env bash
# لیست بکاپ‌های offsite روی rclone
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

[[ -n "$RCLONE_REMOTE" ]] || { echo "ERROR: RCLONE_REMOTE empty in $BACKUP_ENV" >&2; exit 1; }
command -v rclone >/dev/null 2>&1 || { echo "ERROR: rclone not installed" >&2; exit 1; }

echo "=== Offsite backups: ${RCLONE_REMOTE} ==="
echo ""
echo "--- daily/ (last 10) ---"
rclone lsl "${RCLONE_REMOTE}/daily/" 2>/dev/null | grep "${DB_NAME}_full_.*\.bak" | tail -10 || echo "(none)"
echo ""
echo "--- weekly/ (last 5) ---"
rclone lsl "${RCLONE_REMOTE}/weekly/" 2>/dev/null | grep "${DB_NAME}_full_.*\.bak" | tail -5 || echo "(none)"
echo ""
echo "Total .bak on remote:"
echo -n "  daily:  "; rclone lsf "${RCLONE_REMOTE}/daily/" --files-only 2>/dev/null | grep -c "${DB_NAME}_full_.*\.bak" || echo 0
echo -n "  weekly: "; rclone lsf "${RCLONE_REMOTE}/weekly/" --files-only 2>/dev/null | grep -c "${DB_NAME}_full_.*\.bak" || echo 0
