#!/usr/bin/env bash
# Deploy front — docker (default) or host
#
# Usage (on server):
#   bash ~/Api_Vapp_Manually/devops/scripts/deploy-front.sh
#   bash deploy-front.sh --foreground
#   FRONT_DEPLOY_MODE=host bash deploy-front.sh --foreground
#   DOCKER_BUILD_NO_CACHE=1 bash deploy-front.sh
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=lib/docker-pull-fallback.sh
source "$SCRIPT_DIR/lib/docker-pull-fallback.sh"
# shellcheck source=lib/deploy-progress.sh
source "$SCRIPT_DIR/lib/deploy-progress.sh"

FRONT_DIR="${FRONT_DIR:-$HOME/Admin_Vapp}"
FRONT_BRANCH="${FRONT_BRANCH:-main}"
FRONT_CONTAINER="${FRONT_CONTAINER:-vapp-admin}"
FRONT_IMAGE="${FRONT_IMAGE:-vapp-admin:latest}"
FRONT_PORT_BIND="${FRONT_PORT_BIND:-127.0.0.1:3005:80}"
VITE_API_URL="${VITE_API_URL:-}"
DEPLOY_LOG="${DEPLOY_LOG:-}"
FRONT_DEPLOY_MODE="${FRONT_DEPLOY_MODE:-docker}"
DEPLOY_STEP_TOTAL=6

run_deploy() {
  deploy_log "=== deploy-front started ==="
  deploy_log "FRONT_DIR=$FRONT_DIR branch=$FRONT_BRANCH mode=$FRONT_DEPLOY_MODE"

  if [[ "$FRONT_DEPLOY_MODE" == "host" ]]; then
    exec bash "$SCRIPT_DIR/deploy-front-host.sh"
  fi

  if [[ ! -d "$FRONT_DIR" ]]; then
    deploy_log "ERROR: front directory not found: $FRONT_DIR" >&2
    exit 1
  fi

  cd "$FRONT_DIR"

  deploy_step "git pull ($FRONT_BRANCH)"
  if [[ -d "$FRONT_DIR/.git" ]]; then
    git pull origin "$FRONT_BRANCH"
  fi

  deploy_step "Docker pull base images"
  docker_pull_front_base_images

  deploy_step "Docker build front"
  local build_args=(--build-arg "VITE_API_URL=$VITE_API_URL")
  if [[ "${DOCKER_BUILD_NO_CACHE:-0}" == "1" ]]; then
    build_args=(--no-cache "${build_args[@]}")
    deploy_log "WARN: DOCKER_BUILD_NO_CACHE=1 — build will take longer."
  fi
  deploy_log "NOTE: npm install + vite build inside Docker — usually 5–15 min"
  docker build --progress=plain "${build_args[@]}" -t "$FRONT_IMAGE" .

  deploy_step "Docker run front"
  docker rm -f "$FRONT_CONTAINER" 2>/dev/null || true
  docker run -d \
    --name "$FRONT_CONTAINER" \
    -p "$FRONT_PORT_BIND" \
    --restart unless-stopped \
    "$FRONT_IMAGE"

  deploy_step "Health check"
  local front_code="000"
  for attempt in 1 2 3 4 5 6; do
    sleep 5
    front_code="$(curl -sS -m 15 -o /dev/null -w '%{http_code}' http://127.0.0.1:3005/ 2>/dev/null || echo "000")"
    deploy_log "FRONT health attempt $attempt/6: $front_code"
    [[ "$front_code" == "200" ]] && break
  done

  deploy_log "FRONT:$front_code"
  docker ps --filter "name=$FRONT_CONTAINER" --format 'table {{.Names}}\t{{.Status}}'
  deploy_log "=== deploy-front done ==="

  if [[ "$front_code" != "200" ]]; then
    deploy_log "WARN: front health returned $front_code (container may still be starting)." >&2
  fi
}

if [[ "${1:-}" == "--background" ]]; then
  log_file="${DEPLOY_LOG:-/root/vapp-front-deploy-$(date +%F_%H%M%S).log}"
  printf '%s\n' "$log_file" >"${LAST_FRONT_DEPLOY_LOG:-$HOME/.vapp-last-front-deploy.log}"
  echo "Starting background deploy → $log_file"
  echo "(build ~5–15 min — progress: bash $SCRIPT_DIR/deploy-progress-watch.sh $log_file)"
  echo "Log saved: ~/.vapp-last-front-deploy.log"
  nohup env DEPLOY_LOG="$log_file" FRONT_DEPLOY_MODE="$FRONT_DEPLOY_MODE" bash "$0" --foreground >"$log_file" 2>&1 &
  echo "PID: $!"
  echo "Tail: tail -f $log_file"
  exit 0
fi

if [[ "${1:-}" == "--foreground" ]]; then
  shift || true
fi

if [[ -n "$DEPLOY_LOG" ]]; then
  exec >>"$DEPLOY_LOG" 2>&1
fi

run_deploy
