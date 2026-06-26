#!/usr/bin/env bash
# تست آمادگی build روی سرور ایران (mirror ایران‌سرور)
# Usage: bash test-iran-build-prereqs.sh
set -euo pipefail

ok=0 fail=0

check() {
  local name="$1" rc="$2"
  if [[ "$rc" == "0" ]]; then
    echo "OK   $name"
    ok=$((ok + 1))
  else
    echo "FAIL $name"
    fail=$((fail + 1))
  fi
}

echo "=== test-iran-build-prereqs $(date -Is) ==="

if docker info 2>/dev/null | grep -qi 'docker.iranserver.com'; then
  check "docker mirror (iranserver)" 0
else
  echo "WARN: run: sudo bash devops/scripts/apply-docker-mirror-iranserver.sh"
  check "docker mirror (iranserver)" 1
fi

docker pull hello-world >/dev/null 2>&1 && check "docker pull hello-world" 0 || check "docker pull hello-world" 1
docker pull node:22-alpine >/dev/null 2>&1 && check "docker pull node:22-alpine (Admin)" 0 || check "docker pull node:22-alpine (Admin)" 1
docker pull nginx:1.27-alpine >/dev/null 2>&1 && check "docker pull nginx:1.27-alpine" 0 || check "docker pull nginx:1.27-alpine" 1

npm_reg="$(npm config get registry 2>/dev/null || echo unknown)"
echo "INFO npm registry: $npm_reg"

if npm view lodash version --registry=https://npm.iranserver.com/repository/npm/ >/dev/null 2>&1; then
  check "npm iranserver (lodash)" 0
else
  check "npm iranserver (lodash)" 1
fi

if npm view react-router-dom@7.9.4 version --registry=https://npm.iranserver.com/repository/npm/ >/dev/null 2>&1; then
  check "npm iranserver (react-router-dom@7.9.4)" 0
else
  echo "     → Admin npm install may need npmmirror fallback or Mac upload"
  check "npm iranserver (react-router-dom@7.9.4)" 1
fi

api_code="$(curl -sS -m10 -o /dev/null -w '%{http_code}' http://127.0.0.1:8080/health 2>/dev/null || echo 000)"
[[ "$api_code" == "200" ]] && check "API health :8080" 0 || check "API health :8080 ($api_code)" 1

echo ""
echo "SUMMARY: OK=$ok FAIL=$fail"
if [[ "$fail" -eq 0 ]]; then
  echo "OK: server-side build should work"
  exit 0
fi
if docker info 2>/dev/null | grep -qi 'docker.iranserver.com' && [[ "$api_code" == "200" ]]; then
  echo "PARTIAL: Docker+API OK — اگر npm Admin fail شد: Mac → deploy-front-upload-dist.sh"
  exit 0
fi
echo "FAIL: bash vapp-iran-update.sh --mirror"
exit 1
