#!/usr/bin/env bash
# Deploy از Mac — بر اساس نوع تغییر، سریع‌ترین مسیر را انتخاب می‌کند.
#
# Usage (از روت Api_Vapp_Manually):
#   bash devops/scripts/deploy-from-mac.sh api
#   bash devops/scripts/deploy-from-mac.sh api-restart
#   bash devops/scripts/deploy-from-mac.sh admin
#   bash devops/scripts/deploy-from-mac.sh admin-fast
#   bash devops/scripts/deploy-from-mac.sh both
#   bash devops/scripts/deploy-from-mac.sh health
#
# Env: SERVER (پیش‌فرض vapp-prod)
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
API_DIR="$(cd "$SCRIPT_DIR/../.." && pwd)"
SERVER="${SERVER:-vapp-prod}"
REMOTE_API_DIR="${REMOTE_API_DIR:-/root/Api_Vapp_Manually}"
COMPOSE_FILE="${COMPOSE_FILE:-docker/docker-compose.production.yml}"
ENV_FILE="${ENV_FILE:-docker/.env}"

usage() {
  cat <<'EOF'
Deploy از Mac — انتخاب بر اساس تغییر

  api          تغییر C# / API — build Docker روی Mac + upload (~۳–۷ دقیقه با cache)
  api-restart  image از قبل روی سرور است — فقط restart container (~۱ دقیقه)
  admin        تغییر React / پنل — build + upload dist (~۲–۴ دقیقه)
  admin-fast   dist از قبل build شده — فقط upload (~۳۰ ثانیه)
  both         API + Admin (پشت‌سرهم)
  health       چک سلامت روی سرور

راهنما: devops/MAC-QUICK-DEPLOY.md

مثال:
  bash devops/scripts/deploy-from-mac.sh api
  bash devops/scripts/deploy-from-mac.sh admin-fast
EOF
}

run_health() {
  ssh "$SERVER" "bash $REMOTE_API_DIR/devops/scripts/health-check.sh"
}

deploy_api() {
  echo "=== API: build + upload + restart ==="
  SERVER="$SERVER" bash "$SCRIPT_DIR/deploy-api-upload-image.sh"
}

deploy_api_restart() {
  echo "=== API: restart only (بدون build) ==="
  ssh "$SERVER" "cd $REMOTE_API_DIR && docker compose -f $COMPOSE_FILE --env-file $ENV_FILE up -d --force-recreate --no-build api"
  sleep 45
  run_health
}

deploy_admin() {
  echo "=== Admin: build + upload dist ==="
  SERVER="$SERVER" bash "$SCRIPT_DIR/deploy-front-upload-dist.sh"
}

deploy_admin_fast() {
  echo "=== Admin: upload dist (بدون build) ==="
  SKIP_BUILD=1 SERVER="$SERVER" bash "$SCRIPT_DIR/deploy-front-upload-dist.sh"
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
  both)
    deploy_admin
    deploy_api
    ;;
  health)
    run_health
    ;;
  *)
    echo "ERROR: unknown mode: $MODE" >&2
    usage
    exit 1
    ;;
esac
