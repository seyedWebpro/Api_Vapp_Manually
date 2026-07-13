#!/usr/bin/env bash
# Health-check — API + Admin + Public (فرم/گردونه)
#
# Usage:
#   bash health-check.sh
#   bash health-check.sh --with-domain
set -euo pipefail

WITH_DOMAIN=0
[[ "${1:-}" == "--with-domain" ]] && WITH_DOMAIN=1
DOMAIN_HOST="${DOMAIN_HOST:-ok-sms.ir}"

api="$(curl -sS -m 15 -o /dev/null -w '%{http_code}' http://127.0.0.1:8080/health 2>/dev/null || echo "000")"
swagger="$(curl -sS -m 30 -o /dev/null -w '%{http_code}' http://127.0.0.1:8080/swagger/index.html 2>/dev/null || echo "000")"

if [[ "$WITH_DOMAIN" == "1" ]]; then
  nginx_root="$(curl -sS -m 15 -o /dev/null -w '%{http_code}' -H "Host: $DOMAIN_HOST" http://127.0.0.1/ 2>/dev/null || echo "000")"
  public="$(curl -sS -m 15 -o /dev/null -w '%{http_code}' -H "Host: $DOMAIN_HOST" http://127.0.0.1/form/ 2>/dev/null || echo "000")"
  domain_note="host:$DOMAIN_HOST"
else
  nginx_root="$(curl -sS -m 15 -o /dev/null -w '%{http_code}' http://127.0.0.1/ 2>/dev/null || echo "000")"
  public="$(curl -sS -m 15 -o /dev/null -w '%{http_code}' http://127.0.0.1/form/ 2>/dev/null || echo "000")"
  domain_note="ip-default"
fi

if [[ -f /var/www/vapp-admin/index.html ]]; then
  admin="$nginx_root"
  admin_mode="static"
else
  admin="$(curl -sS -m 15 -o /dev/null -w '%{http_code}' http://127.0.0.1:3005/ 2>/dev/null || echo "000")"
  admin_mode="docker:3005"
fi

public_mode="static:/form"
[[ ! -f /var/www/vapp-public/index.html ]] && public_mode="docker:3006"

echo "API:$api ADMIN:$admin($admin_mode) PUBLIC:$public($public_mode) NGINX:$nginx_root($domain_note) SWAGGER:$swagger"

if [[ "$api" == "200" && "$admin" == "200" && "$public" == "200" ]]; then
  echo "OK: all services healthy"
  exit 0
fi

echo "WARN: one or more checks failed" >&2
exit 1
