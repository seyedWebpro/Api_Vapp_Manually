#!/usr/bin/env bash
# rsync local → server (without git)
#
# Usage (from Api_Vapp_Manually root on Mac):
#   SERVER=vapp-prod bash devops/scripts/sync-to-server.sh
#
# Env:
#   SERVER (default vapp-prod — Port 3031 in ~/.ssh/config), REMOTE_* , LOCAL_*
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=lib/deploy-progress.sh
source "$SCRIPT_DIR/lib/deploy-progress.sh"

LOCAL_API_DIR="${LOCAL_API_DIR:-$(cd "$SCRIPT_DIR/../.." && pwd)}"
if [[ -z "${LOCAL_FRONT_DIR:-}" ]]; then
  if [[ -d "$LOCAL_API_DIR/../Admin_Vapp" ]]; then
    LOCAL_FRONT_DIR="$(cd "$LOCAL_API_DIR/../Admin_Vapp" && pwd)"
  else
    LOCAL_FRONT_DIR=""
  fi
fi
if [[ -z "${LOCAL_PUBLIC_DIR:-}" ]]; then
  if [[ -d "$LOCAL_API_DIR/../Public_Vapp" ]]; then
    LOCAL_PUBLIC_DIR="$(cd "$LOCAL_API_DIR/../Public_Vapp" && pwd)"
  else
    LOCAL_PUBLIC_DIR=""
  fi
fi
REMOTE_API_DIR="${REMOTE_API_DIR:-/root/Api_Vapp_Manually}"
REMOTE_FRONT_DIR="${REMOTE_FRONT_DIR:-/root/Admin_Vapp}"
REMOTE_PUBLIC_DIR="${REMOTE_PUBLIC_DIR:-/root/Public_Vapp}"
SERVER="${SERVER:-vapp-prod}"
DEPLOY_STEP_TOTAL=3

SSH_OPTS=(
  -o ServerAliveInterval=15
  -o ServerAliveCountMax=4
  -o TCPKeepAlive=yes
  -o ConnectTimeout=30
  -o BatchMode=no
)

deploy_log "=== sync-to-server ==="
deploy_log "API:    $LOCAL_API_DIR → $SERVER:$REMOTE_API_DIR"

ssh "${SSH_OPTS[@]}" "$SERVER" "mkdir -p $REMOTE_API_DIR $REMOTE_FRONT_DIR $REMOTE_PUBLIC_DIR"

deploy_step "Sync API source"
rsync -ah --progress --partial --delete \
  --exclude 'node_modules' \
  --exclude 'bin' \
  --exclude 'obj' \
  --exclude '.git' \
  --exclude 'log' \
  --exclude 'wwwroot/uploads' \
  --exclude 'docker/.env' \
  "$LOCAL_API_DIR/" "$SERVER:$REMOTE_API_DIR/"

if [[ -n "$LOCAL_FRONT_DIR" && -d "$LOCAL_FRONT_DIR" ]]; then
  deploy_step "Sync Admin source"
  deploy_log "Front: $LOCAL_FRONT_DIR → $SERVER:$REMOTE_FRONT_DIR"
  rsync -ah --progress --partial --delete \
    --exclude 'node_modules' \
    --exclude 'dist' \
    --exclude '.git' \
    "$LOCAL_FRONT_DIR/" "$SERVER:$REMOTE_FRONT_DIR/"
else
  deploy_log "WARN: Admin_Vapp not found — sync front separately." >&2
fi

if [[ -n "$LOCAL_PUBLIC_DIR" && -d "$LOCAL_PUBLIC_DIR" ]]; then
  deploy_step "Sync Public source"
  deploy_log "Public: $LOCAL_PUBLIC_DIR → $SERVER:$REMOTE_PUBLIC_DIR"
  rsync -ah --progress --partial --delete \
    --exclude 'node_modules' \
    --exclude 'dist' \
    --exclude '.git' \
    "$LOCAL_PUBLIC_DIR/" "$SERVER:$REMOTE_PUBLIC_DIR/"
else
  deploy_log "WARN: Public_Vapp not found at $LOCAL_API_DIR/../Public_Vapp — sync public separately." >&2
fi

deploy_log ""
deploy_log "OK: sync done. On server run:"
deploy_log "  ssh $SERVER"
deploy_log "  chmod +x $REMOTE_API_DIR/devops/scripts/*.sh"
deploy_log "  bash $REMOTE_API_DIR/devops/scripts/deploy-public-front-host.sh   # Public"
deploy_log "  bash $REMOTE_API_DIR/devops/scripts/deploy-server.sh --fast --wait # API + Admin"
