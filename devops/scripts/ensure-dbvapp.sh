#!/usr/bin/env bash
# Ensure SQL database DbVapp exists + optionally wait until EF Migrate finished.
# Fresh VPS only has master/model/msdb/tempdb — without DbVapp every /api/* returns 500
# while /health still returns 200.
#
# Usage (on server):
#   bash devops/scripts/ensure-dbvapp.sh
#   bash devops/scripts/ensure-dbvapp.sh --restart-api
#   bash devops/scripts/ensure-dbvapp.sh --restart-api --wait   # wait for AppVersion + migrations
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=lib/load-server-conf.sh
source "$SCRIPT_DIR/lib/load-server-conf.sh"

API_DIR="${API_DIR:-${REMOTE_API_REPO:-$HOME/Api_Vapp_Manually}}"
ENV_FILE="${ENV_FILE:-$API_DIR/docker/.env}"
SQL_CONTAINER="${SQL_CONTAINER:-vapp_sqlserver_prod}"
DB_NAME="${DB_NAME:-DbVapp}"
COMPOSE_FILE="${COMPOSE_FILE:-docker/docker-compose.production.yml}"
RESTART_API=0
WAIT_READY=0

for arg in "$@"; do
  case "$arg" in
    --restart-api) RESTART_API=1 ;;
    --wait) WAIT_READY=1 ;;
    -h|--help)
      sed -n '2,12p' "$0" | sed 's/^# \?//'
      exit 0
      ;;
  esac
done

log() { echo "[$(date -Is)] $*"; }
die() { echo "ERROR: $*" >&2; exit 1; }

[[ -f "$ENV_FILE" ]] || die "missing $ENV_FILE"
SA_PASSWORD="$(grep -E '^SA_PASSWORD=' "$ENV_FILE" | head -1 | cut -d= -f2-)"
[[ -n "$SA_PASSWORD" ]] || die "SA_PASSWORD empty in $ENV_FILE"

docker inspect "$SQL_CONTAINER" >/dev/null 2>&1 || die "container not found: $SQL_CONTAINER"

SQLCMD="$(docker exec "$SQL_CONTAINER" sh -c 'command -v sqlcmd || ls /opt/mssql-tools/bin/sqlcmd /opt/mssql-tools18/bin/sqlcmd 2>/dev/null | head -1')"
[[ -n "$SQLCMD" ]] || die "sqlcmd not found inside $SQL_CONTAINER"

log "=== ensure-dbvapp container=$SQL_CONTAINER db=$DB_NAME ==="

ok=0
for i in $(seq 1 45); do
  if docker exec "$SQL_CONTAINER" "$SQLCMD" -S localhost -U sa -P "$SA_PASSWORD" -C -Q "SELECT 1" -b -o /dev/null 2>/dev/null; then
    ok=1
    break
  fi
  log "SQL not ready ($i/45)..."
  sleep 2
done
[[ "$ok" == "1" ]] || die "SQL Server login failed — SA_PASSWORD must match the password used when the SQL volume was first created"

docker exec "$SQL_CONTAINER" "$SQLCMD" -S localhost -U sa -P "$SA_PASSWORD" -C -Q "
SET NOCOUNT ON;
IF DB_ID(N'$DB_NAME') IS NULL
BEGIN
  CREATE DATABASE [$DB_NAME];
  PRINT 'CREATED_$DB_NAME';
END
ELSE
  PRINT 'EXISTS_$DB_NAME';
"

docker exec "$SQL_CONTAINER" "$SQLCMD" -S localhost -U sa -P "$SA_PASSWORD" -C -Q "
SET NOCOUNT ON;
IF DB_ID(N'$DB_NAME') IS NOT NULL AND DATABASEPROPERTYEX(N'$DB_NAME', 'Status') <> N'ONLINE'
BEGIN
  ALTER DATABASE [$DB_NAME] SET ONLINE;
  PRINT 'SET_ONLINE_$DB_NAME';
END
SELECT name, state_desc FROM sys.databases WHERE name = N'$DB_NAME';
"

if [[ "$RESTART_API" == "1" ]]; then
  log "Restarting API so EF Migrate/Seed runs..."
  cd "$API_DIR"
  docker compose -f "$COMPOSE_FILE" --env-file "$ENV_FILE" \
    up -d --no-deps --force-recreate --no-build api
fi

if [[ "$WAIT_READY" == "1" ]]; then
  log "Waiting for Migrate + AppVersion..."
  MAX_ATTEMPTS="${MAX_ATTEMPTS:-48}" INTERVAL_SECS="${INTERVAL_SECS:-10}" \
    bash "$SCRIPT_DIR/wait-db-ready.sh"
fi

log "OK: ensure-dbvapp done"
