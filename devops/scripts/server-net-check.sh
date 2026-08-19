#!/usr/bin/env bash
# تست دسترسی اینترنت سرور — بدون میرور ایران‌سرور
# Usage: bash devops/scripts/server-net-check.sh
set -euo pipefail

echo "=== Vapp server net check $(date -Is) ==="
echo "Host: $(hostname) | IP check: $(curl -4 -sS -m 8 https://ifconfig.me 2>/dev/null || echo '?')"
echo ""

ok=0
fail=0
check() {
  local name="$1" url="$2"
  local code
  code="$(curl -4 -sS -o /dev/null -w '%{http_code}' --connect-timeout 8 --max-time 20 "$url" 2>/dev/null || echo FAIL)"
  printf '%-28s %s  %s\n' "$name" "$code" "$url"
  if [[ "$code" == "FAIL" || "$code" == "000" ]]; then
    fail=$((fail + 1))
  else
    ok=$((ok + 1))
  fi
}

check "Docker Hub" "https://registry-1.docker.io/v2/"
check "GitHub" "https://github.com"
check "MCR" "https://mcr.microsoft.com/v2/"
check "npm" "https://registry.npmjs.org/"
check "NuGet" "https://api.nuget.org/v3/index.json"
check "Ubuntu archive" "http://archive.ubuntu.com/ubuntu/"

echo ""
echo "OK=$ok FAIL=$fail"

if [[ "$fail" -eq 0 ]]; then
  echo "RESULT: native registries OK — use vapp-iran-update without --mirror"
  exit 0
fi

echo "RESULT: some endpoints failed — retry later."
echo "        فقط اگر لازم شد: bash devops/scripts/apply-build-mirrors-iranserver.sh"
exit 1
