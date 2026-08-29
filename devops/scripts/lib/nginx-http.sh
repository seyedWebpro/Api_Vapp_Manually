#!/usr/bin/env bash
# Shared Host-aware HTTP helpers for nginx SPA routes.
# Source from other scripts — do not execute directly.
#
# Why Host header matters:
#   curl http://127.0.0.1/form/... sends Host: 127.0.0.1
#   If server_name is only the public IP, nginx may hit another vhost → 502.
#   apply-nginx.sh includes 127.0.0.1 localhost + default_server; still always
#   probe with Host: $SERVER_IP for consistency with real clients.

: "${SERVER_IP:=195.24.237.132}"

# http_code URL [Host]
# Accepts 200, or 301/302 (HTTPS redirect after Certbot).
nginx_http_code() {
  local url="$1"
  local host="${2:-$SERVER_IP}"
  local code
  code="$(curl -sS -m 15 -o /dev/null -w '%{http_code}' -H "Host: ${host}" "$url" 2>/dev/null)" || code="000"
  [[ "$code" =~ ^[0-9]{3}$ ]] || code="000"
  # After Certbot: http://domain → 301 https://domain — treat as healthy
  if [[ "$code" == "301" || "$code" == "302" ]]; then
    code="200"
  fi
  printf '%s' "$code"
}

# https_code URL — public HTTPS probe (no Host override)
https_http_code() {
  local url="$1"
  local code
  code="$(curl -sS -m 20 -o /dev/null -w '%{http_code}' "$url" 2>/dev/null)" || code="000"
  [[ "$code" =~ ^[0-9]{3}$ ]] || code="000"
  printf '%s' "$code"
}

# verify_https_cert HOST — curl ssl_verify_result (0 = OK)
verify_https_cert() {
  local host="$1"
  local verify
  verify="$(curl -sS -o /dev/null -w '%{ssl_verify_result}' -m 20 "https://${host}/" 2>/dev/null)" || verify="1"
  [[ "$verify" == "0" ]]
}

# verify_https_cert_retry HOST [attempts] — wait for nginx reload to settle
verify_https_cert_retry() {
  local host="$1"
  local attempts="${2:-3}"
  local i
  for ((i = 1; i <= attempts; i++)); do
    if verify_https_cert "$host"; then
      return 0
    fi
    sleep 1
  done
  return 1
}

# Direct API (no Host needed)
api_http_code() {
  local url="$1"
  local code
  code="$(curl -sS -m 15 -o /dev/null -w '%{http_code}' "$url" 2>/dev/null)" || code="000"
  [[ "$code" =~ ^[0-9]{3}$ ]] || code="000"
  printf '%s' "$code"
}

# Verify Public SPA routes return 200 (or HTTPS redirect). Prints summary. Returns 0 on success.
verify_public_routes() {
  local host="${1:-$SERVER_IP}"
  local form wheel card book
  form="$(nginx_http_code "http://127.0.0.1/form/x" "$host")"
  wheel="$(nginx_http_code "http://127.0.0.1/wheel/x" "$host")"
  card="$(nginx_http_code "http://127.0.0.1/card/x" "$host")"
  book="$(nginx_http_code "http://127.0.0.1/book/x" "$host")"
  echo "PUBLIC form=$form wheel=$wheel card=$card book=$book (Host: $host)"
  [[ "$form" == "200" && "$wheel" == "200" && "$card" == "200" && "$book" == "200" ]]
}
