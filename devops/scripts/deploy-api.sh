#!/usr/bin/env bash
# Deploy API — sync + (optional) docker build + restart + wait AppVersion
#
# Usage (on server):
#   bash deploy-api.sh                    # sync + build + restart
#   SKIP_BUILD=1 bash deploy-api.sh       # فقط restart (سریع — بعد از تغییر .env / توکن)
#   SKIP_GIT_PULL=1 SKIP_BUILD=1 bash deploy-api.sh
#   RELOAD_NGINX=1 bash deploy-api.sh
#   ALLOW_SLOW_START=1 bash deploy-api.sh
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=lib/docker-pull-fallback.sh
source "$SCRIPT_DIR/lib/docker-pull-fallback.sh"
# shellcheck source=lib/deploy-progress.sh
source "$SCRIPT_DIR/lib/deploy-progress.sh"
# shellcheck source=lib/deploy-fail.sh
source "$SCRIPT_DIR/lib/deploy-fail.sh"

API_REPO_DIR="${API_REPO_DIR:-$HOME/Api_Vapp_Manually}"
API_BRANCH="${API_BRANCH:-main}"
COMPOSE_FILE="${COMPOSE_FILE:-docker/docker-compose.production.yml}"
ENV_FILE="${ENV_FILE:-$API_REPO_DIR/docker/.env}"
SKIP_BUILD="${SKIP_BUILD:-${API_SKIP_BUILD:-0}}"
SKIP_GIT_PULL="${SKIP_GIT_PULL:-0}"
DEPLOY_STEP_TOTAL=6

http_code() {
  local url="$1" code
  code="$(curl -sS -m "${2:-15}" -o /dev/null -w '%{http_code}' "$url" 2>/dev/null)" || code="000"
  [[ "$code" =~ ^[0-9]{3}$ ]] || code="000"
  printf '%s' "$code"
}

dump_api_failure() {
  echo "" >&2
  echo "── docker ps ──" >&2
  docker ps -a --filter name=vapp_api_prod --format 'table {{.Names}}\t{{.Status}}\t{{.Ports}}' >&2 || true
  echo "── docker logs (tail 120) ──" >&2
  docker logs --tail 120 vapp_api_prod 2>&1 | tail -120 >&2 || true
  echo "── recent migrate lines ──" >&2
  local logf="$API_REPO_DIR/log/log-$(date +%Y%m%d).txt"
  if [[ -f "$logf" ]]; then
    grep -iE 'Migration|PendingModel|Cannot open|ensured|Fatal|fail|Jwt|OutOfMemory|OOM' "$logf" | tail -40 >&2 || true
  fi
}

deploy_log "=== deploy-api started (SKIP_BUILD=$SKIP_BUILD SKIP_GIT_PULL=$SKIP_GIT_PULL) ==="
deploy_log "Repo: $API_REPO_DIR branch=$API_BRANCH"

cd "$API_REPO_DIR"

# Firebase mount must be a file (not a directory Docker creates if missing)
bash "$SCRIPT_DIR/ensure-runtime-files.sh"

deploy_step "Ensure env file"
if [[ ! -f "$ENV_FILE" ]]; then
  if [[ -f "$API_REPO_DIR/devops/.env.server.example" ]]; then
    cp "$API_REPO_DIR/devops/.env.server.example" "$ENV_FILE"
    deploy_log "WARN: created $ENV_FILE from example — edit SA_PASSWORD and Jwt__Secret before production use." >&2
  else
    printf 'SA_PASSWORD=Vapp@Secure2025!\nAPI_PORT_MAPPING=127.0.0.1:8080:8080\n' > "$ENV_FILE"
  fi
fi

if [[ "$SKIP_GIT_PULL" != "1" && -d "$API_REPO_DIR/.git" ]]; then
  deploy_step "git sync safe ($API_BRANCH)"
  API_REPO_DIR="$API_REPO_DIR" API_BRANCH="$API_BRANCH" ENV_FILE="$ENV_FILE" \
    bash "$SCRIPT_DIR/sync-api-repo-safe.sh"
else
  deploy_log "skip git sync (SKIP_GIT_PULL=$SKIP_GIT_PULL)"
fi

if [[ "$SKIP_BUILD" == "1" ]]; then
  deploy_log "SKIP_BUILD=1 — no docker build (restart existing image)"
else
  deploy_step "Determine build pull strategy"
  BUILD_PULL="${API_BUILD_PULL:-auto}"
  if [[ "$BUILD_PULL" == "auto" ]]; then
    if docker_api_base_images_cached; then
      BUILD_PULL="false"
      deploy_log "NOTE: dotnet base images cached — build with --pull=false"
    else
      BUILD_PULL="always"
      docker_pull_api_base_images || true
      if docker_api_base_images_cached; then
        BUILD_PULL="false"
      else
        deploy_log "ERROR: dotnet base images not on server — build from Mac:" >&2
        deploy_log "  SERVER=vapp-prod bash devops/scripts/deploy-api-upload-image.sh" >&2
        exit 1
      fi
    fi
  fi

  deploy_step "Docker build API"
  docker compose -f "$COMPOSE_FILE" --env-file "$ENV_FILE" build --pull="$BUILD_PULL" api
fi

deploy_step "Ensure DbVapp exists"
bash "$SCRIPT_DIR/ensure-dbvapp.sh" || deploy_log "WARN: ensure-dbvapp failed (API will retry on start)"

deploy_step "Restart API container"
docker compose -f "$COMPOSE_FILE" --env-file "$ENV_FILE" up -d --no-deps --force-recreate --no-build api

if [[ "${RELOAD_NGINX:-0}" == "1" ]]; then
  bash "$SCRIPT_DIR/apply-nginx.sh"
fi

deploy_step "Wait DB migrate + AppVersion (not just /health)"
api_code="000"
appver_code="000"
if MAX_ATTEMPTS="${DB_READY_ATTEMPTS:-36}" INTERVAL_SECS="${DB_READY_INTERVAL:-10}" \
  bash "$SCRIPT_DIR/wait-db-ready.sh"; then
  api_code="200"
  appver_code="200"
else
  api_code="$(http_code http://127.0.0.1:8080/health 10)"
  appver_code="$(http_code 'http://127.0.0.1:8080/api/AppVersion/check?platform=android&currentVersion=1.0.0' 15)"
  dump_api_failure
fi

deploy_log "API:$api_code APPVER:$appver_code"
docker ps --filter name=vapp_api_prod --format 'table {{.Names}}\t{{.Status}}'
deploy_log "=== deploy-api done ==="

if [[ "$api_code" != "200" || "$appver_code" != "200" ]]; then
  if [[ "${ALLOW_SLOW_START:-0}" == "1" ]]; then
    deploy_log "WARN: health=$api_code appver=$appver_code (ALLOW_SLOW_START=1 — not failing yet)" >&2
    exit 0
  fi
  deploy_fail "API health=$api_code AppVersion=$appver_code (see logs above)" \
    "docker logs --tail 200 vapp_api_prod" \
    "SKIP_BUILD=1 bash $SCRIPT_DIR/deploy-api.sh" \
    "bash $SCRIPT_DIR/diagnose-deploy.sh" || true
  exit 1
fi
