#!/usr/bin/env bash
# Post-deploy verification for Vapp production stack.
#
# Usage (on server or via SSH from CI):
#   bash devops/scripts/post-deploy-verify.sh
#   bash devops/scripts/post-deploy-verify.sh --api-only
#   bash devops/scripts/post-deploy-verify.sh --skip-scraper
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=lib/load-server-conf.sh
source "$SCRIPT_DIR/lib/load-server-conf.sh" 2>/dev/null || true
# shellcheck source=lib/nginx-http.sh
source "$SCRIPT_DIR/lib/nginx-http.sh" 2>/dev/null || true

API_ONLY=0
WITH_DOMAIN=0
SKIP_SCRAPER=0
for arg in "$@"; do
  case "$arg" in
    --api-only) API_ONLY=1 ;;
    --with-domain) WITH_DOMAIN=1 ;;
    --skip-scraper) SKIP_SCRAPER=1 ;;
  esac
done

DOMAIN_HOST="${DOMAIN:-vapplication.ir}"
GATEWAY_HOST="${GATEWAY_HOST:-api.v-application.ir}"
NGINX_HOST="${SERVER_IP:-195.24.237.132}"

die() { echo "FAIL: $*" >&2; exit 1; }

echo "=== post-deploy-verify (Vapp) ==="
echo "domain: $DOMAIN_HOST | gateway: $GATEWAY_HOST | nginx Host: $NGINX_HOST"

echo "=== core API smoke ==="
api_health="$(api_http_code "http://127.0.0.1:8080/health")"
app_version="$(api_http_code "http://127.0.0.1:8080/api/AppVersion/check?platform=android&currentVersion=1.0.0")"
swagger="$(api_http_code "http://127.0.0.1:8080/swagger/index.html")"

echo "API /health              : $api_health"
echo "AppVersion check         : $app_version"
echo "Swagger                  : $swagger"

[[ "$api_health" == "200" ]] || die "/health != 200 (got $api_health)"
[[ "$app_version" == "200" ]] || die "AppVersion/check != 200 (got $app_version) — DB/migration issue?"

if [[ "$API_ONLY" == "1" ]]; then
  echo "OK: API-only verify passed"
  exit 0
fi

echo "=== admin + public static (Host: $NGINX_HOST) ==="
if [[ -f /var/www/vapp-admin/index.html ]]; then
  admin="$(nginx_http_code "http://127.0.0.1/" "$NGINX_HOST")"
  admin_mode="static:/var/www/vapp-admin"
else
  admin="$(api_http_code "http://127.0.0.1:3005/")"
  admin_mode="docker:3005"
fi

if [[ -f /var/www/vapp-public/index.html ]]; then
  public="$(nginx_http_code "http://127.0.0.1/form/x" "$NGINX_HOST")"
  public_mode="static:/var/www/vapp-public"
else
  public="$(api_http_code "http://127.0.0.1:3006/form/x")"
  public_mode="docker:3006"
fi

echo "Admin ($admin_mode)       : $admin"
echo "Public form ($public_mode): $public"

[[ "$admin" == "200" ]] || die "Admin front != 200 (got $admin)"
case "$public" in
  200|301|302|404) echo "OK: Public responds ($public)" ;;
  *) die "Public front unexpected $public (expect 2xx/3xx/404; 5xx=broken deploy)" ;;
esac

echo "=== admin API route (auth required) ==="
admin_stats="$(api_http_code "http://127.0.0.1:8080/api/Admin/Dashboard/stats")"
echo "Admin Dashboard/stats    : $admin_stats (expect 401 without token)"
[[ "$admin_stats" == "401" ]] || echo "WARN: expected 401 without token (got $admin_stats) — non-blocking"

if [[ "$SKIP_SCRAPER" == "1" ]]; then
  echo "SKIP: scraper (--skip-scraper)"
else
  echo "=== number scraper ==="
  scraper="$(api_http_code "http://127.0.0.1:8000/health")"
  echo "Scraper /health          : $scraper"
  [[ "$scraper" == "200" ]] || die "Scraper /health != 200 (got $scraper)"
fi

if [[ "$WITH_DOMAIN" == "1" ]]; then
  echo "=== public HTTPS ==="
  app_ssl="$(https_http_code "https://${DOMAIN_HOST}/")"
  gw_ssl="$(https_http_code "https://${GATEWAY_HOST}/health")"
  echo "HTTPS app                : $app_ssl"
  echo "HTTPS gateway /health    : $gw_ssl"
  [[ "$app_ssl" == "200" ]] || die "https://${DOMAIN_HOST}/ != 200"
  [[ "$gw_ssl" == "200" ]] || die "https://${GATEWAY_HOST}/health != 200"
fi

echo "OK: post-deploy-verify passed"
