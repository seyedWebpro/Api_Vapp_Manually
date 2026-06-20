#!/usr/bin/env bash
# cron بکاپ روزانه/هفتگی
# reuse: CRON_DAILY/WEEKLY، مسیر اسکریپت، MARKER cron
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DEVOPS_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
API_ROOT="${API_ROOT:-$HOME/Api_Vapp_Manually}"

BACKUP_ENV="${BACKUP_ENV:-$DEVOPS_ROOT/backup/backup.env}"
BACKUP_ROOT="${BACKUP_ROOT:-$API_ROOT/backups}"
CRON_DAILY="${CRON_DAILY:-0 3 * * *}"
CRON_WEEKLY="${CRON_WEEKLY:-15 3 * * 0}"

DAILY_SCRIPT="$SCRIPT_DIR/backup-database.sh"
WEEKLY_SCRIPT="$SCRIPT_DIR/backup-database-weekly.sh"
CRON_LOG="$BACKUP_ROOT/logs/cron.log"

chmod +x "$DAILY_SCRIPT" "$WEEKLY_SCRIPT" 2>/dev/null || true
mkdir -p "$BACKUP_ROOT/daily" "$BACKUP_ROOT/weekly" "$BACKUP_ROOT/logs"

MARKER="# vapp-db-backup-cron"
DAILY_LINE="$CRON_DAILY cd $API_ROOT && /usr/bin/env BACKUP_ENV=$BACKUP_ENV bash \"$DAILY_SCRIPT\" >> \"$CRON_LOG\" 2>&1 $MARKER-daily"
WEEKLY_LINE="$CRON_WEEKLY cd $API_ROOT && /usr/bin/env BACKUP_ENV=$BACKUP_ENV bash \"$WEEKLY_SCRIPT\" >> \"$CRON_LOG\" 2>&1 $MARKER-weekly"

tmp="$(mktemp)"
crontab -l 2>/dev/null | grep -v "$MARKER" | grep -v 'backup-database' >"$tmp" || true
{
  cat "$tmp"
  echo "$DAILY_LINE"
  echo "$WEEKLY_LINE"
} | crontab -
rm -f "$tmp"

echo "OK: cron installed"
echo "  daily:  $CRON_DAILY"
echo "  weekly: $CRON_WEEKLY"
echo "  log:    $CRON_LOG"
crontab -l | grep "$MARKER" || true
