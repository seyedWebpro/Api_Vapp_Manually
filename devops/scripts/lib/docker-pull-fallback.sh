#!/usr/bin/env bash
# docker pull از registry رسمی — این دیتاسنتر به docker.io / MCR وصل است
# Usage: source lib/docker-pull-fallback.sh && docker_pull_with_fallback node:20-alpine

docker_pull_with_fallback() {
  local image="$1"
  echo "docker pull $image ..."
  if docker pull "$image" 2>&1; then
    echo "OK: $image"
    return 0
  fi
  echo "ERROR: could not pull $image" >&2
  return 1
}

docker_pull_front_base_images() {
  docker_pull_with_fallback "${DOCKER_NODE_IMAGE:-node:22-alpine}"
}

DOTNET_SDK_IMAGE="${DOTNET_SDK_IMAGE:-mcr.microsoft.com/dotnet/sdk:8.0}"
DOTNET_ASPNET_IMAGE="${DOTNET_ASPNET_IMAGE:-mcr.microsoft.com/dotnet/aspnet:8.0}"

docker_api_base_images_cached() {
  docker image inspect "$DOTNET_SDK_IMAGE" >/dev/null 2>&1 \
    && docker image inspect "$DOTNET_ASPNET_IMAGE" >/dev/null 2>&1
}

docker_pull_api_base_images() {
  echo "=== docker pull API base images $(date '+%Y-%m-%dT%H:%M:%S') ==="
  if docker_api_base_images_cached; then
    echo "OK: dotnet sdk + aspnet already cached locally"
    return 0
  fi

  set +e
  docker pull "$DOTNET_SDK_IMAGE"
  local sdk_rc=$?
  docker pull "$DOTNET_ASPNET_IMAGE"
  local aspnet_rc=$?
  set -e

  if [[ "$sdk_rc" -eq 0 && "$aspnet_rc" -eq 0 ]]; then
    echo "OK: dotnet base images pulled"
    return 0
  fi

  echo "WARN: could not pull dotnet base images from mcr — fallback: Mac upload" >&2
  echo "      SERVER=vapp-prod bash devops/scripts/deploy-api-upload-image.sh" >&2
  return 1
}
