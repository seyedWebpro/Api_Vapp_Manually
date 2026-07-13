#!/usr/bin/env bash
# rsync لوکال → سرور (بدون git)
# reuse: LOCAL_* / REMOTE_* dirs، excludeها (node_modules، bin، …)
#
# Usage (روی Mac لوکال — از روت Api_Vapp_Manually):
#   SERVER=vapp-prod bash devops/scripts/sync-to-server.sh
#
# Env:
#   SERVER (پیش‌فرض vapp-prod — Port 3031 در ~/.ssh/config), REMOTE_* , LOCAL_*
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

echo "=== sync-to-server ==="
echo "API:    $LOCAL_API_DIR → $SERVER:$REMOTE_API_DIR"

ssh "$SERVER" "mkdir -p $REMOTE_API_DIR $REMOTE_FRONT_DIR $REMOTE_PUBLIC_DIR"

rsync -avz --delete \
  --exclude 'node_modules' \
  --exclude 'bin' \
  --exclude 'obj' \
  --exclude '.git' \
  --exclude 'log' \
  --exclude 'wwwroot/uploads' \
  --exclude 'docker/.env' \
  "$LOCAL_API_DIR/" "$SERVER:$REMOTE_API_DIR/"

if [[ -n "$LOCAL_FRONT_DIR" && -d "$LOCAL_FRONT_DIR" ]]; then
  echo "Front: $LOCAL_FRONT_DIR → $SERVER:$REMOTE_FRONT_DIR"
  rsync -avz --delete \
    --exclude 'node_modules' \
    --exclude 'dist' \
    --exclude '.git' \
    "$LOCAL_FRONT_DIR/" "$SERVER:$REMOTE_FRONT_DIR/"
else
  echo "WARN: Admin_Vapp not found — sync front separately." >&2
fi

if [[ -n "$LOCAL_PUBLIC_DIR" && -d "$LOCAL_PUBLIC_DIR" ]]; then
  echo "Public: $LOCAL_PUBLIC_DIR → $SERVER:$REMOTE_PUBLIC_DIR"
  rsync -avz --delete \
    --exclude 'node_modules' \
    --exclude 'dist' \
    --exclude '.git' \
    "$LOCAL_PUBLIC_DIR/" "$SERVER:$REMOTE_PUBLIC_DIR/"
else
  echo "WARN: Public_Vapp not found at $LOCAL_API_DIR/../Public_Vapp — sync public separately." >&2
fi

echo ""
echo "OK: sync done. On server run:"
echo "  ssh $SERVER"
echo "  chmod +x $REMOTE_API_DIR/devops/scripts/*.sh"
echo "  bash $REMOTE_API_DIR/devops/scripts/deploy-public-front-host.sh   # Public (فرم/گردونه)"
echo "  bash $REMOTE_API_DIR/devops/scripts/deploy-server.sh --fast --wait # API + Admin"
