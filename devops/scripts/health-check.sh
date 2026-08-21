#!/usr/bin/env bash
# Health-check — API + AppVersion(DB) + Admin + Public
#
# Usage:
#   bash health-check.sh
#   bash health-check.sh --with-domain
#   HEALTH_ATTEMPTS=8 HEALTH_SLEEP=8 bash health-check.sh
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=lib/deploy-fail.sh
source "$SCRIPT_DIR/lib/deploy-fail.sh" 2>/dev/null || true
# shellcheck source=lib/load-server-conf.sh
source "$SCRIPT_DIR/lib/load-server-conf.sh" 2>/dev/null || true

WITH_DOMAIN=0
[[ "${1:-}" == "--with-domain" ]] && WITH_DOMAIN=1
DOMAIN_HOST="${DOMAIN_HOST:-ok-sms.ir}"
SERVER_IP="${SERVER_IP:-195.24.237.132}"
HEALTH_ATTEMPTS="${HEALTH_ATTEMPTS:-1}"
HEALTH_SLEEP="${HEALTH_SLEEP:-8}"

check_once() {
  api="$(curl -sS -m 15 -o /dev/null -w '%{http_code}' http://127.0.0.1:8080/health 2>/dev/null || echo "000")"
  appver="$(curl -sS -m 15 -o /dev/null -w '%{http_code}' 'http://127.0.0.1:8080/api/AppVersion/check?platform=android&currentVersion=1.0.0' 2>/dev/null || echo "000")"
  swagger="$(curl -sS -m 30 -o /dev/null -w '%{http_code}' http://127.0.0.1:8080/swagger/index.html 2>/dev/null || echo "000")"

  # Always set Host so nginx server_name matches (fixes false 502 on 127.0.0.1)
  local host_hdr="$SERVER_IP"
  if [[ "$WITH_DOMAIN" == "1" ]]; then
    host_hdr="$DOMAIN_HOST"
  fi

  nginx_root="$(curl -sS -m 15 -o /dev/null -w '%{http_code}' -H "Host: $host_hdr" http://127.0.0.1/ 2>/dev/null || echo "000")"
  public="$(curl -sS -m 15 -o /dev/null -w '%{http_code}' -H "Host: $host_hdr" http://127.0.0.1/form/x 2>/dev/null || echo "000")"
  card="$(curl -sS -m 15 -o /dev/null -w '%{http_code}' -H "Host: $host_hdr" http://127.0.0.1/card/x 2>/dev/null || echo "000")"
  wheel="$(curl -sS -m 15 -o /dev/null -w '%{http_code}' -H "Host: $host_hdr" http://127.0.0.1/wheel/x 2>/dev/null || echo "000")"
  book="$(curl -sS -m 15 -o /dev/null -w '%{http_code}' -H "Host: $host_hdr" http://127.0.0.1/book/x 2>/dev/null || echo "000")"
  domain_note="host:$host_hdr"

  if [[ -f /var/www/vapp-admin/index.html ]]; then
    admin="$nginx_root"
    admin_mode="static"
  else
    admin="$(curl -sS -m 15 -o /dev/null -w '%{http_code}' http://127.0.0.1:3005/ 2>/dev/null || echo "000")"
    admin_mode="docker:3005"
  fi

  public_mode="static:/form+/wheel+/card+/book"
  [[ ! -f /var/www/vapp-public/index.html ]] && public_mode="docker:3006"

  echo "API:$api APPVER:$appver ADMIN:$admin($admin_mode) PUBLIC:$public CARD:$card WHEEL:$wheel BOOK:$book NGINX:$nginx_root($domain_note) SWAGGER:$swagger"
}

print_fix_hints() {
  echo "" >&2
  echo "── چرا fail شد / چه بزنید ──" >&2
  [[ "$api" != "200" ]] && echo "• API:$api → docker logs vapp_api_prod | bash devops/scripts/deploy-api.sh" >&2
  [[ "$appver" != "200" ]] && echo "• APPVER:$appver → DbVapp/Migrate: bash devops/scripts/ensure-dbvapp.sh --restart-api --wait" >&2
  [[ "$admin" != "200" ]] && echo "• ADMIN:$admin → bash devops/scripts/deploy-front-host.sh" >&2
  if [[ "$public" != "200" || "$card" != "200" || "$wheel" != "200" || "$book" != "200" ]]; then
    echo "• PUBLIC form/wheel/card/book → bash devops/scripts/deploy-public-front-host.sh" >&2
  fi
  echo "• تشخیص کامل: bash devops/scripts/diagnose-deploy.sh" >&2
  echo "" >&2
}

attempt=1
while true; do
  check_once
  if [[ "$api" == "200" && "$appver" == "200" && "$admin" == "200" && "$public" == "200" && "$card" == "200" && "$wheel" == "200" && "$book" == "200" ]]; then
    echo "OK: all services healthy (incl. AppVersion/DB + Public)"
    exit 0
  fi
  if (( attempt >= HEALTH_ATTEMPTS )); then
    break
  fi
  echo "WARN: not healthy yet — retry $attempt/$HEALTH_ATTEMPTS in ${HEALTH_SLEEP}s (API:$api APPVER:$appver PUBLIC:$public)" >&2
  sleep "$HEALTH_SLEEP"
  attempt=$((attempt + 1))
done

echo "FAIL: health-check incomplete" >&2
print_fix_hints
exit 1
