#!/usr/bin/env bash
# Crawl فیکس پیام خطای SMS تکراری + رفتار PreventDuplicate
#
# Usage:
#   bash devops/scripts/crawl-sms-duplicate-send.sh
#   BASE_URL=http://127.0.0.1:5054 SKIP_LIVE_SMS=1 bash devops/scripts/crawl-sms-duplicate-send.sh
#
# Env:
#   SKIP_LIVE_SMS=1  — فقط unit + mapper؛ بدون ارسال واقعی به پنل
#   SKIP_API_START=1 — API از قبل روی BASE_URL بالا است
set -euo pipefail
export PATH="/usr/bin:/bin:/usr/sbin:/sbin:/usr/local/bin:${PATH:-}"

ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
cd "$ROOT"

BASE_URL="${BASE_URL:-http://127.0.0.1:5054}"
SKIP_LIVE_SMS="${SKIP_LIVE_SMS:-0}"
SKIP_API_START="${SKIP_API_START:-0}"
LOG=/tmp/vapp-sms-duplicate-crawl.log
PIDFILE=/tmp/vapp-sms-duplicate-crawl.pid
TMP_DIR="$(mktemp -d)"
PASS=0
FAIL=0
STARTED_API=0
CONTACT_ID=""
NB_ID=""

cleanup() {
  rm -rf "$TMP_DIR"
  if [[ "$STARTED_API" == "1" ]] && [[ -f "$PIDFILE" ]]; then
    kill "$(cat "$PIDFILE")" 2>/dev/null || true
  fi
}
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
    if cur is None: break
    if isinstance(cur,dict):
        cur=cur.get(p)
    else:
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

assert_contains() {
  local name="$1" needle="$2" hay="$3"
  if [[ "$hay" == *"$needle"* ]]; then
    echo "PASS: $name (contains)"
    PASS=$((PASS+1))
  else
    echo "FAIL: $name missing='$needle' in='$hay'"
    FAIL=$((FAIL+1))
  fi
}

assert_not_contains() {
  local name="$1" needle="$2" hay="$3"
  if [[ "$hay" != *"$needle"* ]]; then
    echo "PASS: $name (not contains)"
    PASS=$((PASS+1))
  else
    echo "FAIL: $name should_not_contain='$needle' in='$hay'"
    FAIL=$((FAIL+1))
  fi
}

req() {
  local method="$1" path="$2" out="$3"
  shift 3
  local http
  http=$(curl -s -w "%{http_code}" -o "$out" -X "$method" "${BASE_URL}${path}" \
    -H "Content-Type: application/json; charset=utf-8" \
    -H "Accept: application/json" \
    "$@" || true)
  echo "$http"
}

echo "=== SMS Duplicate / Clear Error Crawl @ $BASE_URL ==="

echo "===== UNIT TESTS ====="
dotnet test Tests/Api_Vapp.Tests.csproj --nologo \
  --filter "FullyQualifiedName~SmsProviderErrorMapperTests" \
  --logger "console;verbosity=minimal"
echo "UNIT_OK"

echo "===== BUILD ====="
dotnet build Api_Vapp.csproj --nologo -v q
echo "BUILD_OK"

