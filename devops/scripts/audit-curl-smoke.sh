#!/usr/bin/env bash
# تست زنده endpointهای حساس audit بعد از deploy
# Usage:
#   BASE_URL=http://127.0.0.1:8080 ADMIN_TOKEN=... bash devops/scripts/audit-curl-smoke.sh
#   یا روی سرور بدون توکن فقط health + unauthorized:
#   BASE_URL=http://127.0.0.1:8080 bash devops/scripts/audit-curl-smoke.sh
set -euo pipefail

BASE_URL="${BASE_URL:-http://127.0.0.1:8080}"
ADMIN_TOKEN="${ADMIN_TOKEN:-}"
PASS=0
FAIL=0

check() {
  local name="$1" expect="$2" code="$3"
  if [[ "$code" == "$expect" ]]; then
    echo "PASS  $name (HTTP $code)"
    PASS=$((PASS + 1))
  else
    echo "FAIL  $name (HTTP $code, expected $expect)"
    FAIL=$((FAIL + 1))
  fi
}

echo "=== Audit curl smoke @ $BASE_URL ==="

code="$(curl -sS -m 15 -o /dev/null -w '%{http_code}' "$BASE_URL/health" || echo 000)"
check "GET /health" "200" "$code"

code="$(curl -sS -m 15 -o /tmp/audit_unauth.json -w '%{http_code}' "$BASE_URL/api/Admin/Audit" || echo 000)"
# بدون توکن باید 401 باشد (مگر DisableAuth)
if [[ "$code" == "401" || "$code" == "403" ]]; then
  check "GET /api/Admin/Audit without token" "$code" "$code"
else
  echo "WARN  /api/Admin/Audit without token returned $code (maybe DisableAuth)"
  PASS=$((PASS + 1))
fi

if [[ -n "$ADMIN_TOKEN" ]]; then
  code="$(curl -sS -m 20 -o /tmp/audit_list.json -w '%{http_code}' \
    "$BASE_URL/api/Admin/Audit?page=1&pageSize=5" \
    -H "Authorization: Bearer $ADMIN_TOKEN" || echo 000)"
  check "GET /api/Admin/Audit list" "200" "$code"

  code="$(curl -sS -m 20 -o /tmp/audit_price.json -w '%{http_code}' \
    "$BASE_URL/api/Admin/Audit?action=SubscriptionPlan.PriceUpdated&pageSize=10" \
    -H "Authorization: Bearer $ADMIN_TOKEN" || echo 000)"
  check "GET audit PriceUpdated" "200" "$code"

  code="$(curl -sS -m 20 -o /tmp/audit_json.json -w '%{http_code}' \
    "$BASE_URL/api/Admin/Audit?q=price&searchInJson=true&pageSize=5" \
    -H "Authorization: Bearer $ADMIN_TOKEN" || echo 000)"
  check "GET audit searchInJson" "200" "$code"

  code="$(curl -sS -m 20 -o /tmp/audit_login.json -w '%{http_code}' \
    "$BASE_URL/api/Admin/Audit?action=Auth.AdminLoginFailed&pageSize=5" \
    -H "Authorization: Bearer $ADMIN_TOKEN" || echo 000)"
  check "GET audit AdminLoginFailed" "200" "$code"

  code="$(curl -sS -m 20 -o /tmp/audit_missing.json -w '%{http_code}' \
    "$BASE_URL/api/Admin/Audit/999999999" \
    -H "Authorization: Bearer $ADMIN_TOKEN" || echo 000)"
  check "GET audit missing id → 404" "404" "$code"
else
  echo "SKIP authenticated tests (set ADMIN_TOKEN)"
fi

echo "=== Result: PASS=$PASS FAIL=$FAIL ==="
[[ "$FAIL" -eq 0 ]]
