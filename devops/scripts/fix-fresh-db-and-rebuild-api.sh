#!/usr/bin/env bash
# ترمیم سرور تازه: DbVapp + wait for Migrate/AppVersion (+ optional rebuild اگر Program.cs پچ لازم باشد)
#
# Usage:
#   bash devops/scripts/fix-fresh-db-and-rebuild-api.sh           # فقط ensure + restart + wait
#   bash devops/scripts/fix-fresh-db-and-rebuild-api.sh --rebuild # build image هم
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
API_DIR="${API_DIR:-$(cd "$SCRIPT_DIR/../.." && pwd)}"
ENV_FILE="${ENV_FILE:-$API_DIR/docker/.env}"
COMPOSE_FILE="${COMPOSE_FILE:-docker/docker-compose.production.yml}"
REBUILD=0
[[ "${1:-}" == "--rebuild" ]] && REBUILD=1

log() { echo "[$(date -Is)] $*"; }
die() { echo "ERROR: $*" >&2; exit 1; }

cd "$API_DIR"
[[ -f "$ENV_FILE" ]] || die "missing $ENV_FILE"

log "1) ensure DbVapp + restart API"
bash "$SCRIPT_DIR/ensure-dbvapp.sh" --restart-api

if [[ "$REBUILD" == "1" ]]; then
  log "2) rebuild API image"
  docker compose -f "$COMPOSE_FILE" --env-file "$ENV_FILE" build --pull=false api
  docker compose -f "$COMPOSE_FILE" --env-file "$ENV_FILE" up -d --no-deps --force-recreate --no-build api
fi

log "3) wait for migrations + AppVersion"
MAX_ATTEMPTS=48 INTERVAL_SECS=10 bash "$SCRIPT_DIR/wait-db-ready.sh"

log "4) full health-check"
HEALTH_ATTEMPTS=3 HEALTH_SLEEP=5 bash "$SCRIPT_DIR/health-check.sh" || true

log "=== SUCCESS ==="
