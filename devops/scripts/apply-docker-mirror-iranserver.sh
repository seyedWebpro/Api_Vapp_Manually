#!/usr/bin/env bash
# Docker registry mirror ایران‌سرور
# Usage: sudo bash apply-docker-mirror-iranserver.sh
set -euo pipefail

if docker info 2>/dev/null | grep -qi 'docker.iranserver.com'; then
  echo "OK: IranServer Docker mirror already active"
  docker info 2>/dev/null | grep -A2 'Registry Mirrors' || true
  exit 0
fi

echo "=== apply-docker-mirror-iranserver $(date -Is) ==="

mkdir -p /etc/docker
cat >/etc/docker/daemon.json <<'EOF'
{
  "dns": ["217.218.127.127", "8.8.8.8", "1.1.1.1"],
  "registry-mirrors": ["https://docker.iranserver.com"],
  "insecure-registries": ["docker.iranserver.com"],
  "max-concurrent-downloads": 4,
  "max-concurrent-uploads": 4
}
EOF

systemctl daemon-reload
systemctl restart docker
sleep 2

docker info | grep -A3 'Registry Mirrors'
docker pull hello-world >/dev/null
echo "OK: IranServer Docker mirror applied"
