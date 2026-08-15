#!/usr/bin/env bash
# Crawl صحت لاگ‌های پشتیبانی (اولویت ۱–۳)
# — ورود/OTP/خروج کاربر، AUTH_DENY، و ردیف‌های AdminAuditLogs
#
# Usage:
#   BASE_URL=http://127.0.0.1:5054 \
#   AUTH_PHONE=09920374397 \
#   bash devops/scripts/crawl-support-audit.sh
#
# Env:
#   SKIP_AUDIT_SQL=1  — فقط API را چک کند
#   SKIP_LOGOUT=1     — لاگین کامل + logout را رد کند (اگر rate-limit)
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "$0")/../.." && pwd)"
BASE_URL="${BASE_URL:-http://127.0.0.1:5054}"
AUTH_PHONE="${AUTH_PHONE:-09920374397}"
UNKNOWN_PHONE="${UNKNOWN_PHONE:-09120000000}"
SKIP_AUDIT_SQL="${SKIP_AUDIT_SQL:-0}"
SKIP_LOGOUT="${SKIP_LOGOUT:-0}"
TMP_DIR="$(mktemp -d)"
PASS=0
FAIL=0
FROM_UTC="$(date -u -v-20M +%Y-%m-%dT%H:%M:%S 2>/dev/null || date -u -d '20 minutes ago' +%Y-%m-%dT%H:%M:%S)"

cleanup() { rm -rf "$TMP_DIR"; }
trap cleanup EXIT

check() {
  local name="$1" ok="$2"
  if [[ "$ok" == "1" ]]; then
    echo "PASS  $name"
    PASS=$((PASS + 1))
  else
    echo "FAIL  $name"
    FAIL=$((FAIL + 1))
  fi
}

json_get() {
  python3 - "$1" "$2" <<'PY'
import json,sys
path=sys.argv[2].split(".")
with open(sys.argv[1],encoding="utf-8") as f:
    data=json.load(f)
cur=data
for p in path:
    if not isinstance(cur,dict):
        cur=None
        break
    if p in cur:
        cur=cur[p]; continue
    alt=p[:1].upper()+p[1:]
    if alt in cur:
        cur=cur[alt]; continue
    low=p[:1].lower()+p[1:]
    cur=cur.get(low)
if isinstance(cur,bool):
    print("true" if cur else "false")
elif cur is None:
    print("")
else:
    print(cur)
PY
}

http_json() {
  local method="$1" url="$2" body="${3:-}" out="$4"
  echo '{}' > "$out"
  local code
  if [[ -n "$body" ]]; then
    code=$(curl -sS -m 25 -X "$method" "$url" \
      -H "Content-Type: application/json" \
      -H "Accept: application/json" \
      -d "$body" \
      -o "$out" -w "%{http_code}" 2>/dev/null || echo "000")
  else
    code=$(curl -sS -m 25 -X "$method" "$url" \
      -H "Accept: application/json" \
      -o "$out" -w "%{http_code}" 2>/dev/null || echo "000")
  fi
  [[ -s "$out" ]] || echo '{}' > "$out"
  printf '%s' "$code"
}

audit_has_action() {
  local action="$1"
  local out
  out="$(MSSQL_SA_PASSWORD="${MSSQL_SA_PASSWORD:-}" bash "$ROOT_DIR/devops/scripts/audit-search.sh" \
    --action "$action" --from "$FROM_UTC" --lines 30 2>&1 || true)"
  # ردیف داده معمولاً با عدد Id شروع می‌شود و Action را دارد — نه فقط متن SQL
  echo "$out" | grep -E "^[0-9]+\|" | grep -F "$action" >/dev/null 2>&1
}

echo "=== crawl-support-audit ==="
echo "BASE_URL=$BASE_URL  FROM_UTC=$FROM_UTC"
echo

# --- 1) لاگین با شماره ناشناس → UserLoginFailed + TraceId ---
CODE=$(http_json POST "$BASE_URL/api/Auth/login" \
  "{\"phoneNumber\":\"$UNKNOWN_PHONE\"}" \
  "$TMP_DIR/login_unknown.json")
TRACE=$(json_get "$TMP_DIR/login_unknown.json" "traceId")
SUCCESS=$(json_get "$TMP_DIR/login_unknown.json" "success")
check "login unknown phone returns failure" "$([[ "$SUCCESS" == "false" && "$CODE" != "000" ]] && echo 1 || echo 0)"
check "login unknown response has traceId" "$([[ -n "$TRACE" ]] && echo 1 || echo 0)"

# --- 2) لاگین شماره واقعی (200 ارسال OTP یا 429 rate-limit — هر دو معتبر) ---
CODE=$(http_json POST "$BASE_URL/api/Auth/login" \
  "{\"phoneNumber\":\"$AUTH_PHONE\"}" \
  "$TMP_DIR/login_ok.json")
LOGIN_OK=$(json_get "$TMP_DIR/login_ok.json" "success")
OTP_TRACE=$(json_get "$TMP_DIR/login_ok.json" "traceId")
OTP_CODE=$(json_get "$TMP_DIR/login_ok.json" "otpCode")
check "login known phone responds (200/429)" "$([[ "$CODE" =~ ^(200|201|429)$ ]] && echo 1 || echo 0)"
check "login known phone has traceId" "$([[ -n "$OTP_TRACE" ]] && echo 1 || echo 0)"

