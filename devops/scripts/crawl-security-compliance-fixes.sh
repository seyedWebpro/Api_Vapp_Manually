#!/usr/bin/env bash
# Crawl تأیید فیکس‌های امنیتی/قراردادی ۱–۱۲
# Usage: BASE_URL=http://127.0.0.1:5054 bash devops/scripts/crawl-security-compliance-fixes.sh
set -euo pipefail

BASE_URL="${BASE_URL:-http://127.0.0.1:5054}"
OWNER_PHONE="${OWNER_PHONE:-09920374397}"
TMP="$(mktemp -d)"
PASS=0
FAIL=0
TOKEN=""

cleanup() { rm -rf "$TMP"; }
trap cleanup EXIT

check() {
  if [[ "$2" == "1" || "$2" == "true" ]]; then echo "PASS  $1"; PASS=$((PASS+1))
  else echo "FAIL  $1 (detail=${3:-})"; FAIL=$((FAIL+1)); fi
}

json_get() {
  python3 - "$1" "$2" <<'PY'
import json,sys
d=json.load(open(sys.argv[1],encoding='utf-8'))
cur=d
for p in sys.argv[2].split('.'):
  cur=cur.get(p) if isinstance(cur,dict) else None
  if cur is None: break
print('true' if cur is True else ('false' if cur is False else ('' if cur is None else cur)))
PY
}

echo "=== Security/compliance fixes crawl @ $BASE_URL ==="
code=$(curl -sS -m 10 -o /dev/null -w '%{http_code}' "$BASE_URL/health" 2>/dev/null || true)
check "health 200" "$([[ "$code" == "200" ]] && echo 1 || echo 0)" "$code"
[[ "$code" == "200" ]] || { echo "API unreachable — abort"; exit 1; }

# --- 1 Contact/all blocked without admin ---
code=$(curl -sS -m 10 -o "$TMP/c_all.json" -w '%{http_code}' "$BASE_URL/api/Contact/all" 2>/dev/null || true)
check "1 Contact/all no-token blocked" "$([[ "$code" == "401" || "$code" == "403" ]] && echo 1 || echo 0)" "$code"

# --- 6 Contact upload requires auth ---
code=$(curl -sS -m 10 -o "$TMP/c_up.json" -w '%{http_code}' -X POST "$BASE_URL/api/Contact/1/upload-profile-image" \
  -H 'Authorization: Bearer bad.token' -F 'ImageFile=@/etc/hosts;type=image/jpeg' 2>/dev/null || true)
check "6 Contact upload bad-token 401" "$([[ "$code" == "401" ]] && echo 1 || echo 0)" "$code"

# --- 2/3 User/Role/UserRole blocked without admin ---
for path in /api/User /api/Role /api/UserRole; do
  code=$(curl -sS -m 10 -o "$TMP/adm.json" -w '%{http_code}' "$BASE_URL$path" 2>/dev/null || true)
  check "2/3 $path no-token blocked" "$([[ "$code" == "401" || "$code" == "403" ]] && echo 1 || echo 0)" "$code"
done

# --- login admin ---
login=$(curl -sS -m 20 -X POST "$BASE_URL/api/Auth/login" -H 'Content-Type: application/json' -d "{\"phoneNumber\":\"$OWNER_PHONE\"}" || true)
otp=$(echo "$login" | python3 -c "import sys,json; d=json.load(sys.stdin); print(d.get('otpCode') or '')" 2>/dev/null || true)
succ=$(echo "$login" | python3 -c "import sys,json; d=json.load(sys.stdin); print(d.get('success'))" 2>/dev/null || true)
check "5 Auth login success (Dev)" "$([[ "$succ" == "True" || "$succ" == "true" ]] && echo 1 || echo 0)" "$succ"
check "5 OTP present in Development response" "$([[ -n "$otp" ]] && echo 1 || echo 0)" "len=${#otp}"

verify=$(curl -sS -m 20 -X POST "$BASE_URL/api/Auth/verify-login" -H 'Content-Type: application/json' \
  -d "{\"phoneNumber\":\"$OWNER_PHONE\",\"otpCode\":\"$otp\"}")
TOKEN=$(echo "$verify" | python3 -c "import sys,json; d=json.load(sys.stdin); print((d.get('tokens') or {}).get('accessToken') or '')")
check "admin token issued" "$([[ -n "$TOKEN" ]] && echo 1 || echo 0)" "${#TOKEN}"

auth_get() {
  local url="$1" out="$2"
  curl -sS -m 20 -o "$out" -w '%{http_code}' -H "Authorization: Bearer $TOKEN" -H 'Accept: application/json' "$url" 2>/dev/null || true
}

# --- admin can access ---
code=$(auth_get "$BASE_URL/api/User?pageNumber=1&pageSize=5" "$TMP/users.json")
check "2 Admin GET /User 200" "$([[ "$code" == "200" ]] && echo 1 || echo 0)" "$code"
ps=$(json_get "$TMP/users.json" data.pageSize)
check "9 User list pageSize default/field <=20 or requested" "$([[ -z "$ps" || "$ps" -le 20 ]] && echo 1 || echo 0)" "$ps"