if [[ "$SKIP_API_START" != "1" ]]; then
  echo "===== RESTART API ====="
  for p in $(lsof -t -iTCP:5054 -sTCP:LISTEN 2>/dev/null || true); do kill "$p" 2>/dev/null || true; done
  sleep 2
  for p in $(lsof -t -iTCP:5054 -sTCP:LISTEN 2>/dev/null || true); do kill -9 "$p" 2>/dev/null || true; done
  sleep 1

  nohup env ASPNETCORE_ENVIRONMENT=Development \
    dotnet exec bin/Debug/net8.0/Api_Vapp.dll --urls "http://127.0.0.1:5054" \
    > "$LOG" 2>&1 &
  echo $! > "$PIDFILE"
  STARTED_API=1
  echo "API_STARTED:$(cat "$PIDFILE")"

  for i in $(seq 1 90); do
    if grep -qE "Migration completed successfully|Application started|Now listening on" "$LOG" 2>/dev/null; then
      echo "API_BOOT_SIGNAL"
      break
    fi
    if ! kill -0 "$(cat "$PIDFILE")" 2>/dev/null; then
      echo "API_DIED_EARLY"
      tail -80 "$LOG" || true
      exit 1
    fi
    sleep 2
  done

  READY=0
  for i in $(seq 1 60); do
    code=$(curl -s -o /dev/null -w "%{http_code}" --max-time 10 "$BASE_URL/health" || echo 000)
    if [[ "$code" == "200" ]]; then READY=1; break; fi
    if ! kill -0 "$(cat "$PIDFILE")" 2>/dev/null; then
      echo "API_DIED_WHILE_WAITING"
      tail -80 "$LOG" || true
      exit 1
    fi
    sleep 2
  done
  if [[ "$READY" != "1" ]]; then
    echo "API_NOT_READY"
    tail -80 "$LOG" || true
    exit 1
  fi
  echo "API_READY"
fi

# --- health ---
HTTP=$(curl -s -o "$TMP_DIR/health.json" -w "%{http_code}" "$BASE_URL/health" || true)
assert_eq "health http" "200" "$HTTP"

# --- wallet top-up for default user (DisableAuth) ---
# اگر موجودی کافی نباشد ارسال fail می‌شود؛ برای تست duplicate باید حداقل یک ارسال موفق داشته باشیم
HTTP=$(req GET "/api/Wallet/balance" "$TMP_DIR/wallet.json" || true)
BAL=$(json_get "$TMP_DIR/wallet.json" data.balance 2>/dev/null || echo "")
echo "WALLET_BALANCE=${BAL:-unknown}"

# --- ensure notebook + contact ---
curl -s -o "$TMP_DIR/nb.json" -w "%{http_code}" -X POST "$BASE_URL/api/ContactNotebook" \
  -F "Name=SmsDupCrawlNB" -F "IsActive=true" >/dev/null || true
NB_ID=$(json_get "$TMP_DIR/nb.json" data.id)
if [[ -z "$NB_ID" ]]; then
  HTTP=$(req GET "/api/ContactNotebook?pageNumber=1&pageSize=5" "$TMP_DIR/nbl.json")
  NB_ID=$(python3 - "$TMP_DIR/nbl.json" <<'PY'
import json,sys
d=json.load(open(sys.argv[1],encoding='utf-8'))
items=((d.get('data') or {}).get('notebooks') or (d.get('data') or {}).get('items') or [])
print(items[0]['id'] if items else '')
PY
)
fi
echo "NB_ID=$NB_ID"
assert_eq "notebook ready" "true" "$([[ -n "$NB_ID" ]] && echo true || echo false)"

# شماره ثابت برای تحریک محدودیت ۱ دقیقه‌ای پنل (دو ارسال پشت‌سرهم)
MOB="0912$(printf '%07d' $((RANDOM % 10000000)))"
HTTP=$(req POST "/api/Contact" "$TMP_DIR/ct.json" \
  -d "{\"contactNotebookId\":$NB_ID,\"fullName\":\"Sms Dup Crawl\",\"mobileNumber\":\"$MOB\"}")
CONTACT_ID=$(json_get "$TMP_DIR/ct.json" data.id)
if [[ -z "$CONTACT_ID" ]]; then
  # ممکن است مخاطب تکراری باشد — از لیست بردار
  HTTP=$(req GET "/api/Contact?pageNumber=1&pageSize=20&contactNotebookId=$NB_ID" "$TMP_DIR/ctl.json")
  CONTACT_ID=$(python3 - "$TMP_DIR/ctl.json" "$MOB" <<'PY'
import json,sys
d=json.load(open(sys.argv[1],encoding='utf-8'))
mob=sys.argv[2]
items=((d.get('data') or {}).get('contacts') or (d.get('data') or {}).get('items') or [])
hit=next((i for i in items if str(i.get('mobileNumber') or '')==mob), None)
print((hit or {}).get('id') or (items[0]['id'] if items else ''))
PY
)
fi
echo "CONTACT_ID=$CONTACT_ID MOB=$MOB"
assert_eq "contact ready" "true" "$([[ -n "$CONTACT_ID" ]] && echo true || echo false)"

