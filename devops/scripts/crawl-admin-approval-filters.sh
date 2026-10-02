#!/usr/bin/env bash
# Crawl تست فیلتر، صفحه‌بندی و ترتیب صف تأیید قالب / پیام / محتوا
# Usage: BASE_URL=http://127.0.0.1:5054 bash devops/scripts/crawl-admin-approval-filters.sh
#        SKIP_API_RESTART=1 SKIP_UNIT=1 bash devops/scripts/crawl-admin-approval-filters.sh
set -euo pipefail
export PATH="/usr/bin:/bin:/usr/sbin:/sbin:/usr/local/bin:${PATH:-}"

ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
cd "$ROOT"
BASE="${BASE_URL:-http://127.0.0.1:5054}"
ADMIN_PHONE="${ADMIN_PHONE:-09920374397}"
SKIP_API_RESTART="${SKIP_API_RESTART:-0}"
SKIP_UNIT="${SKIP_UNIT:-0}"
LOG=/tmp/vapp-admin-approval-crawl.log
PIDFILE=/tmp/vapp-admin-approval-crawl.pid
TMP_DIR="$(mktemp -d)"
PASS=0
FAIL=0
SUFFIX="$(date +%s)"
TOKEN=""
AUTH_HDR=()

cleanup() { rm -rf "$TMP_DIR"; }
trap cleanup EXIT

json_get() {
  local file="$1" expr="$2"
  python3 - "$file" "$expr" <<'PY'
import json,sys
path=sys.argv[2].split(".")
with open(sys.argv[1],encoding="utf-8") as f:
    data=json.load(f)
cur=data
for p in path:
    if cur is None:
        break
    if isinstance(cur, list) and p.isdigit():
        i=int(p)
        cur = cur[i] if 0 <= i < len(cur) else None
        continue
    if isinstance(cur, dict):
        if p in cur:
            cur=cur[p]
            continue
        found=None
        for k,v in cur.items():
            if k.lower()==p.lower():
                found=v
                break
        cur=found
        continue
    cur=None
    break
if isinstance(cur,bool):
    print("true" if cur else "false")
elif cur is None:
    print("")
else:
    print(cur)
PY
}

assert_eq() {
  local name="$1" expected="$2" actual="$3"
  if [[ "$expected" == "$actual" ]]; then
    echo "PASS: $name (got=$actual)"
    PASS=$((PASS+1))
  else
    echo "FAIL: $name expected=$expected got=$actual"
    FAIL=$((FAIL+1))
  fi
}

set_token() {
  TOKEN="$1"
  if [[ -n "$TOKEN" ]]; then
    AUTH_HDR=(-H "Authorization: Bearer $TOKEN")
  else
    AUTH_HDR=()
  fi
}

