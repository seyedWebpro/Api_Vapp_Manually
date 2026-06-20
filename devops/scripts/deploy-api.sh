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

docker compose -f "$COMPOSE_FILE" --env-file "$ENV_FILE" up -d --build --force-recreate api

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
