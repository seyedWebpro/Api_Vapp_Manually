#!/usr/bin/env bash
# نصب cron بکاپ روزانه + هفتگی + health-check برای DbVapp
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DEVOPS_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
API_ROOT="${API_ROOT:-$HOME/Api_Vapp_Manually}"
COMPOSE_FILE="${COMPOSE_FILE:-$API_ROOT/docker/docker-compose.production.yml}"
COMPOSE_ENV="${COMPOSE_ENV:-$API_ROOT/docker/.env}"

BACKUP_ENV="${BACKUP_ENV:-$DEVOPS_ROOT/backup/backup.env}"
if [[ -f "$BACKUP_ENV" ]]; then
  set -a
  # shellcheck disable=SC1090
  source "$BACKUP_ENV"
  set +a
fi

BACKUP_ROOT="${BACKUP_ROOT:-$API_ROOT/backups}"
CRON_DAILY="${CRON_DAILY:-0 3 * * *}"
CRON_WEEKLY="${CRON_WEEKLY:-15 3 * * 0}"
CRON_HEALTH="${CRON_HEALTH:-30 5 * * *}"

DAILY_SCRIPT="$SCRIPT_DIR/backup-database.sh"
WEEKLY_SCRIPT="$SCRIPT_DIR/backup-database-weekly.sh"
HEALTH_SCRIPT="$SCRIPT_DIR/backup-health-check.sh"
CRON_LOG="$BACKUP_ROOT/logs/cron.log"
HEALTH_LOG="$BACKUP_ROOT/logs/health-check.log"

chmod +x "$DAILY_SCRIPT" "$WEEKLY_SCRIPT" "$HEALTH_SCRIPT" \
  "$SCRIPT_DIR/restore-database.sh" \
  "$SCRIPT_DIR/test-restore-database.sh" \
  "$SCRIPT_DIR/verify-backup-set.sh" \
  "$SCRIPT_DIR/verify-rclone-offsite.sh" \
  "$SCRIPT_DIR/list-offsite-backups.sh" \
  "$SCRIPT_DIR/setup-gdrive-offsite.sh" \
  "$SCRIPT_DIR/download-backup-to-mac.sh" 2>/dev/null || true

mkdir -p "$BACKUP_ROOT/daily" "$BACKUP_ROOT/weekly" "$BACKUP_ROOT/logs" "$BACKUP_ROOT/restore"

SQL_CONTAINER="${SQL_CONTAINER:-vapp_sqlserver_prod}"
if ! docker ps --format '{{.Names}}' | grep -qx "$SQL_CONTAINER"; then
  _found="$(docker ps --format '{{.Names}}' | grep -E "^[0-9a-f]{12}_${SQL_CONTAINER}$" | head -1 || true)"
  [[ -n "$_found" ]] && SQL_CONTAINER="$_found"
fi

# اگر bind source حذف شده، کانتینر را دوباره mount کن
if [[ ! -d "$BACKUP_ROOT" ]] || ! docker exec "$SQL_CONTAINER" test -d /backups/daily 2>/dev/null; then
  echo "INFO: ensuring backups bind mount (force-recreate sqlserver) ..."
  mkdir -p "$BACKUP_ROOT/daily" "$BACKUP_ROOT/weekly" "$BACKUP_ROOT/logs" "$BACKUP_ROOT/restore"
  if [[ -f "$COMPOSE_FILE" ]]; then
    (cd "$(dirname "$COMPOSE_FILE")" && docker compose -f "$(basename "$COMPOSE_FILE")" --env-file "$COMPOSE_ENV" up -d --force-recreate sqlserver) \
      || echo "WARN: could not recreate sqlserver — recreate manually" >&2
    sleep 8
  fi
fi

MSSQL_UID="$(docker exec "$SQL_CONTAINER" id -u mssql 2>/dev/null || echo 10001)"
MSSQL_GID="$(docker exec "$SQL_CONTAINER" id -g mssql 2>/dev/null || echo 0)"
chown "${MSSQL_UID}:${MSSQL_GID}" "$BACKUP_ROOT/daily" "$BACKUP_ROOT/weekly" "$BACKUP_ROOT/restore" \
  || { echo "ERROR: chown backup dirs failed (uid=${MSSQL_UID} gid=${MSSQL_GID})" >&2; exit 1; }
chown root:root "$BACKUP_ROOT" "$BACKUP_ROOT/logs" 2>/dev/null || true
chmod 711 "$BACKUP_ROOT" 2>/dev/null || true
chmod 700 "$BACKUP_ROOT/daily" "$BACKUP_ROOT/weekly" "$BACKUP_ROOT/logs" "$BACKUP_ROOT/restore" 2>/dev/null || true

if docker ps --format '{{.Names}}' | grep -qx "$SQL_CONTAINER"; then
  docker exec -u mssql "$SQL_CONTAINER" bash -c \
    'test -d /backups/daily && touch /backups/daily/.perm_probe && rm -f /backups/daily/.perm_probe' \
    && echo "OK: mssql write probe /backups/daily" \
    || echo "WARN: mssql cannot write /backups/daily — recreate sqlserver after mkdir $BACKUP_ROOT" >&2
fi

MARKER="# vapp-db-backup-cron"
# BACKUP_ENV باید quote شود
DAILY_LINE="$CRON_DAILY cd \"$API_ROOT\" && BACKUP_ENV=\"$BACKUP_ENV\" bash \"$DAILY_SCRIPT\" >> \"$CRON_LOG\" 2>&1 $MARKER-daily"
WEEKLY_LINE="$CRON_WEEKLY cd \"$API_ROOT\" && BACKUP_ENV=\"$BACKUP_ENV\" bash \"$WEEKLY_SCRIPT\" >> \"$CRON_LOG\" 2>&1 $MARKER-weekly"
HEALTH_LINE="$CRON_HEALTH cd \"$API_ROOT\" && BACKUP_ENV=\"$BACKUP_ENV\" bash \"$HEALTH_SCRIPT\" >> \"$HEALTH_LOG\" 2>&1 $MARKER-health"

tmp="$(mktemp)"
crontab -l 2>/dev/null | grep -v "$MARKER" | grep -v 'backup-database.sh' | grep -v 'backup-database-weekly.sh' | grep -v 'backup-health-check.sh' >"$tmp" || true
{
  cat "$tmp"
  echo "$DAILY_LINE"
  echo "$WEEKLY_LINE"
  echo "$HEALTH_LINE"
} | crontab -
rm -f "$tmp"

if [[ ! -f "$BACKUP_ENV" && -f "$DEVOPS_ROOT/backup/backup.env.example" ]]; then
  cp "$DEVOPS_ROOT/backup/backup.env.example" "$BACKUP_ENV"
  chmod 600 "$BACKUP_ENV"
  echo "INFO: created $BACKUP_ENV from example (edit if needed)"
fi

echo "OK: cron installed"
echo "  daily:  $CRON_DAILY  -> backup-database.sh"
echo "  weekly: $CRON_WEEKLY -> backup-database-weekly.sh"
echo "  health: $CRON_HEALTH -> backup-health-check.sh"
echo "  log:    $CRON_LOG"
echo "  health: $HEALTH_LOG"
echo ""
crontab -l | grep "$MARKER" || true