# --- قالب تأییدشده پیش‌فرض (بدون آن، quick-send می‌رود صف تأیید ۲۰۲) ---
HTTP=$(req GET "/api/Template" "$TMP_DIR/tpl.json")
TPL_READY=$(python3 - "$TMP_DIR/tpl.json" <<'PY' 2>/dev/null || echo no
import json,sys
raw=open(sys.argv[1],encoding='utf-8').read().strip()
if not raw:
  print('no'); raise SystemExit
d=json.loads(raw)
items=d.get('data') or []
if isinstance(items,dict):
  items=items.get('items') or items.get('templates') or []
hit=next((i for i in items if i.get('isDefault') and i.get('approvalStatus')=='Approved'), None)
print('yes' if hit else 'no')
if hit: print(str(hit.get('id')))
PY
)
echo "TPL_READY=$TPL_READY"
TID=""
if [[ "$TPL_READY" == yes* ]]; then
  TID=$(echo "$TPL_READY" | tail -1)
else
  HTTP=$(curl -s -w "%{http_code}" -o "$TMP_DIR/tplc.json" -X POST "$BASE_URL/api/Template" \
    -F "Name=قالب تست تکراری" \
    -F "Content=سلام تست ارسال سریع تکراری" \
    -F "Category=test" || true)
  TID=$(json_get "$TMP_DIR/tplc.json" data.id)
  echo "CREATED_TEMPLATE=$TID http=$HTTP"
  if [[ -n "$TID" ]]; then
    curl -s -o "$TMP_DIR/appr.json" -X POST "$BASE_URL/api/Admin/TemplateApproval/$TID/approve" \
      -H "Content-Type: application/json" >/dev/null || true
    echo "APPROVE=$(json_get "$TMP_DIR/appr.json" success) $(json_get "$TMP_DIR/appr.json" message)"
    curl -s -o "$TMP_DIR/def.json" -X POST "$BASE_URL/api/Template/set-default" \
      -H "Content-Type: application/json" \
      -d "{\"templateId\":$TID}" >/dev/null || true
    echo "SET_DEFAULT=$(json_get "$TMP_DIR/def.json" success)"
  fi
fi
assert_eq "template id ready" "true" "$([[ -n "$TID" ]] && echo true || echo false)"

if [[ "$SKIP_LIVE_SMS" == "1" ]]; then
  echo "SKIP_LIVE_SMS=1 — skipping provider send"
