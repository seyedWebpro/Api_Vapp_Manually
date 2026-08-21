#!/usr/bin/env bash
# Health-check — API + AppVersion(DB) + Admin + Public
#
# Usage:
#   bash health-check.sh
#   bash health-check.sh --with-domain
#   bash health-check.sh --api-only
#   HEALTH_ATTEMPTS=8 HEALTH_SLEEP=8 bash health-check.sh
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=lib/deploy-fail.sh
source "$SCRIPT_DIR/lib/deploy-fail.sh" 2>/dev/null || true
# shellcheck source=lib/load-server-conf.sh
source "$SCRIPT_DIR/lib/load-server-conf.sh" 2>/dev/null || true
# shellcheck source=lib/nginx-http.sh
source "$SCRIPT_DIR/lib/nginx-http.sh"

WITH_DOMAIN=0
API_ONLY=0
for arg in "$@"; do
  case "$arg" in
    --with-domain) WITH_DOMAIN=1 ;;
    --api-only) API_ONLY=1 ;;
  esac
done

DOMAIN_HOST="${DOMAIN_HOST:-ok-sms.ir}"
SERVER_IP="${SERVER_IP:-195.24.237.132}"
HEALTH_ATTEMPTS="${HEALTH_ATTEMPTS:-1}"
HEALTH_SLEEP="${HEALTH_SLEEP:-8}"

check_once() {
  api="$(api_http_code http://127.0.0.1:8080/health)"
  appver="$(api_http_code 'http://127.0.0.1:8080/api/AppVersion/check?platform=android&currentVersion=1.0.0')"
  swagger="$(api_http_code http://127.0.0.1:8080/swagger/index.html)"

  local host_hdr="$SERVER_IP"
  if [[ "$WITH_DOMAIN" == "1" ]]; then
    host_hdr="$DOMAIN_HOST"
  fi

  nginx_root="$(nginx_http_code http://127.0.0.1/ "$host_hdr")"
  public="$(nginx_http_code http://127.0.0.1/form/x "$host_hdr")"
  card="$(nginx_http_code http://127.0.0.1/card/x "$host_hdr")"
  wheel="$(nginx_http_code http://127.0.0.1/wheel/x "$host_hdr")"
  book="$(nginx_http_code http://127.0.0.1/book/x "$host_hdr")"
  domain_note="host:$host_hdr"

  if [[ -f /var/www/vapp-admin/index.html ]]; then
    admin="$nginx_root"
    admin_mode="static"
  else
    admin="$(api_http_code http://127.0.0.1:3005/)"
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
  [[ "$appver" != "200" ]] && echo "• APPVER:$appver → bash devops/scripts/ensure-dbvapp.sh --restart-api --wait" >&2
  [[ "$admin" != "200" ]] && echo "• ADMIN:$admin → bash devops/scripts/deploy-front-host.sh" >&2
  if [[ "$public" != "200" || "$card" != "200" || "$wheel" != "200" || "$book" != "200" ]]; then
    if [[ -f /var/www/vapp-public/index.html ]]; then
      echo "• PUBLIC 502 با index موجود → bash devops/scripts/ensure-nginx-ok.sh" >&2
    fi
    echo "• PUBLIC form/wheel/card/book → SERVER_IP=$SERVER_IP bash devops/scripts/deploy-public-front-host.sh" >&2
  fi
  echo "• تشخیص کامل: bash devops/scripts/diagnose-deploy.sh" >&2
  echo "" >&2
}

is_healthy() {
  if [[ "$API_ONLY" == "1" ]]; then
    [[ "$api" == "200" && "$appver" == "200" ]]
    return
  fi
  [[ "$api" == "200" && "$appver" == "200" && "$admin" == "200" && "$public" == "200" && "$card" == "200" && "$wheel" == "200" && "$book" == "200" ]]
}

attempt=1
while true; do
  check_once
  if is_healthy; then
    if [[ "$API_ONLY" == "1" ]]; then
      echo "OK: API + AppVersion healthy"
    else
      echo "OK: all services healthy (incl. AppVersion/DB + Public)"
    fi
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
