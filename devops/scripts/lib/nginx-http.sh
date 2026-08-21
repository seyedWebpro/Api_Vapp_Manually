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
nginx_http_code() {
  local url="$1"
  local host="${2:-$SERVER_IP}"
  local code
  code="$(curl -sS -m 15 -o /dev/null -w '%{http_code}' -H "Host: ${host}" "$url" 2>/dev/null)" || code="000"
  [[ "$code" =~ ^[0-9]{3}$ ]] || code="000"
  printf '%s' "$code"
}

# Direct API (no Host needed)
api_http_code() {
  local url="$1"
  local code
  code="$(curl -sS -m 15 -o /dev/null -w '%{http_code}' "$url" 2>/dev/null)" || code="000"
  [[ "$code" =~ ^[0-9]{3}$ ]] || code="000"
  printf '%s' "$code"
}

# Verify Public SPA routes return 200. Prints summary. Returns 0 on success.
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
