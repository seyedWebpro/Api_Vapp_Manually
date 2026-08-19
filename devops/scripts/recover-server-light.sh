#!/usr/bin/env bash
# Recover Vapp server after OOM/hang — scraper بدون SQL + restart سرویس‌ها
#
# Usage (روی سرور بعد از SSH یا hard-reboot از پنل هاستینگ):
#   bash ~/Api_Vapp_Manually/devops/scripts/recover-server-light.sh
#
# Env:
#   SKIP_VAPP=1   فقط scraper (بدون restart Vapp API)
#   SKIP_SCRAPER=1 فقط Vapp
set -euo pipefail

VAPP_DIR="${VAPP_DIR:-$HOME/Api_Vapp_Manually}"
SCRAPER_DIR="${SCRAPER_DIR:-$HOME/scraping_Number_Vapp}"
VAPP_ENV="${VAPP_ENV:-$VAPP_DIR/docker/.env}"
SCRAPER_ENV="${SCRAPER_ENV:-$SCRAPER_DIR/.env}"
SCRAPER_COMPOSE="${SCRAPER_COMPOSE:-docker-compose.production-api-only.yml}"

log() { echo "[$(date '+%Y-%m-%dT%H:%M:%S%z')] $*"; }

log "=== recover-server-light started ==="
log "free -h:"; free -h || true
log "docker ps:"; docker ps --format 'table {{.Names}}\t{{.Status}}' 2>/dev/null || true

# --- 1) خاموش کردن SQL ربات (RAM آزاد) ---
if docker ps -a --format '{{.Names}}' 2>/dev/null | grep -qx phonescraper_sqlserver_prod; then
  log "Stopping phonescraper_sqlserver_prod (optional scraper DB)..."
  docker update --restart=no phonescraper_sqlserver_prod 2>/dev/null || true
  docker stop phonescraper_sqlserver_prod 2>/dev/null || true
  log "Scraper SQL stopped."
else
  log "phonescraper_sqlserver_prod not found — skip."
fi

# --- 2) Scraper API-only (بدون DB) ---
if [[ "${SKIP_SCRAPER:-0}" != "1" && -d "$SCRAPER_DIR" ]]; then
  log "Redeploying scraper (api-only, no SQL)..."
  cd "$SCRAPER_DIR"
  if [[ ! -f "$SCRAPER_ENV" ]]; then
    log "WARN: missing $SCRAPER_ENV — skip scraper redeploy"
  elif [[ ! -f "$SCRAPER_COMPOSE" ]]; then
    log "WARN: missing $SCRAPER_COMPOSE — using deploy-api.sh"
    ALLOW_SLOW_START=1 bash "$SCRAPER_DIR/devops/scripts/deploy-api.sh" || true
  else
    docker compose -f "$SCRAPER_COMPOSE" --env-file "$SCRAPER_ENV" up -d --no-deps --force-recreate --no-build api 2>/dev/null \
      || docker compose -f "$SCRAPER_COMPOSE" --env-file "$SCRAPER_ENV" up -d --force-recreate api
    sleep 15
    scraper_code="$(curl -sS -m 15 -o /dev/null -w '%{http_code}' http://127.0.0.1:8000/health 2>/dev/null || echo 000)"
    log "Scraper health: $scraper_code"
  fi
else
  log "SKIP_SCRAPER=1 or repo missing — skip scraper."
fi

# --- 3) Restart Vapp stack (API + SQL Vapp) ---
if [[ "${SKIP_VAPP:-0}" != "1" && -d "$VAPP_DIR" ]]; then
  log "Restarting Vapp API..."
  cd "$VAPP_DIR"
  docker compose -f docker/docker-compose.production.yml --env-file "$VAPP_ENV" \
    up -d --no-deps --force-recreate --no-build api

  log "Ensuring Vapp SQL is up..."
  docker compose -f docker/docker-compose.production.yml --env-file "$VAPP_ENV" \
    up -d sqlserver 2>/dev/null || true

  log "Waiting for Vapp API (up to 90s)..."
  for i in $(seq 1 9); do
    sleep 10
    api_code="$(curl -sS -m 15 -o /dev/null -w '%{http_code}' http://127.0.0.1:8080/health 2>/dev/null || echo 000)"
    log "Vapp health attempt $i/9: $api_code"
    [[ "$api_code" == "200" ]] && break
  done
else
  log "SKIP_VAPP=1 or repo missing — skip Vapp."
fi

# --- 4) Health summary ---
log "=== final status ==="
docker ps --format 'table {{.Names}}\t{{.Status}}\t{{.Ports}}' 2>/dev/null || true
free -h || true

if [[ -x "$VAPP_DIR/devops/scripts/health-check.sh" ]]; then
  HEALTH_ATTEMPTS=3 HEALTH_SLEEP=10 bash "$VAPP_DIR/devops/scripts/health-check.sh" || true
fi

if [[ -x "$SCRAPER_DIR/devops/scripts/health-check.sh" ]]; then
  bash "$SCRAPER_DIR/devops/scripts/health-check.sh" 2>/dev/null || true
fi

log "=== recover-server-light done ==="