req() {
  local method="$1" path="$2" out="$3"
  shift 3
  if [[ ${#AUTH_HDR[@]} -gt 0 ]]; then
    curl -s -w "%{http_code}" -o "$out" -X "$method" --get "${BASE}${path}" \
      -H "Accept: application/json" \
      "${AUTH_HDR[@]}" \
      "$@" || true
  else
    curl -s -w "%{http_code}" -o "$out" -X "$method" --get "${BASE}${path}" \
      -H "Accept: application/json" \
      "$@" || true
  fi
}

# GET با queryهای فارسی (urlencode امن)
req_get() {
  local path="$1" out="$2"
  shift 2
  if [[ ${#AUTH_HDR[@]} -gt 0 ]]; then
    curl -s -w "%{http_code}" -o "$out" --get "${BASE}${path}" \
      -H "Accept: application/json" \
      "${AUTH_HDR[@]}" \
      "$@" || true
  else
    curl -s -w "%{http_code}" -o "$out" --get "${BASE}${path}" \
      -H "Accept: application/json" \
      "$@" || true
  fi
}

admin_login() {
  local login_out="$TMP_DIR/admin_login.json" verify_out="$TMP_DIR/admin_verify.json"
  local code otp token attempt wait_s
  for attempt in 1 2 3 4 5; do
    code=$(curl -sS -m 25 -o "$login_out" -w "%{http_code}" -X POST \
      -H "Content-Type: application/json" \
      -d "{\"phoneNumber\":\"$ADMIN_PHONE\"}" \
      "$BASE/api/Auth/admin/login" || echo 000)
    if [[ "$code" == "429" ]]; then
      wait_s=$(json_get "$login_out" retryAfterSeconds)
      wait_s="${wait_s:-60}"
      echo "OTP rate-limited — waiting ${wait_s}s (attempt $attempt)"
      sleep "$wait_s"
      sleep 2
      continue
    fi
    if [[ "$code" != "200" ]]; then
      echo "admin login HTTP=$code body=$(head -c 300 "$login_out" 2>/dev/null || true)"
      return 1
    fi
    break
  done
  if [[ "$code" != "200" ]]; then
    echo "admin login still rate-limited after retries"
    return 1
  fi
  otp=$(json_get "$login_out" otpCode)
  if [[ -z "$otp" && -n "${TEST_OTP:-}" ]]; then
    otp="$TEST_OTP"
  fi
  # Development often nests otp under data
  if [[ -z "$otp" ]]; then
    otp=$(json_get "$login_out" data.otpCode)
  fi
  if [[ -z "$otp" ]]; then
    echo "missing otpCode (Development OTP required)"
    return 1
  fi
  code=$(curl -sS -m 25 -o "$verify_out" -w "%{http_code}" -X POST \
    -H "Content-Type: application/json" \
    -d "{\"phoneNumber\":\"$ADMIN_PHONE\",\"otpCode\":\"$otp\"}" \
    "$BASE/api/Auth/admin/verify-login" || echo 000)
  if [[ "$code" != "200" ]]; then
    echo "admin verify HTTP=$code body=$(head -c 300 "$verify_out" 2>/dev/null || true)"
    return 1
  fi
  token=$(python3 - "$verify_out" <<'PY'
import json,sys
d=json.load(open(sys.argv[1],encoding='utf-8'))
data=d.get('data') or d.get('Data') or d
t=(data.get('tokens') or data.get('Tokens') or d.get('tokens') or d.get('Tokens') or {})
print(t.get('accessToken') or t.get('AccessToken') or '')
PY
)
  [[ -n "$token" ]] || return 1
  set_token "$token"
  echo "ADMIN_AUTH_OK"
}

create_template() {
  local out="$1" name="$2" content="$3" desc="$4"
  if [[ ${#AUTH_HDR[@]} -gt 0 ]]; then
    curl -s -w "%{http_code}" -o "$out" -X POST "$BASE/api/Template" \
      "${AUTH_HDR[@]}" \
      -F "Name=${name}" \
      -F "Content=${content}" \
      -F "Description=${desc}" || true
  else
    curl -s -w "%{http_code}" -o "$out" -X POST "$BASE/api/Template" \
      -F "Name=${name}" \
      -F "Content=${content}" \
      -F "Description=${desc}" || true
  fi
}

if [[ "$SKIP_UNIT" != "1" ]]; then
  echo "===== UNIT TESTS ====="
  dotnet test Tests/Api_Vapp.Tests.csproj --nologo \
    --filter "FullyQualifiedName~AdminApprovalListFilterTests|FullyQualifiedName~AdminApprovalQueueOrderingTests" \
    --logger "console;verbosity=minimal"
  echo "UNIT_OK"
fi

if [[ "$SKIP_API_RESTART" != "1" ]]; then
  echo "===== RESTART API ====="
  for p in $(lsof -t -iTCP:5054 -sTCP:LISTEN 2>/dev/null || true); do kill "$p" 2>/dev/null || true; done
  sleep 2
  for p in $(lsof -t -iTCP:5054 -sTCP:LISTEN 2>/dev/null || true); do kill -9 "$p" 2>/dev/null || true; done
  sleep 1

  nohup env ASPNETCORE_ENVIRONMENT=Development \
    dotnet exec bin/Debug/net8.0/Api_Vapp.dll --urls "http://127.0.0.1:5054" \
    > "$LOG" 2>&1 &
  echo $! > "$PIDFILE"
fi

READY=0
for i in $(seq 1 90); do
  code=$(curl -s -o /dev/null -w "%{http_code}" --max-time 10 "$BASE/health" || echo 000)
  if [[ "$code" == "200" ]]; then READY=1; break; fi
  if [[ "$SKIP_API_RESTART" != "1" ]] && ! kill -0 "$(cat "$PIDFILE")" 2>/dev/null; then
    echo "API_DIED_EARLY"
    tail -40 "$LOG" || true
    exit 1
  fi
  sleep 2
done
if [[ "$READY" != "1" ]]; then
  echo "API_NOT_READY"
  tail -40 "$LOG" || true
  exit 1
fi
echo "API_READY"

# Admin endpoints need JWT when DisableAuth is off
HTTP=$(req_get "/api/Admin/TemplateApproval" "$TMP_DIR/auth_probe.json" \
  --data-urlencode "page=1" --data-urlencode "pageSize=1")
if [[ "$HTTP" != "200" ]]; then
  echo "Admin list without token HTTP=$HTTP — trying admin login..."
  if ! admin_login; then
    echo "FAIL: could not authenticate as admin"
    exit 1
  fi
else
  echo "INFO: Admin list allowed without token (DisableAuth/dev)"
fi

echo "===== SEED TEMPLATE ====="
HTTP=$(create_template "$TMP_DIR/template.json" "قالب کراول ${SUFFIX}" "سلام هلدینگ کراول ${SUFFIX} برای تست فیلتر" "توضیح کراول")
SC=$(json_get "$TMP_DIR/template.json" statusCode)
TEMPLATE_ID=$(json_get "$TMP_DIR/template.json" data.id)
assert_eq "create template status" "201" "$SC"
echo "TEMPLATE_ID=$TEMPLATE_ID"

echo "===== TEMPLATE APPROVAL API ====="
HTTP=$(req_get "/api/Admin/TemplateApproval" "$TMP_DIR/tpl_page1.json" \
  --data-urlencode "page=1" --data-urlencode "pageSize=1")
SC=$(json_get "$TMP_DIR/tpl_page1.json" statusCode)
TC=$(json_get "$TMP_DIR/tpl_page1.json" data.totalCount)
TP=$(json_get "$TMP_DIR/tpl_page1.json" data.totalPages)
PS=$(json_get "$TMP_DIR/tpl_page1.json" data.pageSize)
IC=$(python3 - "$TMP_DIR/tpl_page1.json" <<'PY'
import json,sys
d=json.load(open(sys.argv[1],encoding='utf-8'))
print(len(((d.get('data') or {}).get('items') or [])))
PY
)
assert_eq "template list status" "200" "$SC"
assert_eq "template pageSize" "1" "$PS"
assert_eq "template page1 item count" "1" "$IC"
if [[ "$TC" -gt 1 ]]; then
  assert_eq "template totalPages gt 1" "true" "$([[ "$TP" -gt 1 ]] && echo true || echo false)"
fi

HTTP=$(req_get "/api/Admin/TemplateApproval" "$TMP_DIR/tpl_search.json" \
  --data-urlencode "search=هلدینگ کراول ${SUFFIX}" \
  --data-urlencode "page=1" --data-urlencode "pageSize=20")
SC=$(json_get "$TMP_DIR/tpl_search.json" statusCode)
FOUND=$(python3 - "$TMP_DIR/tpl_search.json" "$TEMPLATE_ID" <<'PY'
import json,sys
d=json.load(open(sys.argv[1],encoding='utf-8'))
tid=int(sys.argv[2])
items=((d.get('data') or {}).get('items') or [])
print('yes' if any(i.get('id')==tid for i in items) else 'no')
PY
)
assert_eq "template search status" "200" "$SC"
assert_eq "template search finds seeded item" "yes" "$FOUND"

USER_ID=$(json_get "$TMP_DIR/tpl_search.json" data.items.0.userId)
if [[ -n "$USER_ID" ]]; then
  HTTP=$(req_get "/api/Admin/TemplateApproval" "$TMP_DIR/tpl_user.json" \
    --data-urlencode "userSearch=${USER_ID}" \
    --data-urlencode "page=1" --data-urlencode "pageSize=20")
  SC=$(json_get "$TMP_DIR/tpl_user.json" statusCode)
  assert_eq "template userSearch status" "200" "$SC"
fi

HTTP=$(req_get "/api/Admin/TemplateApproval" "$TMP_DIR/tpl_empty.json" \
  --data-urlencode "search=__no_match_${SUFFIX}__" \
  --data-urlencode "page=1" --data-urlencode "pageSize=20")
TC=$(json_get "$TMP_DIR/tpl_empty.json" data.totalCount)
assert_eq "template empty search totalCount" "0" "$TC"

echo "===== MESSAGE APPROVAL API ====="
HTTP=$(req_get "/api/Admin/MessageApproval" "$TMP_DIR/msg_page1.json" \
  --data-urlencode "page=1" --data-urlencode "pageSize=1")
SC=$(json_get "$TMP_DIR/msg_page1.json" statusCode)
PS=$(json_get "$TMP_DIR/msg_page1.json" data.pageSize)
assert_eq "message list status" "200" "$SC"
assert_eq "message pageSize" "1" "$PS"

HTTP=$(req_get "/api/Admin/MessageApproval" "$TMP_DIR/msg_empty.json" \
  --data-urlencode "search=__no_match_${SUFFIX}__" \
  --data-urlencode "page=1" --data-urlencode "pageSize=20")
SC=$(json_get "$TMP_DIR/msg_empty.json" statusCode)
TC=$(json_get "$TMP_DIR/msg_empty.json" data.totalCount)
assert_eq "message empty search status" "200" "$SC"
assert_eq "message empty search totalCount" "0" "$TC"

HTTP=$(req_get "/api/Admin/MessageApproval/pending" "$TMP_DIR/msg_pending.json" \
  --data-urlencode "page=1" --data-urlencode "pageSize=5")
SC=$(json_get "$TMP_DIR/msg_pending.json" statusCode)
assert_eq "message pending status" "200" "$SC"

echo "===== EDITED ITEM FLOATS TO TOP ====="
HTTP=$(create_template "$TMP_DIR/template_old.json" "قالب قدیمی کراول ${SUFFIX}" "متن اولیه قدیمی کراول ${SUFFIX}" "قدیمی")
SC=$(json_get "$TMP_DIR/template_old.json" statusCode)
OLD_TEMPLATE_ID=$(json_get "$TMP_DIR/template_old.json" data.id)
assert_eq "create older template status" "201" "$SC"

HTTP=$(create_template "$TMP_DIR/template_new.json" "قالب تازه کراول ${SUFFIX}" "متن تازه کراول ${SUFFIX}" "تازه")
SC=$(json_get "$TMP_DIR/template_new.json" statusCode)
NEW_TEMPLATE_ID=$(json_get "$TMP_DIR/template_new.json" data.id)
assert_eq "create newer template status" "201" "$SC"

if [[ ${#AUTH_HDR[@]} -gt 0 ]]; then
  HTTP=$(curl -s -w "%{http_code}" -o "$TMP_DIR/template_edit.json" -X POST \
    "$BASE/api/Template/${OLD_TEMPLATE_ID}/update" \
    "${AUTH_HDR[@]}" \
    -F "Content=متن ویرایش‌شده کراول ${SUFFIX} باید اول صف باشد" || true)
else
  HTTP=$(curl -s -w "%{http_code}" -o "$TMP_DIR/template_edit.json" -X POST \
    "$BASE/api/Template/${OLD_TEMPLATE_ID}/update" \
    -F "Content=متن ویرایش‌شده کراول ${SUFFIX} باید اول صف باشد" || true)
fi
SC=$(json_get "$TMP_DIR/template_edit.json" statusCode)
assert_eq "edit older template status" "200" "$SC"

HTTP=$(req_get "/api/Admin/TemplateApproval" "$TMP_DIR/tpl_order.json" \
  --data-urlencode "status=Pending" \
  --data-urlencode "search=${SUFFIX}" \
  --data-urlencode "page=1" --data-urlencode "pageSize=5")
SC=$(json_get "$TMP_DIR/tpl_order.json" statusCode)
FIRST_ID=$(json_get "$TMP_DIR/tpl_order.json" data.items.0.id)
FIRST_UPDATED=$(json_get "$TMP_DIR/tpl_order.json" data.items.0.updatedAt)
assert_eq "template order list status" "200" "$SC"
assert_eq "edited template is first among matching pending" "$OLD_TEMPLATE_ID" "$FIRST_ID"
if [[ -n "$FIRST_UPDATED" ]]; then
  echo "PASS: edited template has updatedAt (got=$FIRST_UPDATED)"
  PASS=$((PASS+1))
else
  echo "FAIL: edited template missing updatedAt"
  FAIL=$((FAIL+1))
fi

ORDER_OK=$(python3 - "$TMP_DIR/tpl_order.json" "$OLD_TEMPLATE_ID" "$NEW_TEMPLATE_ID" <<'PY'
import json,sys
d=json.load(open(sys.argv[1],encoding='utf-8'))
old_id,new_id=int(sys.argv[2]),int(sys.argv[3])
ids=[i.get('id') for i in ((d.get('data') or {}).get('items') or [])]
try:
    print('yes' if ids.index(old_id) < ids.index(new_id) else 'no')
except ValueError:
    print('no')
PY
)
assert_eq "edited older template before newer created" "yes" "$ORDER_OK"

HTTP=$(req_get "/api/Admin/QuickSendApproval" "$TMP_DIR/qs_page.json" \
  --data-urlencode "status=Pending" \
  --data-urlencode "page=1" --data-urlencode "pageSize=5")
SC=$(json_get "$TMP_DIR/qs_page.json" statusCode)
assert_eq "quick-send list status" "200" "$SC"

echo "===== SUMMARY ====="
echo "PASS=$PASS FAIL=$FAIL"
if [[ "$FAIL" -gt 0 ]]; then
  exit 1
fi
echo "CRAWL_OK"
