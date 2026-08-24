#!/usr/bin/env bash
# سوئیچ Vapp به دامنه ok-sms.ir (یا برگشت به IP)
#
# Usage (روی سرور):
#   bash devops/scripts/switch-to-domain.sh              # دامنه + https در .env
#   bash devops/scripts/switch-to-domain.sh --certbot  # + Certbot (DNS باید درست باشد)
#   bash devops/scripts/switch-to-domain.sh --http-only  # دامنه با http (قبل از SSL)
#   bash devops/scripts/switch-to-domain.sh --ip-only   # برگشت به IP
#
# Env:
#   DOMAIN_HOST (پیش‌فرض ok-sms.ir) — برای ساب‌دامین: DOMAIN_HOST=api.v-application.ir
#   SERVER_IP (پیش‌فرض از server.conf)
#   DOMAIN_SKIP_WWW=1 — اجباری برای رد کردن www (برای ساب‌دامین خودکار است)
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=lib/load-server-conf.sh
source "$SCRIPT_DIR/lib/load-server-conf.sh"
API_DIR="$(cd "$SCRIPT_DIR/../.." && pwd)"
DOMAIN_HOST="${DOMAIN_HOST:-${DOMAIN:-ok-sms.ir}}"
SERVER_IP="${SERVER_IP:-195.24.237.132}"
ENV_FILE="${ENV_FILE:-$API_DIR/docker/.env}"
COMPOSE_FILE="${COMPOSE_FILE:-docker/docker-compose.production.yml}"
SECRETS_FILE="${SECRETS_FILE:-$HOME/vapp-secrets.txt}"

MODE="domain-https"
RUN_CERTBOT=0
for arg in "$@"; do
  case "$arg" in
    --certbot) RUN_CERTBOT=1 ;;
    --http-only) MODE="domain-http" ;;
    --ip-only) MODE="ip" ;;
    -h|--help)
      sed -n '2,12p' "$0"
      exit 0
      ;;
  esac
done

read_var() {
  local key="$1" file="$2"
  grep -E "^${key}=" "$file" 2>/dev/null | tail -1 | cut -d= -f2- | tr -d '\r' || true
}

ensure_secrets() {
  local sa jwt
  [[ -f "$ENV_FILE" ]] || touch "$ENV_FILE"
  sa=$(read_var SA_PASSWORD "$ENV_FILE")
  jwt=$(read_var Jwt__Secret "$ENV_FILE")
  if [[ -z "$sa" && -f "$SECRETS_FILE" ]]; then
    sa=$(read_var SA_PASSWORD "$SECRETS_FILE")
  fi
  if [[ -z "$jwt" && -f "$SECRETS_FILE" ]]; then
    jwt=$(read_var Jwt__Secret "$SECRETS_FILE")
  fi
  [[ -n "$sa" ]] || { echo "ERROR: SA_PASSWORD missing in $ENV_FILE or $SECRETS_FILE" >&2; exit 1; }
  [[ -n "$jwt" ]] || { echo "ERROR: Jwt__Secret missing" >&2; exit 1; }
  SEC_SA="$sa"
  SEC_JWT="$jwt"
}

write_env_ip() {
  ensure_secrets
  upsert_env SA_PASSWORD "$SEC_SA"
  upsert_env API_PORT_MAPPING "127.0.0.1:8080:8080"
  upsert_env PUBLIC_API_BASE_URL "http://${SERVER_IP}"
  upsert_env PUBLIC_FRONTEND_URL "http://${SERVER_IP}"
  upsert_env FORM_PUBLIC_BASE_URL "http://${SERVER_IP}/form"
  upsert_env WHEEL_PUBLIC_BASE_URL "http://${SERVER_IP}/wheel"
  upsert_env CARD_PUBLIC_BASE_URL "http://${SERVER_IP}/card"
  upsert_env BOOKING_PUBLIC_BASE_URL "http://${SERVER_IP}/book"
  upsert_env Jwt__Secret "$SEC_JWT"
  chmod 600 "$ENV_FILE"
}

