#!/usr/bin/env bash
# یک‌جا: SHA256 + VERIFYONLY + test-restore (بدون دست زدن به DbVapp)
#   bash verify-backup-set.sh
#   bash verify-backup-set.sh --file /path/to/DbVapp_full_....bak
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

BACKUP_ROOT="${BACKUP_ROOT:-$API_ROOT/backups}"
BAK_FILE=""

while [[ $# -gt 0 ]]; do
  case "$1" in
    --file) BAK_FILE="$2"; shift 2 ;;
    -h|--help)
      echo "Usage: $0 [--file PATH.bak]"
      exit 0
      ;;
    *) echo "Unknown: $1" >&2; exit 1 ;;
  esac
done

if [[ -z "$BAK_FILE" ]]; then
  [[ -e "$BACKUP_ROOT/latest.bak" ]] || { echo "ERROR: no latest.bak" >&2; exit 1; }
  BAK_FILE="$(readlink -f "$BACKUP_ROOT/latest.bak")"
fi
[[ -f "$BAK_FILE" ]] || { echo "ERROR: missing $BAK_FILE" >&2; exit 1; }

echo "=== 1/3 SHA256 ==="
if [[ -f "${BAK_FILE}.sha256" ]]; then
  (cd "$(dirname "$BAK_FILE")" && sha256sum -c "$(basename "${BAK_FILE}.sha256")")
else
  echo "WARN: no .sha256 sidecar"
fi

echo "=== 2/3 health-check ==="
bash "$SCRIPT_DIR/backup-health-check.sh"

echo "=== 3/3 test-restore (side DB) ==="
bash "$SCRIPT_DIR/test-restore-database.sh" --file "$BAK_FILE"

echo "OK: backup set fully verified — restorable"
