#!/usr/bin/env bash
# Crawl عمیق BankAccount + تأیید ادمین ارسال سریع
# Usage: BASE_URL=http://127.0.0.1:5054 bash devops/scripts/crawl-bank-account-quick-send.sh
set -euo pipefail

BASE_URL="${BASE_URL:-http://127.0.0.1:5054}"
TMP_DIR="$(mktemp -d)"
PASS=0
FAIL=0
BA_ID=""
BA2_ID=""
CONTACT_ID=""

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

echo "=== BankAccount QuickSend Deep Crawl @ $BASE_URL ==="

# --- health ---
HTTP=$(curl -s -o "$TMP_DIR/health.json" -w "%{http_code}" "$BASE_URL/health" || true)
assert_eq "health http" "200" "$HTTP"

# --- auth ---
HTTP=$(curl -s -w "%{http_code}" -o "$TMP_DIR/badtok.json" \
  -H "Authorization: Bearer not-a-token" \
  "$BASE_URL/api/BankAccount")
assert_eq "invalid token statusCode" "401" "$(json_get "$TMP_DIR/badtok.json" statusCode)"

# --- validation: empty body ---
HTTP=$(req POST "/api/BankAccount" "$TMP_DIR/empty.json" -d '{}')
assert_eq "empty title status" "400" "$(json_get "$TMP_DIR/empty.json" statusCode)"
assert_eq "empty title errorCode" "VALIDATION_FAILED" "$(json_get "$TMP_DIR/empty.json" errorCode)"

# --- validation: no bank fields ---
HTTP=$(req POST "/api/BankAccount" "$TMP_DIR/nofield.json" -d '{"title":"بدون فیلد"}')
assert_eq "no bank fields status" "400" "$(json_get "$TMP_DIR/nofield.json" statusCode)"
assert_eq "no bank fields errorCode" "INVALID_INPUT" "$(json_get "$TMP_DIR/nofield.json" errorCode)"
assert_contains "no bank fields msg" "حداقل یکی" "$(json_get "$TMP_DIR/nofield.json" message)"

# --- validation: bad card ---
HTTP=$(req POST "/api/BankAccount" "$TMP_DIR/badcard.json" -d '{"title":"بد","cardNumber":"1234"}')
assert_eq "bad card status" "400" "$(json_get "$TMP_DIR/badcard.json" statusCode)"
assert_contains "bad card msg" "۱۶" "$(json_get "$TMP_DIR/badcard.json" message)"

# --- validation: bad sheba ---
HTTP=$(req POST "/api/BankAccount" "$TMP_DIR/badsheba.json" -d '{"title":"بد","shebaNumber":"IR12"}')
assert_eq "bad sheba status" "400" "$(json_get "$TMP_DIR/badsheba.json" statusCode)"
assert_contains "bad sheba msg" "۲۴" "$(json_get "$TMP_DIR/badsheba.json" message)"

# --- create: card only (normalize dashes) ---
HTTP=$(req POST "/api/BankAccount" "$TMP_DIR/create1.json" \
  -d '{"title":"  حساب کارت  ","cardNumber":"6037-9912-3456-7890","isDefault":true}')
assert_eq "create card-only status" "201" "$(json_get "$TMP_DIR/create1.json" statusCode)"
BA_ID=$(json_get "$TMP_DIR/create1.json" data.id)
assert_eq "create Pending" "Pending" "$(json_get "$TMP_DIR/create1.json" data.approvalStatus)"
assert_eq "create default" "true" "$(json_get "$TMP_DIR/create1.json" data.isDefault)"
assert_eq "card normalized" "6037991234567890" "$(json_get "$TMP_DIR/create1.json" data.cardNumber)"
assert_eq "title trimmed" "حساب کارت" "$(json_get "$TMP_DIR/create1.json" data.title)"
echo "BA_ID=$BA_ID"

# --- create: sheba + account ---
HTTP=$(req POST "/api/BankAccount" "$TMP_DIR/create2.json" \
  -d '{"title":"حساب شبا","accountNumber":"123456789012","shebaNumber":"IR12 0170 0000 0012 3456 7890 01"}')
assert_eq "create sheba status" "201" "$(json_get "$TMP_DIR/create2.json" statusCode)"
BA2_ID=$(json_get "$TMP_DIR/create2.json" data.id)
assert_eq "sheba normalized" "IR120170000000123456789001" "$(json_get "$TMP_DIR/create2.json" data.shebaNumber)"
echo "BA2_ID=$BA2_ID"

