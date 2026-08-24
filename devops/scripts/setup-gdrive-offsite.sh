#!/usr/bin/env bash
# آماده‌سازی backup.env برای offsite Google Drive (Vapp)
# پیش‌نیاز: rclone config با remote مثلاً vapp-gdrive
# راهنما: backup/GOOGLE_DRIVE_OFFSITE.md
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DEVOPS_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
BACKUP_ENV="${BACKUP_ENV:-$DEVOPS_ROOT/backup/backup.env}"
EXAMPLE="${DEVOPS_ROOT}/backup/backup.env.example"

RCLONE_REMOTE_NAME="${RCLONE_REMOTE_NAME:-vapp-gdrive}"
GDRIVE_FOLDER="${GDRIVE_FOLDER:-vapp-db-backups}"
DB_NAME="${DB_NAME:-DbVapp}"

die() { echo "ERROR: $*" >&2; exit 1; }
ok() { echo "OK: $*"; }

command -v rclone >/dev/null 2>&1 || die "rclone not installed. Run: sudo apt-get install -y rclone"

if [[ ! -f "${HOME}/.config/rclone/rclone.conf" ]]; then
  die "rclone remote not configured. Run: rclone config (see backup/GOOGLE_DRIVE_OFFSITE.md)"
fi

chmod 600 "${HOME}/.config/rclone/rclone.conf" 2>/dev/null || true

if ! rclone listremotes | grep -q "^${RCLONE_REMOTE_NAME}:$"; then
  echo "Available remotes:"
  rclone listremotes || true
  die "Remote '${RCLONE_REMOTE_NAME}:' not found. Set RCLONE_REMOTE_NAME or run rclone config"
fi

rclone lsd "${RCLONE_REMOTE_NAME}:" >/dev/null 2>&1 \
  || die "Cannot list ${RCLONE_REMOTE_NAME}: — check OAuth token (backup/GOOGLE_DRIVE_OFFSITE.md)"

mkdir -p "$(dirname "$BACKUP_ENV")"
if [[ ! -f "$BACKUP_ENV" ]]; then
  cp "$EXAMPLE" "$BACKUP_ENV"
  ok "created $BACKUP_ENV from example"
fi

REMOTE_VALUE="${RCLONE_REMOTE_NAME}:${GDRIVE_FOLDER}/${DB_NAME}"

upsert() {
  local var="$1" val="$2"
  local tmp
  tmp="$(mktemp)"
  grep -vE "^${var}=" "$BACKUP_ENV" >"$tmp" || true
  printf '%s=%s\n' "$var" "$val" >>"$tmp"
  mv "$tmp" "$BACKUP_ENV"
}

upsert "RCLONE_REMOTE" "$REMOTE_VALUE"
chmod 600 "$BACKUP_ENV"

ok "RCLONE_REMOTE=${REMOTE_VALUE} in ${BACKUP_ENV}"
echo ""
echo "Next:"
echo "  bash \"$SCRIPT_DIR/verify-rclone-offsite.sh\""
echo "  bash \"$SCRIPT_DIR/backup-database.sh\""
echo "  bash \"$SCRIPT_DIR/list-offsite-backups.sh\""
