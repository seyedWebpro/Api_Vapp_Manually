#!/usr/bin/env bash
# Deploy Public_Vapp — docker (جایگزین host؛ معمولاً از Mac با upload-dist استفاده کنید)
#
# Usage (روی سرور):
#   bash deploy-public-front.sh
#   bash deploy-public-front.sh --foreground
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=lib/docker-pull-fallback.sh
source "$SCRIPT_DIR/lib/docker-pull-fallback.sh"

PUBLIC_DIR="${PUBLIC_DIR:-$HOME/Public_Vapp}"
PUBLIC_BRANCH="${PUBLIC_BRANCH:-main}"
PUBLIC_CONTAINER="${PUBLIC_CONTAINER:-vapp-public}"
PUBLIC_IMAGE="${PUBLIC_IMAGE:-vapp-public:latest}"
PUBLIC_PORT_BIND="${PUBLIC_PORT_BIND:-127.0.0.1:3006:80}"
VITE_API_URL="${VITE_API_URL:-}"
DEPLOY_LOG="${DEPLOY_LOG:-}"

run_deploy() {
  echo "=== deploy-public-front started $(date '+%Y-%m-%dT%H:%M:%S') ==="
  echo "PUBLIC_DIR=$PUBLIC_DIR branch=$PUBLIC_BRANCH"

  [[ -d "$PUBLIC_DIR" ]] || { echo "ERROR: $PUBLIC_DIR not found" >&2; exit 1; }

  cd "$PUBLIC_DIR"
  if [[ -d .git ]]; then
    git pull origin "$PUBLIC_BRANCH"
  fi

  local build_args=(--build-arg "VITE_API_URL=$VITE_API_URL")
  if [[ "${DOCKER_BUILD_NO_CACHE:-0}" == "1" ]]; then
    build_args=(--no-cache "${build_args[@]}")
  fi

  docker_pull_front_base_images
  docker build --progress=plain "${build_args[@]}" -t "$PUBLIC_IMAGE" .

  docker rm -f "$PUBLIC_CONTAINER" 2>/dev/null || true
  docker run -d \
    --name "$PUBLIC_CONTAINER" \
    -p "$PUBLIC_PORT_BIND" \
    --restart unless-stopped \
    "$PUBLIC_IMAGE"

  local code attempt
  code="000"
  for attempt in 1 2 3 4 5 6; do
    sleep 3
    code="$(curl -sS -m 10 -o /dev/null -w '%{http_code}' http://127.0.0.1:3006/ 2>/dev/null || echo "000")"
    [[ "$code" == "200" ]] && break
  done

  FRONT_STATIC_ROOT="${FRONT_STATIC_ROOT:-/var/www/vapp-admin}" \
    bash "$SCRIPT_DIR/apply-nginx.sh" || true

  echo "PUBLIC:$code"
  docker ps --filter "name=$PUBLIC_CONTAINER" --format 'table {{.Names}}\t{{.Status}}'
  echo "=== deploy-public-front done $(date '+%Y-%m-%dT%H:%M:%S') ==="
}

if [[ "${1:-}" == "--background" ]]; then
  log_file="${DEPLOY_LOG:-/root/vapp-public-deploy-$(date +%F_%H%M%S).log}"
  nohup env DEPLOY_LOG="$log_file" bash "$0" --foreground >"$log_file" 2>&1 &
  echo "PID: $! | tail -f $log_file"
  exit 0
fi

if [[ "${1:-}" == "--foreground" ]]; then
  shift || true
fi

if [[ -n "$DEPLOY_LOG" ]]; then
  exec >>"$DEPLOY_LOG" 2>&1
fi

run_deploy