# --- list ---
HTTP=$(req GET "/api/BankAccount?pageNumber=1&pageSize=10" "$TMP_DIR/list.json")
assert_eq "list status" "200" "$(json_get "$TMP_DIR/list.json" statusCode)"
TC=$(json_get "$TMP_DIR/list.json" data.totalCount)
assert_eq "list has items" "true" "$([[ "${TC:-0}" -ge 2 ]] && echo true || echo false)"

# --- get by id ---
HTTP=$(req GET "/api/BankAccount/${BA_ID}" "$TMP_DIR/get.json")
assert_eq "get status" "200" "$(json_get "$TMP_DIR/get.json" statusCode)"
assert_eq "get id" "$BA_ID" "$(json_get "$TMP_DIR/get.json" data.id)"

# --- get not found ---
HTTP=$(req GET "/api/BankAccount/999999" "$TMP_DIR/nf.json")
assert_eq "get 404" "404" "$(json_get "$TMP_DIR/nf.json" statusCode)"

# --- admin pending filter ---
HTTP=$(req GET "/api/Admin/QuickSendApproval/pending?itemType=BankAccount" "$TMP_DIR/pend.json")
assert_eq "admin pending status" "200" "$(json_get "$TMP_DIR/pend.json" statusCode)"
FOUND=$(python3 - "$TMP_DIR/pend.json" "$BA_ID" <<'PY'
import json,sys
d=json.load(open(sys.argv[1],encoding='utf-8'))
lid=int(sys.argv[2])
items=((d.get('data') or {}).get('items') or [])
hit=next((i for i in items if i.get('id')==lid), None)
print('yes' if hit else 'no')
if hit:
  print('preview='+str(hit.get('contentPreview') or ''))
  print('public='+str(hit.get('publicUrl')))
  print('type='+str(hit.get('itemTypeTitle') or ''))
PY
)
assert_contains "pending contains BA" "yes" "$FOUND"
assert_contains "pending title fa" "شماره حساب" "$FOUND"
assert_contains "pending preview card" "کارت:" "$FOUND"
assert_contains "pending publicUrl null" "public=None" "$FOUND"

# --- contact for quick-send ---
curl -s -o "$TMP_DIR/nb.json" -w "%{http_code}" -X POST "$BASE_URL/api/ContactNotebook" \
  -F "Name=BACrawlNB" -F "IsActive=true" >/dev/null || true
NB_ID=$(json_get "$TMP_DIR/nb.json" data.id)
if [[ -z "$NB_ID" ]]; then
  HTTP=$(req GET "/api/ContactNotebook?pageNumber=1&pageSize=5" "$TMP_DIR/nbl.json")
  NB_ID=$(python3 - "$TMP_DIR/nbl.json" <<'PY'
import json,sys
d=json.load(open(sys.argv[1],encoding='utf-8'))
items=((d.get('data') or {}).get('notebooks') or [])
print(items[0]['id'] if items else '')
PY
)
fi
echo "NB_ID=$NB_ID"
assert_eq "notebook ready" "true" "$([[ -n "$NB_ID" ]] && echo true || echo false)"

MOB="0912$(printf '%07d' $((RANDOM % 10000000)))"
HTTP=$(req POST "/api/Contact" "$TMP_DIR/ct.json" \
  -d "{\"contactNotebookId\":$NB_ID,\"fullName\":\"BA Crawl\",\"mobileNumber\":\"$MOB\"}")
CONTACT_ID=$(json_get "$TMP_DIR/ct.json" data.id)
if [[ -z "$CONTACT_ID" ]]; then
  HTTP=$(req GET "/api/Contact/notebook/${NB_ID}?pageNumber=1&pageSize=1" "$TMP_DIR/cts.json")
  CONTACT_ID=$(python3 - "$TMP_DIR/cts.json" <<'PY'
import json,sys
d=json.load(open(sys.argv[1],encoding='utf-8'))
data=d.get('data') or {}
items=data.get('contacts') or data.get('items') or []
print(items[0]['id'] if items else '')
PY
)
fi
echo "CONTACT_ID=$CONTACT_ID"
assert_eq "contact ready" "true" "$([[ -n "$CONTACT_ID" ]] && echo true || echo false)"