write_env_domain() {
  local scheme="$1"
  ensure_secrets
  upsert_env SA_PASSWORD "$SEC_SA"
  upsert_env API_PORT_MAPPING "127.0.0.1:8080:8080"
  upsert_env PUBLIC_API_BASE_URL "${scheme}://${DOMAIN_HOST}"
  upsert_env PUBLIC_FRONTEND_URL "${scheme}://${DOMAIN_HOST}"
  upsert_env FORM_PUBLIC_BASE_URL "${scheme}://${DOMAIN_HOST}/form"
  upsert_env WHEEL_PUBLIC_BASE_URL "${scheme}://${DOMAIN_HOST}/wheel"
  upsert_env CARD_PUBLIC_BASE_URL "${scheme}://${DOMAIN_HOST}/card"
  upsert_env BOOKING_PUBLIC_BASE_URL "${scheme}://${DOMAIN_HOST}/book"
  upsert_env Jwt__Secret "$SEC_JWT"
  # زرین‌پال — callback روی همین دامنه/ساب‌دامین
  upsert_env Payment__UseSimulation "false"
  upsert_env ZarinPal__CallbackUrl "${scheme}://${DOMAIN_HOST}/api/Payment/callback/zarinpal"
  upsert_env ZarinPal__Sandbox "false"
  upsert_env ZarinPal__AllowSandboxAutoVerify "false"
  upsert_env ZarinPal__Currency "IRT"
  upsert_env ZarinPal__AppReturnUrl "vapp://payment/result"
  chmod 600 "$ENV_FILE"
}

upsert_env() {
  local key="$1" value="$2"
  [[ -f "$ENV_FILE" ]] || touch "$ENV_FILE"
  python3 -c '
import sys
from pathlib import Path
path = Path(sys.argv[1]); key = sys.argv[2]; value = sys.argv[3]
lines = path.read_text(encoding="utf-8").splitlines() if path.exists() else []
out=[]; found=False
for line in lines:
    if line.startswith(key + "="):
        out.append(f"{key}={value}"); found=True
    else:
        out.append(line)
if not found:
    out.append(f"{key}={value}")
path.write_text("\n".join(out) + "\n", encoding="utf-8")
' "$ENV_FILE" "$key" "$value"
}

restart_api() {
  cd "$API_DIR"
  docker compose -f "$COMPOSE_FILE" --env-file "$ENV_FILE" up -d --no-deps --force-recreate api
  sleep 20
}

apply_nginx() {
  local dh=""
  [[ "$MODE" != "ip" ]] && dh="$DOMAIN_HOST"
  PUBLIC_STATIC_ROOT=/var/www/vapp-public SERVER_IP="$SERVER_IP" DOMAIN_HOST="$dh" \
    bash "$SCRIPT_DIR/apply-nginx.sh"
}

run_certbot() {
  if ! command -v certbot >/dev/null 2>&1; then
    apt-get update -qq && apt-get install -y -qq certbot python3-certbot-nginx
  fi
  local resolved
  resolved=$(dig +short "$DOMAIN_HOST" A @8.8.8.8 2>/dev/null | head -1 || true)
  if [[ -z "$resolved" ]]; then
    resolved=$(getent ahostsv4 "$DOMAIN_HOST" 2>/dev/null | awk "{print \$1; exit}" || true)
  fi
  if [[ "$resolved" != "$SERVER_IP" ]]; then
    echo "WARN: DNS $DOMAIN_HOST → '$resolved' (expected $SERVER_IP). Certbot skipped." >&2
    echo "      Fix DNS then: sudo certbot --nginx -d $DOMAIN_HOST --redirect" >&2
    return 1
  fi
  # ساب‌دامین (api.example.ir) یا DOMAIN_SKIP_WWW=1 → فقط خود دامنه
  if [[ "${DOMAIN_SKIP_WWW:-0}" == "1" ]] || [[ "$DOMAIN_HOST" == *.*.* ]]; then
    certbot --nginx -d "$DOMAIN_HOST" \
      --non-interactive --agree-tos --register-unsafely-without-email --redirect
  else
    certbot --nginx -d "$DOMAIN_HOST" -d "www.$DOMAIN_HOST" \
      --non-interactive --agree-tos --register-unsafely-without-email --redirect
  fi
}

case "$MODE" in
  ip)
    echo "=== Switch to IP: $SERVER_IP ==="
    write_env_ip
    apply_nginx
    restart_api
    ;;
  domain-http)
    echo "=== Switch to domain (HTTP): $DOMAIN_HOST ==="
    write_env_domain "http"
    apply_nginx
    restart_api
    ;;
  domain-https)
    echo "=== Switch to domain (HTTPS in .env): $DOMAIN_HOST ==="
    write_env_domain "https"
    apply_nginx
    restart_api
    if [[ "$RUN_CERTBOT" == "1" ]]; then
      run_certbot || true
    fi
    ;;
esac

bash "$SCRIPT_DIR/health-check.sh" $([[ "$MODE" != "ip" ]] && echo --with-domain)
echo ""
echo "OK: mode=$MODE domain=${DOMAIN_HOST:-—}"
docker exec vapp_api_prod printenv 2>/dev/null | grep -E 'PublicBaseUrl|FORM_|WHEEL_|CARD_|BOOKING_' | sort || true
