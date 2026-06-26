#!/usr/bin/env bash
# docker pull — اول mirror ایران‌سرور؛ اگر image نبود → موقت بدون mirror از docker.io
# Usage: source lib/docker-pull-fallback.sh && docker_pull_with_fallback node:20-alpine

docker_pull_with_fallback() {
  local image="$1"
  echo "docker pull $image ..."
  if docker pull "$image" 2>&1; then
    echo "OK: $image"
    return 0
  fi

  echo "WARN: mirror pull failed for $image — trying docker.io without mirror"
  local daemon_json="/etc/docker/daemon.json"
  local backup="/tmp/docker-daemon.json.bak.$$"

  if [[ -f "$daemon_json" ]] && grep -q 'docker.iranserver.com' "$daemon_json" 2>/dev/null; then
    cp "$daemon_json" "$backup"
    cat >"$daemon_json" <<'EOF'
{
  "dns": ["217.218.127.127", "8.8.8.8", "1.1.1.1"],
  "max-concurrent-downloads": 4,
  "max-concurrent-uploads": 4
}
EOF
    systemctl daemon-reload
    systemctl restart docker
    sleep 3
  fi

  set +e
  docker pull "$image"
  local rc=$?
  set -e

  if [[ -f "$backup" ]]; then
    cp "$backup" "$daemon_json"
    rm -f "$backup"
    systemctl daemon-reload
    systemctl restart docker
    sleep 2
    echo "OK: IranServer Docker mirror restored"
  fi

  if [[ "$rc" -eq 0 ]]; then
    echo "OK: $image (direct docker.io)"
    return 0
  fi

  echo "ERROR: could not pull $image" >&2
  return 1
}

docker_pull_front_base_images() {
  docker_pull_with_fallback "${DOCKER_NODE_IMAGE:-node:20-alpine}"
  docker_pull_with_fallback "${DOCKER_NGINX_IMAGE:-nginx:alpine}"
}