# --- quick-send pending -> 202 ---
HTTP=$(req POST "/api/BankAccount/quick-send" "$TMP_DIR/qs_p.json" \
  -d "{\"contactId\":$CONTACT_ID,\"bankAccountId\":$BA_ID}")
assert_eq "qs pending status" "202" "$(json_get "$TMP_DIR/qs_p.json" statusCode)"
assert_contains "qs pending msg" "صف تأیید" "$(json_get "$TMP_DIR/qs_p.json" message)"
assert_eq "qs pending adminStatus" "Pending" "$(json_get "$TMP_DIR/qs_p.json" data.adminApprovalStatus)"

# --- quick-send invalid ids ---
HTTP=$(req POST "/api/BankAccount/quick-send" "$TMP_DIR/qs_bad.json" -d '{"contactId":0,"bankAccountId":0}')
assert_eq "qs invalid ids" "400" "$(json_get "$TMP_DIR/qs_bad.json" statusCode)"

HTTP=$(req POST "/api/BankAccount/quick-send" "$TMP_DIR/qs_nf.json" \
  -d "{\"contactId\":$CONTACT_ID,\"bankAccountId\":999999}")
assert_eq "qs missing ba" "404" "$(json_get "$TMP_DIR/qs_nf.json" statusCode)"

# --- reject empty reason ---
HTTP=$(req POST "/api/Admin/QuickSendApproval/BankAccount/${BA_ID}/reject" "$TMP_DIR/rej_empty.json" -d '{}')
assert_eq "reject empty reason" "400" "$(json_get "$TMP_DIR/rej_empty.json" statusCode)"

# --- reject ---
HTTP=$(req POST "/api/Admin/QuickSendApproval/BankAccount/${BA_ID}/reject" "$TMP_DIR/rej.json" \
  -d '{"reason":"اطلاعات بانکی ناقص است"}')
assert_eq "reject status" "200" "$(json_get "$TMP_DIR/rej.json" statusCode)"
assert_contains "reject msg no public link" "رد شد" "$(json_get "$TMP_DIR/rej.json" message)"
assert_not_contains "reject msg no لینک عمومی" "لینک عمومی" "$(json_get "$TMP_DIR/rej.json" message)"

# --- qs rejected ---
HTTP=$(req POST "/api/BankAccount/quick-send" "$TMP_DIR/qs_r.json" \
  -d "{\"contactId\":$CONTACT_ID,\"bankAccountId\":$BA_ID}")
assert_eq "qs rejected status" "400" "$(json_get "$TMP_DIR/qs_r.json" statusCode)"
assert_eq "qs rejected errorCode" "CONTENT_REJECTED" "$(json_get "$TMP_DIR/qs_r.json" errorCode)"
assert_contains "qs rejected msg" "تأیید نشد" "$(json_get "$TMP_DIR/qs_r.json" message)"

# --- update content -> Pending again ---
HTTP=$(req POST "/api/BankAccount/${BA_ID}/update" "$TMP_DIR/upd.json" \
  -d '{"title":"حساب کارت اصلاح","cardNumber":"6037991234567891","shebaNumber":"IR120170000000123456789099"}')
assert_eq "update status" "200" "$(json_get "$TMP_DIR/upd.json" statusCode)"
assert_eq "update resets Pending" "Pending" "$(json_get "$TMP_DIR/upd.json" data.approvalStatus)"
assert_eq "update card" "6037991234567891" "$(json_get "$TMP_DIR/upd.json" data.cardNumber)"

# --- update clear all bank fields -> 400 ---
HTTP=$(req POST "/api/BankAccount/${BA_ID}/update" "$TMP_DIR/clr.json" \
  -d '{"accountNumber":"","cardNumber":"","shebaNumber":""}')
assert_eq "clear all fields blocked" "400" "$(json_get "$TMP_DIR/clr.json" statusCode)"

# --- update IsActive alone should NOT reset approval if content same ---
# first approve then toggle isActive
HTTP=$(req POST "/api/Admin/QuickSendApproval/BankAccount/${BA_ID}/approve" "$TMP_DIR/ap.json")
assert_eq "approve status" "200" "$(json_get "$TMP_DIR/ap.json" statusCode)"
assert_contains "approve msg" "تأیید شد" "$(json_get "$TMP_DIR/ap.json" message)"
assert_not_contains "approve msg no منتشر for BA" "منتشر" "$(json_get "$TMP_DIR/ap.json" message)"

