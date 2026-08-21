#!/usr/bin/env bash
# Deploy from Mac — سریع‌ترین مسیر بر اساس نوع تغییر + خطای واضح
#
# Usage (from Api_Vapp_Manually root):
#   bash devops/scripts/deploy-from-mac.sh api
#   bash devops/scripts/deploy-from-mac.sh admin
#   bash devops/scripts/deploy-from-mac.sh public
#   bash devops/scripts/deploy-from-mac.sh all
#   bash devops/scripts/deploy-from-mac.sh health
#   bash devops/scripts/deploy-from-mac.sh diagnose
#
# Env: SERVER (default vapp-prod) — فیلترشکن را خاموش کنید
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=lib/deploy-progress.sh
source "$SCRIPT_DIR/lib/deploy-progress.sh"
# shellcheck source=lib/deploy-fail.sh
source "$SCRIPT_DIR/lib/deploy-fail.sh"

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

  api          C# / API — build Docker on Mac + upload (~3–7 min)
  api-restart  image on server — restart + wait Db/Migrate (~1–3 min)
  admin        Admin panel — build + upload dist
  admin-fast   Admin dist already built — upload only
  public       Public_Vapp (form/wheel) — build + upload
  public-fast  Public dist already built — upload only
  all-fronts   Admin + Public
  both         API + Admin
  all          API + Admin + Public
  health       health-check on server (incl. AppVersion + Public)
  diagnose     full diagnose on server (reasons + next commands)
  db-fix      ensure DbVapp + restart API + wait migrate (on server)

Example:
  bash devops/scripts/deploy-from-mac.sh api
  bash devops/scripts/deploy-from-mac.sh diagnose
EOF
}

fail() {
  deploy_fail "$@" || true
  echo "  SERVER=$SERVER bash $SCRIPT_DIR/deploy-from-mac.sh diagnose" >&2
  exit 1
}

require_ssh() {
  if ! ssh -o BatchMode=yes -o ConnectTimeout=15 "$SERVER" 'echo ok' >/dev/null 2>&1; then
    fail "SSH to $SERVER failed (VPN را خاموش کنید؟)" \
      "bash devops/scripts/setup-local-ssh-to-server.sh --force" \
      "ssh $SERVER 'echo SSH_OK'"
  fi
}

run_health() {
  ssh "$SERVER" "bash $REMOTE_API_DIR/devops/scripts/health-check.sh" \
    || fail "remote health-check failed" "SERVER=$SERVER bash $SCRIPT_DIR/deploy-from-mac.sh diagnose"
}

run_diagnose() {
  require_ssh
  ssh "$SERVER" "bash $REMOTE_API_DIR/devops/scripts/diagnose-deploy.sh"
}

run_db_fix() {
  require_ssh
  ssh "$SERVER" "bash $REMOTE_API_DIR/devops/scripts/ensure-dbvapp.sh --restart-api --wait" \
    || fail "db-fix failed on server" "SERVER=$SERVER bash $SCRIPT_DIR/deploy-from-mac.sh diagnose"
}

deploy_api() {
  deploy_log "=== API: build + upload + restart ==="
  require_ssh
  SERVER="$SERVER" bash "$SCRIPT_DIR/deploy-api-upload-image.sh" \
    || fail "API upload/deploy failed" "check Docker build on Mac / SSH"
  ssh "$SERVER" "bash $REMOTE_API_DIR/devops/scripts/wait-db-ready.sh" \
    || fail "API up but DB/AppVersion not ready" \
      "SERVER=$SERVER bash $SCRIPT_DIR/deploy-from-mac.sh db-fix"
}

deploy_api_restart() {
  deploy_log "=== API: restart + wait migrate ==="
  require_ssh
  ssh "$SERVER" "bash $REMOTE_API_DIR/devops/scripts/ensure-dbvapp.sh && cd $REMOTE_API_DIR && docker compose -f $COMPOSE_FILE --env-file $ENV_FILE up -d --no-deps --force-recreate --no-build api"
  ssh "$SERVER" "bash $REMOTE_API_DIR/devops/scripts/wait-db-ready.sh" \
    || fail "restart OK but AppVersion/DB not ready" "SERVER=$SERVER bash $SCRIPT_DIR/deploy-from-mac.sh diagnose"
  run_health
}

deploy_admin() {
  deploy_log "=== Admin: build + upload dist ==="
  require_ssh
  SERVER="$SERVER" bash "$SCRIPT_DIR/deploy-front-upload-dist.sh" \
    || fail "Admin upload failed"
}

deploy_admin_fast() {
  deploy_log "=== Admin: upload dist (no build) ==="
  require_ssh
  SKIP_BUILD=1 SERVER="$SERVER" bash "$SCRIPT_DIR/deploy-front-upload-dist.sh" \
    || fail "Admin fast upload failed"
}

deploy_public() {
  deploy_log "=== Public: build + upload dist ==="
  require_ssh
  SERVER="$SERVER" bash "$SCRIPT_DIR/deploy-public-front-upload-dist.sh" \
    || fail "Public upload failed"
}

deploy_public_fast() {
  deploy_log "=== Public: upload dist (no build) ==="
  require_ssh
  SKIP_BUILD=1 SERVER="$SERVER" bash "$SCRIPT_DIR/deploy-public-front-upload-dist.sh" \
    || fail "Public fast upload failed"
}

MODE="${1:-}"

case "$MODE" in
  -h|--help|help) usage; exit 0 ;;
  "") usage; exit 1 ;;
  api) deploy_api ;;
  api-restart) deploy_api_restart ;;
  admin) deploy_admin ;;
  admin-fast) deploy_admin_fast ;;
  public) deploy_public ;;
  public-fast) deploy_public_fast ;;
  all-fronts) deploy_admin; deploy_public ;;
  both) deploy_admin; deploy_api ;;
  all) deploy_admin; deploy_public; deploy_api ;;
  health) require_ssh; run_health ;;
  diagnose) run_diagnose ;;
  db-fix) run_db_fix ;;
  *)
    fail "unknown mode: $MODE" "bash $0 --help"
    ;;
esac

deploy_ok_box "deploy-from-mac mode=$MODE finished in $(_deploy_elapsed "$START")"
