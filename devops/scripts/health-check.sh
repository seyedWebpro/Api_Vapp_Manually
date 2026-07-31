#!/usr/bin/env bash
# Health-check — API + Admin + Public (فرم/گردونه)
#
# Usage:
#   bash health-check.sh
#   bash health-check.sh --with-domain
#   HEALTH_ATTEMPTS=8 HEALTH_SLEEP=8 bash health-check.sh   # after deploy/restart
set -euo pipefail

WITH_DOMAIN=0
[[ "${1:-}" == "--with-domain" ]] && WITH_DOMAIN=1
DOMAIN_HOST="${DOMAIN_HOST:-ok-sms.ir}"
HEALTH_ATTEMPTS="${HEALTH_ATTEMPTS:-1}"
HEALTH_SLEEP="${HEALTH_SLEEP:-8}"

check_once() {
  api="$(curl -sS -m 15 -o /dev/null -w '%{http_code}' http://127.0.0.1:8080/health 2>/dev/null || echo "000")"
  swagger="$(curl -sS -m 30 -o /dev/null -w '%{http_code}' http://127.0.0.1:8080/swagger/index.html 2>/dev/null || echo "000")"

  if [[ "$WITH_DOMAIN" == "1" ]]; then
    nginx_root="$(curl -sS -m 15 -o /dev/null -w '%{http_code}' -H "Host: $DOMAIN_HOST" http://127.0.0.1/ 2>/dev/null || echo "000")"
    public="$(curl -sS -m 15 -o /dev/null -w '%{http_code}' -H "Host: $DOMAIN_HOST" http://127.0.0.1/form/ 2>/dev/null || echo "000")"
    card="$(curl -sS -m 15 -o /dev/null -w '%{http_code}' -H "Host: $DOMAIN_HOST" http://127.0.0.1/card/ 2>/dev/null || echo "000")"
    wheel="$(curl -sS -m 15 -o /dev/null -w '%{http_code}' -H "Host: $DOMAIN_HOST" http://127.0.0.1/wheel/ 2>/dev/null || echo "000")"
    book="$(curl -sS -m 15 -o /dev/null -w '%{http_code}' -H "Host: $DOMAIN_HOST" http://127.0.0.1/book/ 2>/dev/null || echo "000")"
    domain_note="host:$DOMAIN_HOST"
  else
    nginx_root="$(curl -sS -m 15 -o /dev/null -w '%{http_code}' http://127.0.0.1/ 2>/dev/null || echo "000")"
    public="$(curl -sS -m 15 -o /dev/null -w '%{http_code}' http://127.0.0.1/form/ 2>/dev/null || echo "000")"
    card="$(curl -sS -m 15 -o /dev/null -w '%{http_code}' http://127.0.0.1/card/ 2>/dev/null || echo "000")"
    wheel="$(curl -sS -m 15 -o /dev/null -w '%{http_code}' http://127.0.0.1/wheel/ 2>/dev/null || echo "000")"
    book="$(curl -sS -m 15 -o /dev/null -w '%{http_code}' http://127.0.0.1/book/ 2>/dev/null || echo "000")"
    domain_note="ip-default"
  fi

  if [[ -f /var/www/vapp-admin/index.html ]]; then
    admin="$nginx_root"
    admin_mode="static"
  else
    admin="$(curl -sS -m 15 -o /dev/null -w '%{http_code}' http://127.0.0.1:3005/ 2>/dev/null || echo "000")"
    admin_mode="docker:3005"
  fi

  public_mode="static:/form+/wheel+/card+/book"
  [[ ! -f /var/www/vapp-public/index.html ]] && public_mode="docker:3006"

  echo "API:$api ADMIN:$admin($admin_mode) PUBLIC:$public CARD:$card WHEEL:$wheel BOOK:$book NGINX:$nginx_root($domain_note) SWAGGER:$swagger"
}

attempt=1
while true; do
  check_once
  if [[ "$api" == "200" && "$admin" == "200" && "$public" == "200" && "$card" == "200" && "$wheel" == "200" && "$book" == "200" ]]; then
    echo "OK: all services healthy"
    exit 0
  fi
  if (( attempt >= HEALTH_ATTEMPTS )); then
    break
  fi
  echo "WARN: not healthy yet — retry $attempt/$HEALTH_ATTEMPTS in ${HEALTH_SLEEP}s (API:$api)" >&2
  sleep "$HEALTH_SLEEP"
  attempt=$((attempt + 1))
done

echo "WARN: one or more checks failed" >&2
exit 1