code=$(auth_get "$BASE_URL/api/Contact/all?pageNumber=1&pageSize=5" "$TMP/c_admin.json")
check "1 Admin Contact/all 200" "$([[ "$code" == "200" ]] && echo 1 || echo 0)" "$code"

code=$(auth_get "$BASE_URL/api/Role?pageNumber=1&pageSize=5" "$TMP/roles.json")
check "3 Admin Role 200" "$([[ "$code" == "200" ]] && echo 1 || echo 0)" "$code"

# --- profile still works (non-admin-only path) ---
code=$(auth_get "$BASE_URL/api/User/profile" "$TMP/prof.json")
check "User profile still OK" "$([[ "$code" == "200" ]] && echo 1 || echo 0)" "$code"

# --- 9 pageSize clamp ---
code=$(auth_get "$BASE_URL/api/Wallet/transactions?pageNumber=1&pageSize=500" "$TMP/wal.json")
wps=$(json_get "$TMP/wal.json" data.pageSize)
check "9 Wallet pageSize=500 clamped to 100 or 20" "$([[ -n "$wps" && "$wps" -le 100 ]] && echo 1 || echo 0)" "$wps"

# --- 10 validation errorCode ---
code=$(curl -sS -m 15 -o "$TMP/val.json" -w '%{http_code}' -X POST "$BASE_URL/api/professional-campaigns" \
  -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' -d '{}' 2>/dev/null || true)
ec=$(json_get "$TMP/val.json" errorCode)
check "10 invalid body has errorCode" "$([[ -n "$ec" ]] && echo 1 || echo 0)" "$ec"
tid=$(json_get "$TMP/val.json" traceId)
check "10 invalid body has traceId" "$([[ -n "$tid" ]] && echo 1 || echo 0)" "$tid"

# --- 4 Payment redirect missing ---
code=$(curl -sS -m 10 -o /dev/null -w '%{http_code}' "$BASE_URL/api/Payment/redirect/999999999" 2>/dev/null || true)
check "4 Payment redirect missing 404" "$([[ "$code" == "404" ]] && echo 1 || echo 0)" "$code"

# --- get-user-by-token ---
code=$(curl -sS -m 15 -o "$TMP/bytok.json" -w '%{http_code}' -X POST "$BASE_URL/api/Auth/get-user-by-token" \
  -H 'Content-Type: application/json' -d "{\"token\":\"$TOKEN\"}" 2>/dev/null || true)
check "8 get-user-by-token 200" "$([[ "$code" == "200" ]] && echo 1 || echo 0)" "$code"
ok=$(json_get "$TMP/bytok.json" success)
check "8 get-user-by-token success" "$([[ "$ok" == "true" ]] && echo 1 || echo 0)" "$ok"

code=$(curl -sS -m 15 -o "$TMP/bytok_bad.json" -w '%{http_code}' -X POST "$BASE_URL/api/Auth/get-user-by-token" \
  -H 'Content-Type: application/json' -d '{"token":"bad.token.value"}' 2>/dev/null || true)
bec=$(json_get "$TMP/bytok_bad.json" errorCode)
check "8 bad token has errorCode" "$([[ -n "$bec" ]] && echo 1 || echo 0)" "$bec/$code"

# --- Occasion table still works (frontend contract) ---
code=$(auth_get "$BASE_URL/api/SpecialOccasion/table" "$TMP/occ.json")
check "Occasion table 200" "$([[ "$code" == "200" ]] && echo 1 || echo 0)" "$code"
items=$(python3 - "$TMP/occ.json" <<'PY'
import json,sys
d=json.load(open(sys.argv[1],encoding='utf-8'))
print(len((d.get('data') or {}).get('items') or []))
PY
)
check "Occasion table has items" "$([[ "${items:-0}" -gt 0 ]] && echo 1 || echo 0)" "$items"

# --- 4 simulate: without valid JWT must not complete as anonymous owner ---
# با DisableAuth ممکن است DefaultPolicy شل باشد؛ خود endpoint هنوز userId می‌خواهد.
code=$(curl -sS -m 10 -o "$TMP/sim.json" -w '%{http_code}' -X POST "$BASE_URL/api/Payment/999999999/simulate" 2>/dev/null || true)
# Accept 401 (auth required) or 404/400 (auth bypassed but ownership/not found — not 200 success)
sim_ok=$(python3 - "$TMP/sim.json" "$code" <<'PY'
import json,sys
code=sys.argv[2]
try:
  d=json.load(open(sys.argv[1],encoding='utf-8'))
except Exception:
  d={}
success=d.get('success') is True
print('0' if success or code=='200' else '1')
PY
)
check "4 simulate not anonymously completable" "$sim_ok" "$code"

echo "=== RESULT PASS=$PASS FAIL=$FAIL ==="
[[ "$FAIL" -eq 0 ]]
