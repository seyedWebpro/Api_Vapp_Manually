#!/usr/bin/env bash
# Deploy API — git pull + docker compose (Vapp: .NET 8)
# reuse: API_REPO_DIR، COMPOSE_FILE، ENV_FILE، container name، health URL
#
# Usage (روی سرور):
#   bash ~/Api_Vapp_Manually/devops/scripts/deploy-api.sh
#   RELOAD_NGINX=1 bash deploy-api.sh
#   ALLOW_SLOW_START=1 bash deploy-api.sh
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=lib/docker-pull-fallback.sh
source "$SCRIPT_DIR/lib/docker-pull-fallback.sh"
API_REPO_DIR="${API_REPO_DIR:-$HOME/Api_Vapp_Manually}"
API_BRANCH="${API_BRANCH:-main}"
COMPOSE_FILE="${COMPOSE_FILE:-docker/docker-compose.production.yml}"
ENV_FILE="${ENV_FILE:-$API_REPO_DIR/docker/.env}"

echo "=== deploy-api started $(date -Is) ==="

cd "$API_REPO_DIR"

if [[ ! -f "$ENV_FILE" ]]; then
  if [[ -f "$API_REPO_DIR/devops/.env.server.example" ]]; then
    cp "$API_REPO_DIR/devops/.env.server.example" "$ENV_FILE"
    echo "WARN: created $ENV_FILE from example — edit SA_PASSWORD and Jwt__Secret before production use." >&2
  else
    printf 'SA_PASSWORD=Vapp@Secure2025!\nAPI_PORT_MAPPING=127.0.0.1:8080:8080\n' > "$ENV_FILE"
  fi
fi

if [[ -d "$API_REPO_DIR/.git" ]]; then
  git pull origin "$API_BRANCH"
fi

BUILD_PULL="${API_BUILD_PULL:-auto}"
if [[ "$BUILD_PULL" == "auto" ]]; then
  if docker_api_base_images_cached; then
    BUILD_PULL="false"
    echo "NOTE: dotnet base images cached — build با --pull=false (mcr از ایران block است)"
  else
    BUILD_PULL="always"
    docker_pull_api_base_images || true
    if docker_api_base_images_cached; then
      BUILD_PULL="false"
    else
      echo "ERROR: dotnet base images not on server — build از Mac:" >&2
      echo "  SERVER=root@185.116.162.233 bash devops/scripts/deploy-api-upload-image.sh" >&2
      exit 1
    fi
  fi
fi

docker compose -f "$COMPOSE_FILE" --env-file "$ENV_FILE" build --pull="$BUILD_PULL" api
docker compose -f "$COMPOSE_FILE" --env-file "$ENV_FILE" up -d --no-deps --force-recreate --no-build api

if [[ "${RELOAD_NGINX:-0}" == "1" ]]; then
  bash "$SCRIPT_DIR/apply-nginx.sh"
fi

api_code="000"
for attempt in 1 2 3 4 5 6; do
  sleep 10
  api_code="$(curl -sS -m 15 -o /dev/null -w '%{http_code}' http://127.0.0.1:8080/health 2>/dev/null || echo "000")"
  echo "API health attempt $attempt/6: $api_code"
  [[ "$api_code" == "200" ]] && break
done

echo "API:$api_code"
docker ps --filter name=vapp_api_prod --format 'table {{.Names}}\t{{.Status}}'
echo "=== deploy-api done $(date -Is) ==="

if [[ "$api_code" != "200" ]]; then
  echo "WARN: API health returned $api_code (migration/startup may still be running — retry health-check.sh)" >&2
  if [[ "${ALLOW_SLOW_START:-0}" == "1" ]]; then
    exit 0
  fi
  exit 1
fi
