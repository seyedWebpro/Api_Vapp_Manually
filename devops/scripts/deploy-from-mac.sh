#!/usr/bin/env bash
# Deploy from Mac — choose fastest path based on change type.
#
# Usage (from Api_Vapp_Manually root):
#   bash devops/scripts/deploy-from-mac.sh api
#   bash devops/scripts/deploy-from-mac.sh admin
#   bash devops/scripts/deploy-from-mac.sh public
#   bash devops/scripts/deploy-from-mac.sh public-fast
#   bash devops/scripts/deploy-from-mac.sh all-fronts
#
# Env: SERVER (default vapp-prod)
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=lib/deploy-progress.sh
source "$SCRIPT_DIR/lib/deploy-progress.sh"

API_DIR="$(cd "$SCRIPT_DIR/../.." && pwd)"
SERVER="${SERVER:-vapp-prod}"
REMOTE_API_DIR="${REMOTE_API_DIR:-/root/Api_Vapp_Manually}"
COMPOSE_FILE="${COMPOSE_FILE:-docker/docker-compose.production.yml}"
ENV_FILE="${ENV_FILE:-docker/.env}"
DEPLOY_STEP_TOTAL=1
START=$SECONDS

usage() {
  cat <<'EOF'
Deploy from Mac — choose path based on change

  api          C# / API change — build Docker on Mac + upload (~3–7 min with cache)
  api-restart  image already on server — restart container only (~1 min)
  admin        React / admin panel — build + upload dist (~2–4 min)
  admin-fast   dist already built — upload only (~30 sec)
  public       Public_Vapp (SMS form/wheel) — build + upload (~2–4 min)
  public-fast  Public dist already built — upload only (~30 sec)
  all-fronts   Admin + Public both
  both         API + Admin
  all          API + Admin + Public
  health       health check on server

Example:
  bash devops/scripts/deploy-from-mac.sh api
  bash devops/scripts/deploy-from-mac.sh admin-fast
EOF
}

run_health() {
  ssh "$SERVER" "bash $REMOTE_API_DIR/devops/scripts/health-check.sh"
}

deploy_api() {
  deploy_log "=== API: build + upload + restart ==="
  SERVER="$SERVER" bash "$SCRIPT_DIR/deploy-api-upload-image.sh"
}

deploy_api_restart() {
  deploy_log "=== API: restart only (no build) ==="
  ssh "$SERVER" "cd $REMOTE_API_DIR && docker compose -f $COMPOSE_FILE --env-file $ENV_FILE up -d --no-deps --force-recreate --no-build api"
  sleep 45
  run_health
}

deploy_admin() {
  deploy_log "=== Admin: build + upload dist ==="
  SERVER="$SERVER" bash "$SCRIPT_DIR/deploy-front-upload-dist.sh"
}

deploy_admin_fast() {
  deploy_log "=== Admin: upload dist (no build) ==="
  SKIP_BUILD=1 SERVER="$SERVER" bash "$SCRIPT_DIR/deploy-front-upload-dist.sh"
}

deploy_public() {
  deploy_log "=== Public: build + upload dist ==="
  SERVER="$SERVER" bash "$SCRIPT_DIR/deploy-public-front-upload-dist.sh"
}

deploy_public_fast() {
  deploy_log "=== Public: upload dist (no build) ==="
  SKIP_BUILD=1 SERVER="$SERVER" bash "$SCRIPT_DIR/deploy-public-front-upload-dist.sh"
}

deploy_all_fronts() {
  deploy_admin
  deploy_public
}

MODE="${1:-}"

case "$MODE" in
  -h|--help|help)
    usage
    exit 0
    ;;
  "")
    usage
    exit 1
    ;;
  api)
    deploy_api
    ;;
  api-restart)
    deploy_api_restart
    ;;
  admin)
    deploy_admin
    ;;
  admin-fast)
    deploy_admin_fast
    ;;
  public)
    deploy_public
    ;;
  public-fast)
    deploy_public_fast
    ;;
  all-fronts)
    deploy_all_fronts
    ;;
  both)
    deploy_admin
    deploy_api
    ;;
  all)
    deploy_all_fronts
    deploy_api
    ;;
  health)
    run_health
    ;;
  *)
    deploy_log "ERROR: unknown mode: $MODE" >&2
    usage
    exit 1
    ;;
esac

deploy_log "✓ deploy-from-mac mode=$MODE finished in $(_deploy_elapsed "$START")"
