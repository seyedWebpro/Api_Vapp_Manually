#!/usr/bin/env bash
# حذف میرورهای ایران‌سرور (Docker / npm / NuGet) — دیتاسنتر جدید به registry رسمی وصل است
# Usage: sudo bash devops/scripts/clear-iranserver-mirrors.sh
set -euo pipefail

echo "=== clear-iranserver-mirrors $(date -Is) ==="

mkdir -p /etc/docker
cat >/etc/docker/daemon.json <<'EOF'
{
  "dns": ["8.8.8.8", "1.1.1.1", "9.9.9.9"],
  "max-concurrent-downloads": 4,
  "max-concurrent-uploads": 4
}
EOF

if command -v docker >/dev/null 2>&1 && systemctl is-active --quiet docker 2>/dev/null; then
  systemctl daemon-reload
  systemctl restart docker
  sleep 2
  echo "OK: docker daemon.json without iranserver mirror"
  docker info 2>/dev/null | grep -A5 'Registry Mirrors' || true
else
  echo "OK: docker daemon.json written (docker not running yet)"
fi

if command -v npm >/dev/null 2>&1; then
  npm config set registry https://registry.npmjs.org/ 2>/dev/null || true
  npm config delete strict-ssl 2>/dev/null || true
  echo "OK: npm registry → https://registry.npmjs.org/"
fi

if command -v dotnet >/dev/null 2>&1; then
  dotnet nuget remove source iranserver 2>/dev/null || true
  echo "OK: nuget iranserver source removed (if present)"
fi

echo "OK: IranServer mirrors cleared"
