#!/usr/bin/env bash
# Health-check — curl به پورت‌های local
# reuse: URLها و کدهای HTTP مورد انتظار هر سرویس
#
# Usage:
#   bash health-check.sh
set -euo pipefail

api="$(curl -sS -m 15 -o /dev/null -w '%{http_code}' http://127.0.0.1:8080/health 2>/dev/null || echo "000")"
front="$(curl -sS -m 15 -o /dev/null -w '%{http_code}' http://127.0.0.1:3005/ 2>/dev/null || echo "000")"
public="$(curl -sS -m 15 -o /dev/null -w '%{http_code}' http://127.0.0.1/ 2>/dev/null || echo "000")"
swagger="$(curl -sS -m 30 -o /dev/null -w '%{http_code}' http://127.0.0.1:8080/swagger/index.html 2>/dev/null || echo "000")"

echo "API:$api FRONT:$front NGINX:$public SWAGGER:$swagger"

if [[ "$api" == "200" && "$front" == "200" ]]; then
  echo "OK: core services healthy"
  exit 0
fi

echo "WARN: one or more checks failed" >&2
if [[ "$api" == "000" || "$front" == "000" ]]; then
  echo "  tip: بلافاصله بعد recreate → ۳۰–۶۰ ثانیه صبر و دوباره health-check" >&2
fi
exit 1