CODE=$(http_json POST "$BASE_URL/api/Auth/verify-login" \
  "{\"phoneNumber\":\"$AUTH_PHONE\",\"otpCode\":\"0000\"}" \
  "$TMP_DIR/verify_bad.json")
VERIFY_OK=$(json_get "$TMP_DIR/verify_bad.json" "success")
VERIFY_TRACE=$(json_get "$TMP_DIR/verify_bad.json" "traceId")
check "verify wrong OTP fails" "$([[ "$VERIFY_OK" == "false" ]] && echo 1 || echo 0)"
check "verify wrong OTP has traceId" "$([[ -n "$VERIFY_TRACE" ]] && echo 1 || echo 0)"

# --- 3) Bearer نامعتبر → 401 + TraceId ---
CODE=$(curl -sS -m 10 -o "$TMP_DIR/deny.json" -w "%{http_code}" \
  -H "Authorization: Bearer invalid.token.here" \
  -H "Accept: application/json" \
  "$BASE_URL/api/User/profile" || echo "000")
DENY_TRACE=$(json_get "$TMP_DIR/deny.json" "traceId")
check "invalid bearer returns 401" "$([[ "$CODE" == "401" ]] && echo 1 || echo 0)"
check "401 response has traceId" "$([[ -n "$DENY_TRACE" ]] && echo 1 || echo 0)"

# --- 4) لاگین موفق + logout (اگر OTP در پاسخ dev باشد) ---
if [[ "$SKIP_LOGOUT" != "1" && -n "$OTP_CODE" && "$LOGIN_OK" == "true" ]]; then
  CODE=$(http_json POST "$BASE_URL/api/Auth/verify-login" \
    "{\"phoneNumber\":\"$AUTH_PHONE\",\"otpCode\":\"$OTP_CODE\"}" \
    "$TMP_DIR/verify_ok.json")
  VOK=$(json_get "$TMP_DIR/verify_ok.json" "success")
  TOK=$(python3 - "$TMP_DIR/verify_ok.json" <<'PY'
import json,sys
d=json.load(open(sys.argv[1],encoding="utf-8"))
t=d.get("tokens") or d.get("Tokens") or {}
print(t.get("accessToken") or t.get("AccessToken") or "")
PY
)
  check "verify correct OTP succeeds" "$([[ "$VOK" == "true" && -n "$TOK" ]] && echo 1 || echo 0)"
  if [[ -n "$TOK" ]]; then
    CODE=$(curl -sS -m 10 -o "$TMP_DIR/logout.json" -w "%{http_code}" \
      -X POST "$BASE_URL/api/Auth/logout" \
      -H "Authorization: Bearer $TOK" \
      -H "Accept: application/json" || echo "000")
    LOUT=$(json_get "$TMP_DIR/logout.json" "success")
    LTRACE=$(json_get "$TMP_DIR/logout.json" "traceId")
    check "logout succeeds" "$([[ "$LOUT" == "true" && "$CODE" == "200" ]] && echo 1 || echo 0)"
    check "logout has traceId" "$([[ -n "$LTRACE" ]] && echo 1 || echo 0)"
  fi
else
  echo "SKIP  logout flow (no otpCode / rate-limited / SKIP_LOGOUT=1)"
fi

# --- 5) SQL Audit ---
if [[ "$SKIP_AUDIT_SQL" != "1" ]]; then
  echo
  echo "--- AdminAuditLogs checks ---"
  if audit_has_action "Auth.UserLoginFailed"; then
    check "DB has Auth.UserLoginFailed" 1
  else
    check "DB has Auth.UserLoginFailed" 0
  fi
  if audit_has_action "Auth.OtpSent" || [[ "$LOGIN_OK" != "true" ]]; then
    # اگر OTP ارسال نشده (مثلاً فقط rate-limit)، OtpSent اختیاری است
    if audit_has_action "Auth.OtpSent"; then
      check "DB has Auth.OtpSent" 1
    else
      check "DB has Auth.OtpSent (optional when rate-limited)" 1
      echo "INFO  OtpSent not found in window (likely rate-limited earlier)"
    fi
  else
    check "DB has Auth.OtpSent" 0
  fi
  if [[ -n "${TOK:-}" ]]; then
    if audit_has_action "Auth.UserLoginSucceeded"; then
      check "DB has Auth.UserLoginSucceeded" 1
    else
      check "DB has Auth.UserLoginSucceeded" 0
    fi
    if audit_has_action "Auth.Logout"; then
      check "DB has Auth.Logout" 1
    else
      check "DB has Auth.Logout" 0
    fi
  fi
else
  echo "SKIP  SQL audit checks (SKIP_AUDIT_SQL=1)"
fi

echo
echo "=== result: PASS=$PASS FAIL=$FAIL ==="
if [[ "$FAIL" -gt 0 ]]; then
  exit 1
fi
exit 0
