#!/usr/bin/env bash
# Ensure host files/env so API container can actually start (permanent anti-000 fix)
#
# Fixes the usual production foot-guns:
#   - secrets/firebase-service-account.json became a DIRECTORY (Docker bind-mount gotcha)
#   - empty Jwt__Secret= in docker/.env overrides appsettings → crash before listen
#   - missing backups/log/secrets dirs
#
# Usage (on server, before compose up):
#   bash devops/scripts/ensure-runtime-files.sh
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
API_DIR="$(cd "$SCRIPT_DIR/../.." && pwd)"
ENV_FILE="${ENV_FILE:-$API_DIR/docker/.env}"
SECRETS_DIR="$API_DIR/secrets"
FIREBASE="$SECRETS_DIR/firebase-service-account.json"

log() { echo "[ensure-runtime] $*"; }

mkdir -p "$API_DIR/backups/daily" "$API_DIR/backups/weekly" "$API_DIR/log" "$API_DIR/wwwroot/uploads" "$SECRETS_DIR"

# --- Firebase must be a FILE, never a directory ---
if [[ -d "$FIREBASE" ]]; then
  log "WARN: $FIREBASE is a DIRECTORY (docker created it) — replacing with placeholder file"
  rm -rf "$FIREBASE"
fi
if [[ ! -f "$FIREBASE" ]]; then
  log "creating placeholder firebase JSON (Push disabled until real credentials)"
  cat >"$FIREBASE" <<'JSON'
{
  "type": "service_account",
  "project_id": "vapp-placeholder",
  "private_key_id": "placeholder",
  "private_key": "-----BEGIN PRIVATE KEY-----\nMIIBUgIBADANBgkqhkiG9w0BAQEFAASCATwwggE4AgEAAkEAuQ==\n-----END PRIVATE KEY-----\n",
  "client_email": "placeholder@vapp-placeholder.iam.gserviceaccount.com",
  "client_id": "0",
  "auth_uri": "https://accounts.google.com/o/oauth2/auth",
  "token_uri": "https://oauth2.googleapis.com/token"
}
JSON
  chmod 600 "$FIREBASE"
fi

# --- Sanitize .env: empty overrides kill Jwt / ConnectionStrings ---
if [[ -f "$ENV_FILE" ]]; then
  tmp="$(mktemp)"
  # drop empty KEY= lines for known dangerous overrides
  grep -vE '^(Jwt__Secret|Jwt__Issuer|Jwt__Audience|ConnectionStrings__DockerConnection|ConnectionStrings__LocalConnection)=\s*$' "$ENV_FILE" >"$tmp" || true
  # if Jwt__Secret is placeholder/too short, remove so appsettings.json wins
  if grep -qE '^Jwt__Secret=' "$tmp"; then
    jwt_val="$(grep -E '^Jwt__Secret=' "$tmp" | head -1 | cut -d= -f2-)"
    if [[ ${#jwt_val} -lt 32 || "$jwt_val" == CHANGE_ME* ]]; then
      log "WARN: removing weak/placeholder Jwt__Secret from .env (use appsettings default)"
      grep -vE '^Jwt__Secret=' "$tmp" >"${tmp}.2" || true
      mv "${tmp}.2" "$tmp"
    fi
  fi
  mv "$tmp" "$ENV_FILE"
  log "sanitized $ENV_FILE"
else
  log "WARN: no $ENV_FILE yet"
fi

# Ensure port mapping exists
if [[ -f "$ENV_FILE" ]] && ! grep -qE '^API_PORT_MAPPING=' "$ENV_FILE"; then
  printf '\nAPI_PORT_MAPPING=127.0.0.1:8080:8080\n' >>"$ENV_FILE"
  log "added API_PORT_MAPPING=127.0.0.1:8080:8080"
fi

log "OK: runtime files ready"