# double approve
HTTP=$(req POST "/api/Admin/QuickSendApproval/BankAccount/${BA_ID}/approve" "$TMP_DIR/ap2.json")
assert_eq "double approve" "400" "$(json_get "$TMP_DIR/ap2.json" statusCode)"

# get detail admin
HTTP=$(req GET "/api/Admin/QuickSendApproval/BankAccount/${BA_ID}" "$TMP_DIR/adet.json")
assert_eq "admin get status" "200" "$(json_get "$TMP_DIR/adet.json" statusCode)"
assert_eq "admin get Approved" "Approved" "$(json_get "$TMP_DIR/adet.json" data.approvalStatus)"

# qs approved — must NOT be 202 (wallet/sms may fail)
HTTP=$(req POST "/api/BankAccount/quick-send" "$TMP_DIR/qs_ok.json" \
  -d "{\"contactId\":$CONTACT_ID,\"bankAccountId\":$BA_ID}")
SC=$(json_get "$TMP_DIR/qs_ok.json" statusCode)
MSG=$(json_get "$TMP_DIR/qs_ok.json" message)
echo "approved qs status=$SC msg=$MSG"
if [[ "$SC" == "202" ]]; then
  assert_eq "approved not queued" "200" "202"
else
  assert_eq "approved not queued" "true" "true"
fi

# --- set-default ---
HTTP=$(req POST "/api/BankAccount/${BA2_ID}/set-default" "$TMP_DIR/def.json")
assert_eq "set-default status" "200" "$(json_get "$TMP_DIR/def.json" statusCode)"
assert_eq "set-default true" "true" "$(json_get "$TMP_DIR/def.json" data.isDefault)"
HTTP=$(req GET "/api/BankAccount/${BA_ID}" "$TMP_DIR/get2.json")
assert_eq "old default cleared" "false" "$(json_get "$TMP_DIR/get2.json" data.isDefault)"

# --- cannot deactivate default ---
HTTP=$(req POST "/api/BankAccount/${BA2_ID}/update" "$TMP_DIR/deact.json" -d '{"isActive":false}')
assert_eq "deactivate default blocked" "400" "$(json_get "$TMP_DIR/deact.json" statusCode)"

# --- delete non-default ---
HTTP=$(req POST "/api/BankAccount/${BA_ID}/delete" "$TMP_DIR/del.json")
assert_eq "delete status" "200" "$(json_get "$TMP_DIR/del.json" statusCode)"
HTTP=$(req GET "/api/BankAccount/${BA_ID}" "$TMP_DIR/delget.json")
assert_eq "deleted get 404" "404" "$(json_get "$TMP_DIR/delget.json" statusCode)"

# --- qs deleted ---
HTTP=$(req POST "/api/BankAccount/quick-send" "$TMP_DIR/qs_del.json" \
  -d "{\"contactId\":$CONTACT_ID,\"bankAccountId\":$BA_ID}")
assert_eq "qs deleted 404" "404" "$(json_get "$TMP_DIR/qs_del.json" statusCode)"

# --- admin invalid type ---
HTTP=$(req GET "/api/Admin/QuickSendApproval/pending?itemType=FooBar" "$TMP_DIR/badtype.json")
assert_eq "admin bad type" "400" "$(json_get "$TMP_DIR/badtype.json" statusCode)"

# --- dashboard pending field ---
HTTP=$(req GET "/api/Admin/Dashboard/stats" "$TMP_DIR/stats.json")
PQ=$(json_get "$TMP_DIR/stats.json" data.pendingQuickSendApprovals)
assert_eq "dashboard pendingQuickSend field" "true" "$([[ -n "$PQ" ]] && echo true || echo false)"

# --- pagination edge ---
HTTP=$(req GET "/api/BankAccount?pageNumber=0&pageSize=500" "$TMP_DIR/page.json")
assert_eq "pagination clamp status" "200" "$(json_get "$TMP_DIR/page.json" statusCode)"
PS=$(json_get "$TMP_DIR/page.json" data.pageSize)
assert_eq "pageSize clamped to 10" "10" "$PS"

echo ""
echo "=== RESULT: PASS=$PASS FAIL=$FAIL ==="
if [[ "$FAIL" -gt 0 ]]; then
  exit 1
fi
exit 0
