#!/usr/bin/env bash
# Set / check ZOHAL_API_TOKEN in docker/.env (never committed to git)
#
# Usage (on server):
#   bash devops/scripts/ensure-zohal-token.sh --check
#   bash devops/scripts/ensure-zohal-token.sh --set 'YOUR_TOKEN_FROM_DASHBOARD'
#   bash devops/scripts/ensure-zohal-token.sh --set 'TOKEN' --restart
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=lib/load-server-conf.sh
source "$SCRIPT_DIR/lib/load-server-conf.sh" 2>/dev/null || true

API_DIR="${API_DIR:-${REMOTE_API_REPO:-$HOME/Api_Vapp_Manually}}"
ENV_FILE="${ENV_FILE:-$API_DIR/docker/.env}"
COMPOSE_FILE="${COMPOSE_FILE:-$API_DIR/docker/docker-compose.production.yml}"
TOKEN=""
DO_SET=0
DO_CHECK=1
DO_RESTART=0

usage() {
  sed -n '3,8p' "$0" | sed 's/^# \?//'
  exit "${1:-0}"
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --check) DO_CHECK=1; DO_SET=0 ;;
    --set)
      DO_SET=1
      shift
      TOKEN="${1:-}"
      ;;
    --set=*)
      DO_SET=1
      TOKEN="${1#*=}"
      ;;
    --restart) DO_RESTART=1 ;;
    -h|--help) usage 0 ;;
    *)
      echo "ERROR: unknown option: $1" >&2
      usage 1
      ;;
  esac
  shift
done

mkdir -p "$(dirname "$ENV_FILE")"
[[ -f "$ENV_FILE" ]] || touch "$ENV_FILE"

read_token() {
  grep -E '^ZOHAL_API_TOKEN=' "$ENV_FILE" 2>/dev/null | head -1 | cut -d= -f2- || true
}

token_ok() {
  local t="$1"
  [[ -n "$t" && "$t" != "CHANGE_ME_ZOHAL_TOKEN" && "$t" != "your-token" && "$t" != "TOKEN" && "$t" != "YOUR_TOKEN" ]]
}

upsert_env() {
  local key="$1" val="$2" tmp
  tmp="$(mktemp)"
  if [[ -f "$ENV_FILE" ]] && grep -qE "^${key}=" "$ENV_FILE"; then
    grep -vE "^${key}=" "$ENV_FILE" >"$tmp" || true
  else
    [[ -f "$ENV_FILE" ]] && cat "$ENV_FILE" >"$tmp" || true
  fi
  printf '%s=%s\n' "$key" "$val" >>"$tmp"
  mv "$tmp" "$ENV_FILE"
}

if [[ "$DO_SET" == "1" ]]; then
  [[ -n "$TOKEN" ]] || { echo "ERROR: --set needs token value" >&2; exit 1; }
  token_ok "$TOKEN" || { echo "ERROR: token looks like a placeholder" >&2; exit 1; }
  upsert_env "ZOHAL_API_TOKEN" "$TOKEN"
  upsert_env "Zohal__Enabled" "true"
  if ! grep -qE '^Zohal__BaseUrl=' "$ENV_FILE"; then
    upsert_env "Zohal__BaseUrl" "https://service.zohal.io/api/v0"
  fi
  if ! grep -qE '^Zohal__TimeoutSeconds=' "$ENV_FILE"; then
    upsert_env "Zohal__TimeoutSeconds" "30"
  fi
  echo "OK: wrote ZOHAL_API_TOKEN to $ENV_FILE (len=${#TOKEN})"
fi

cur="$(read_token)"
if token_ok "$cur"; then
  echo "OK: ZOHAL_API_TOKEN present (len=${#cur})"
else
  echo "WARN: ZOHAL_API_TOKEN missing in $ENV_FILE" >&2
  echo "NEXT: get token from https://dashboard.zohal.io then:" >&2
  echo "  bash $SCRIPT_DIR/ensure-zohal-token.sh --set 'YOUR_TOKEN' --restart" >&2
  echo "Also allowlist IP ${SERVER_IP:-195.24.237.132} in Zohal dashboard." >&2
  if [[ "$DO_SET" != "1" && "$DO_CHECK" == "1" ]]; then
    exit 2
  fi
fi

if [[ "$DO_RESTART" == "1" ]]; then
  echo "Restarting API to load Zohal token..."
  (cd "$(dirname "$ENV_FILE")" && docker compose -f "$COMPOSE_FILE" --env-file "$ENV_FILE" up -d --force-recreate --no-build api)
  bash "$SCRIPT_DIR/wait-db-ready.sh" || true
fi

exit 0