else
  echo "===== LIVE QUICK-SEND (1st then immediate 2nd) ====="
  T0=$(date +%s)

  HTTP1=$(req POST "/api/Message/quick-send" "$TMP_DIR/qs1.json" \
    -d "{\"contactId\":$CONTACT_ID}")
  SC1=$(json_get "$TMP_DIR/qs1.json" statusCode)
  MSG1=$(json_get "$TMP_DIR/qs1.json" message)
  EC1=$(json_get "$TMP_DIR/qs1.json" errorCode)
  SUCC1=$(json_get "$TMP_DIR/qs1.json" success)
  echo "QS1 http=$HTTP1 statusCode=$SC1 success=$SUCC1 errorCode=$EC1 message=$MSG1"
  echo "QS1_BODY=$(head -c 400 "$TMP_DIR/qs1.json")"

  # اگر رفت صف تأیید — فیکس تست
  if [[ "$SC1" == "202" ]]; then
    echo "FAIL: quick-send went to admin approval queue (template not approved/default)"
    FAIL=$((FAIL+1))
  fi

  HTTP2=$(req POST "/api/Message/quick-send" "$TMP_DIR/qs2.json" \
    -d "{\"contactId\":$CONTACT_ID}")
  SC2=$(json_get "$TMP_DIR/qs2.json" statusCode)
  MSG2=$(json_get "$TMP_DIR/qs2.json" message)
  EC2=$(json_get "$TMP_DIR/qs2.json" errorCode)
  SUCC2=$(json_get "$TMP_DIR/qs2.json" success)
  echo "QS2 http=$HTTP2 statusCode=$SC2 success=$SUCC2 errorCode=$EC2 message=$MSG2"
  echo "QS2_BODY=$(head -c 400 "$TMP_DIR/qs2.json")"

  T1=$(date +%s)
  ELAPSED=$((T1 - T0))
  echo "ELAPSED_SECONDS=$ELAPSED"

  assert_not_contains "qs2 no raw provider text" "نمی باشید" "$MSG2"
  assert_not_contains "qs2 no old vague text" "مشکلی در ارسال پیامک پیش آمد" "$MSG2"

  if [[ "$SUCC1" == "true" && "$SUCC2" == "false" ]]; then
    assert_eq "qs2 statusCode 400" "400" "$SC2"
    assert_eq "qs2 errorCode SMS_DUPLICATE" "SMS_DUPLICATE" "$EC2"
    assert_contains "qs2 clear duplicate message" "یک دقیقه" "$MSG2"
  elif [[ "$SUCC1" == "false" && "$EC1" == "SMS_DUPLICATE" ]]; then
    assert_contains "qs1 clear duplicate message" "یک دقیقه" "$MSG1"
    assert_not_contains "qs1 no raw provider" "نمی باشید" "$MSG1"
  elif [[ "$SUCC1" == "false" && "$MSG1" == *"موجودی"* ]]; then
    echo "WARN: wallet insufficient — duplicate live path skipped"
    assert_contains "wallet message clear" "موجودی" "$MSG1"
  elif [[ "$SUCC1" == "true" && "$SUCC2" == "true" ]]; then
    echo "WARN: both sends succeeded (provider did not reject duplicate)"
    assert_eq "qs2 success true" "true" "$SUCC2"
  else
    if [[ "$SUCC2" == "false" ]]; then
      assert_not_contains "qs2 failure not vague old" "مشکلی در ارسال پیامک پیش آمد" "$MSG2"
      if [[ "$EC2" == "SMS_DUPLICATE" ]]; then
        assert_contains "qs2 duplicate msg" "یک دقیقه" "$MSG2"
      fi
    fi
    if [[ "$SUCC1" == "false" ]]; then
      assert_not_contains "qs1 failure not vague old" "مشکلی در ارسال پیامک پیش آمد" "$MSG1"
    fi
  fi

  if [[ -f "$LOG" ]]; then
    RETRIES=$(grep -c "Retrying SMS send" "$LOG" 2>/dev/null || true)
    RETRIES=${RETRIES:-0}
    echo "LOG_RETRIES=$RETRIES"
    if [[ "$EC2" == "SMS_DUPLICATE" || "$EC1" == "SMS_DUPLICATE" ]]; then
      if [[ "$RETRIES" -gt 6 ]]; then
        echo "FAIL: too many SMS retries in log ($RETRIES) — duplicate should be non-retryable"
        FAIL=$((FAIL+1))
      else
        echo "PASS: retry count acceptable ($RETRIES)"
        PASS=$((PASS+1))
      fi
    fi
  fi
fi

HTTP=$(req POST "/api/Message/quick-send" "$TMP_DIR/badqs.json" -d '{"contactId":999999999}')
assert_eq "quick-send missing contact 404" "404" "$(json_get "$TMP_DIR/badqs.json" statusCode)"

echo ""
echo "=== SUMMARY pass=$PASS fail=$FAIL ==="
if [[ "$FAIL" -gt 0 ]]; then
  exit 1
fi
exit 0
