#!/usr/bin/env bash
# rsync لوکال → سرور (بدون git)
# reuse: LOCAL_* / REMOTE_* dirs، excludeها (node_modules، bin، …)
#
# Usage (روی Mac لوکال — از روت Api_Vapp_Manually):
#   SERVER=root@185.116.162.233 bash devops/scripts/sync-to-server.sh
#
# Env:
#   SERVER, REMOTE_API_DIR, REMOTE_FRONT_DIR, LOCAL_API_DIR, LOCAL_FRONT_DIR
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
LOCAL_API_DIR="${LOCAL_API_DIR:-$(cd "$SCRIPT_DIR/../.." && pwd)}"
if [[ -z "${LOCAL_FRONT_DIR:-}" ]]; then
  if [[ -d "$LOCAL_API_DIR/../Admin_Vapp" ]]; then
    LOCAL_FRONT_DIR="$(cd "$LOCAL_API_DIR/../Admin_Vapp" && pwd)"
  else
    LOCAL_FRONT_DIR=""
  fi
fi
REMOTE_API_DIR="${REMOTE_API_DIR:-/root/Api_Vapp_Manually}"
REMOTE_FRONT_DIR="${REMOTE_FRONT_DIR:-/root/Admin_Vapp}"
SERVER="${SERVER:-root@185.116.162.233}"

echo "=== sync-to-server ==="
echo "API:   $LOCAL_API_DIR → $SERVER:$REMOTE_API_DIR"

ssh "$SERVER" "mkdir -p $REMOTE_API_DIR $REMOTE_FRONT_DIR"

rsync -avz --delete \
  --exclude 'node_modules' \
  --exclude 'bin' \
  --exclude 'obj' \
  --exclude '.git' \
  --exclude 'log' \
  --exclude 'wwwroot/uploads' \
  "$LOCAL_API_DIR/" "$SERVER:$REMOTE_API_DIR/"

if [[ -n "$LOCAL_FRONT_DIR" && -d "$LOCAL_FRONT_DIR" ]]; then
  echo "Front: $LOCAL_FRONT_DIR → $SERVER:$REMOTE_FRONT_DIR"
  rsync -avz --delete \
    --exclude 'node_modules' \
    --exclude 'dist' \
    --exclude '.git' \
    "$LOCAL_FRONT_DIR/" "$SERVER:$REMOTE_FRONT_DIR/"
else
  echo "WARN: Admin_Vapp not found at $LOCAL_API_DIR/../Admin_Vapp — sync front separately." >&2
fi

echo ""
echo "OK: sync done. On server run:"
echo "  ssh $SERVER"
echo "  chmod +x $REMOTE_API_DIR/devops/scripts/*.sh"
echo "  cp $REMOTE_API_DIR/devops/.env.server.example $REMOTE_API_DIR/docker/.env"
echo "  nano $REMOTE_API_DIR/docker/.env"
echo "  bash $REMOTE_API_DIR/devops/scripts/deploy-server.sh --full --wait"
