#!/usr/bin/env bash
# Build API Docker image on Mac (mcr.microsoft.com) and load on Iran server
#
# Usage (روی Mac — از روت Api_Vapp_Manually):
#   SERVER=root@185.116.162.233 bash devops/scripts/deploy-api-upload-image.sh
#   SERVER=root@185.116.162.233 bash devops/scripts/deploy-api-upload-image.sh --no-deploy
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
LOCAL_API_DIR="${LOCAL_API_DIR:-$(cd "$SCRIPT_DIR/../.." && pwd)}"
SERVER="${SERVER:-root@185.116.162.233}"
REMOTE_API_DIR="${REMOTE_API_DIR:-/root/Api_Vapp_Manually}"
COMPOSE_FILE="${COMPOSE_FILE:-docker/docker-compose.production.yml}"
ENV_FILE="${ENV_FILE:-docker/.env}"
API_IMAGE="${API_IMAGE:-vapp-api}"
DEPLOY_AFTER_LOAD="${DEPLOY_AFTER_LOAD:-1}"

if [[ "${1:-}" == "--no-deploy" ]]; then
  DEPLOY_AFTER_LOAD=0
fi

echo "=== deploy-api-upload-image ==="
echo "Build: $LOCAL_API_DIR"
echo "Server: $SERVER:$REMOTE_API_DIR"
echo "Image: $API_IMAGE"

cd "$LOCAL_API_DIR"
docker compose -f "$COMPOSE_FILE" build api
echo "=== uploading $API_IMAGE to server (چند دقیقه طول می‌کشد) ==="
docker save "$API_IMAGE" | gzip | ssh "$SERVER" 'gunzip | docker load'

if [[ "$DEPLOY_AFTER_LOAD" == "1" ]]; then
  ssh "$SERVER" "cd $REMOTE_API_DIR && git pull origin main && docker compose -f $COMPOSE_FILE --env-file $ENV_FILE up -d --force-recreate --no-build api"
  echo "=== health check on server ==="
  ssh "$SERVER" "bash $REMOTE_API_DIR/devops/scripts/health-check.sh" || true
fi

echo "OK: API image uploaded${DEPLOY_AFTER_LOAD:+ and deployed}"
