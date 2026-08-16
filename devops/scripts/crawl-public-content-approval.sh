#!/usr/bin/env bash
# Crawl تست تأیید انتشار عمومی (کارت/فرم/گردونه/رزرو)
# Usage: BASE_URL=http://127.0.0.1:5054 bash devops/scripts/crawl-public-content-approval.sh
set -euo pipefail

BASE_URL="${BASE_URL:-http://127.0.0.1:5054}"
TMP_DIR="$(mktemp -d)"
PASS=0
FAIL=0
CARD_ID=""
FORM_ID=""
WHEEL_ID=""
SLUG_CARD=""
SLUG_FORM=""
SLUG_WHEEL=""

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

req() {
  local method="$1" path="$2" out="$3"
  shift 3
  curl -s -w "%{http_code}" -o "$out" -X "$method" "${BASE_URL}${path}" \
    -H "Content-Type: application/json" \
    -H "Accept: application/json" \
    "$@" || true
}

echo "=== Public Content Approval Crawl @ $BASE_URL ==="

TS=$(date +%s)
SLUG_CARD="pub-card-$TS"
SLUG_FORM="pub-form-$TS"
SLUG_WHEEL="pub-wheel-$TS"

# 1) BusinessCard publish -> Pending, public blocked
HTTP=$(req POST "/api/BusinessCard" "$TMP_DIR/card.json" \
  -d '{"templateKey":"business","title":"کارت تست انتشار","descriptionEnabled":true,"descriptionText":"تست","contactEnabled":true,"contactPhone":"09121234567"}')
CARD_ID=$(json_get "$TMP_DIR/card.json" data.id)
if [[ -z "$CARD_ID" ]]; then
  req POST "/api/BusinessCard/draft" "$TMP_DIR/card.json" \
    -d '{"templateKey":"business","title":"کارت تست انتشار","descriptionEnabled":true,"descriptionText":"تست","contactEnabled":true,"contactPhone":"09121234567"}' >/dev/null
  CARD_ID=$(json_get "$TMP_DIR/card.json" data.id)
fi
echo "CARD_ID=$CARD_ID"
assert_eq "card created" "true" "$([[ -n "$CARD_ID" ]] && echo true || echo false)"

req POST "/api/BusinessCard/${CARD_ID}/publish" "$TMP_DIR/pub_card.json" -d "{\"slug\":\"$SLUG_CARD\"}" >/dev/null
AS=$(json_get "$TMP_DIR/pub_card.json" data.approvalStatus)
assert_eq "published card Pending" "Pending" "$AS"

HTTP=$(curl -s -w "%{http_code}" -o "$TMP_DIR/pub_get_card.json" \
  "${BASE_URL}/api/BusinessCardPublic/${SLUG_CARD}")
BODY=$(cat "$TMP_DIR/pub_get_card.json")
SC=$(json_get "$TMP_DIR/pub_get_card.json" statusCode)
EC=$(json_get "$TMP_DIR/pub_get_card.json" errorCode)
MSG=$(json_get "$TMP_DIR/pub_get_card.json" message)
echo "public card pending: http=$HTTP sc=$SC ec=$EC msg=$MSG"
assert_eq "public card pending statusCode" "403" "$SC"
assert_eq "public card pending errorCode" "CONTENT_PENDING_APPROVAL" "$EC"
assert_contains "public card pending message" "منتشر نشده" "$MSG"

# 2) UserForm publish -> Pending, public blocked
req POST "/api/UserForm/draft" "$TMP_DIR/form_draft.json" \
  -d '{"title":"فرم تست انتشار","templateKey":"default","fields":[{"fieldKey":"full_name","fieldType":"text","label":"نام","isRequired":true,"displayOrder":0},{"fieldKey":"mobile","fieldType":"mobile","label":"موبایل","isRequired":true,"displayOrder":1}]}' >/dev/null
FORM_ID=$(json_get "$TMP_DIR/form_draft.json" data.id)
echo "FORM_ID=$FORM_ID"
req POST "/api/UserForm/${FORM_ID}/publish" "$TMP_DIR/pub_form.json" -d "{\"slug\":\"$SLUG_FORM\"}" >/dev/null
AS=$(json_get "$TMP_DIR/pub_form.json" data.approvalStatus)
assert_eq "published form Pending" "Pending" "$AS"

curl -s -o "$TMP_DIR/pub_get_form.json" "${BASE_URL}/api/FormPublic/${SLUG_FORM}" >/dev/null
SC=$(json_get "$TMP_DIR/pub_get_form.json" statusCode)
EC=$(json_get "$TMP_DIR/pub_get_form.json" errorCode)
MSG=$(json_get "$TMP_DIR/pub_get_form.json" message)
assert_eq "public form pending statusCode" "403" "$SC"
assert_eq "public form pending errorCode" "CONTENT_PENDING_APPROVAL" "$EC"
assert_contains "public form pending message" "منتشر نشده" "$MSG"

# 3) LuckyWheel publish -> Pending, public blocked
req POST "/api/LuckyWheel/draft" "$TMP_DIR/wheel_draft.json" \
  -d '{"title":"گردونه تست انتشار","templateKey":"default"}' >/dev/null
WHEEL_ID=$(json_get "$TMP_DIR/wheel_draft.json" data.id)
echo "WHEEL_ID=$WHEEL_ID"
req POST "/api/LuckyWheel/${WHEEL_ID}/items" "$TMP_DIR/wheel_items.json" \
  -d '{"items":[{"title":"جایزه ۱","weight":1,"displayOrder":0},{"title":"جایزه ۲","weight":1,"displayOrder":1}]}' >/dev/null
req POST "/api/LuckyWheel/${WHEEL_ID}/publish" "$TMP_DIR/pub_wheel.json" -d "{\"slug\":\"$SLUG_WHEEL\"}" >/dev/null
AS=$(json_get "$TMP_DIR/pub_wheel.json" data.approvalStatus)
assert_eq "published wheel Pending" "Pending" "$AS"

curl -s -o "$TMP_DIR/pub_get_wheel.json" "${BASE_URL}/api/LuckyWheelPublic/${SLUG_WHEEL}" >/dev/null
SC=$(json_get "$TMP_DIR/pub_get_wheel.json" statusCode)
EC=$(json_get "$TMP_DIR/pub_get_wheel.json" errorCode)
MSG=$(json_get "$TMP_DIR/pub_get_wheel.json" message)
assert_eq "public wheel pending statusCode" "403" "$SC"
assert_eq "public wheel pending errorCode" "CONTENT_PENDING_APPROVAL" "$EC"
assert_contains "public wheel pending message" "منتشر نشده" "$MSG"

# 4) Admin list includes pending items
req GET "/api/Admin/QuickSendApproval?status=Pending&itemType=BusinessCard" "$TMP_DIR/admin_bc.json" >/dev/null
FOUND_BC=$(python3 - "$TMP_DIR/admin_bc.json" "$CARD_ID" <<'PY'
import json,sys
d=json.load(open(sys.argv[1],encoding='utf-8'))
cid=int(sys.argv[2])
items=((d.get('data') or {}).get('items') or [])
print('yes' if any(i.get('id')==cid for i in items) else 'no')
PY
)
assert_eq "admin list contains pending card" "yes" "$FOUND_BC"

req GET "/api/Admin/QuickSendApproval?status=Pending&itemType=UserForm" "$TMP_DIR/admin_form.json" >/dev/null
FOUND_FORM=$(python3 - "$TMP_DIR/admin_form.json" "$FORM_ID" <<'PY'
import json,sys
d=json.load(open(sys.argv[1],encoding='utf-8'))
fid=int(sys.argv[2])
items=((d.get('data') or {}).get('items') or [])
print('yes' if any(i.get('id')==fid for i in items) else 'no')
PY
)
assert_eq "admin list contains pending form" "yes" "$FOUND_FORM"

# 5) Approve all three -> public accessible
req POST "/api/Admin/QuickSendApproval/BusinessCard/${CARD_ID}/approve" "$TMP_DIR/ap_bc.json" >/dev/null
assert_eq "approve card" "200" "$(json_get "$TMP_DIR/ap_bc.json" statusCode)"

req POST "/api/Admin/QuickSendApproval/UserForm/${FORM_ID}/approve" "$TMP_DIR/ap_form.json" >/dev/null
assert_eq "approve form" "200" "$(json_get "$TMP_DIR/ap_form.json" statusCode)"

req POST "/api/Admin/QuickSendApproval/LuckyWheel/${WHEEL_ID}/approve" "$TMP_DIR/ap_wheel.json" >/dev/null
assert_eq "approve wheel" "200" "$(json_get "$TMP_DIR/ap_wheel.json" statusCode)"

curl -s -o "$TMP_DIR/pub_card_ok.json" "${BASE_URL}/api/BusinessCardPublic/${SLUG_CARD}" >/dev/null
assert_eq "public card after approve" "200" "$(json_get "$TMP_DIR/pub_card_ok.json" statusCode)"
assert_eq "public card after approve success" "true" "$(json_get "$TMP_DIR/pub_card_ok.json" success)"

curl -s -o "$TMP_DIR/pub_form_ok.json" "${BASE_URL}/api/FormPublic/${SLUG_FORM}" >/dev/null
assert_eq "public form after approve" "200" "$(json_get "$TMP_DIR/pub_form_ok.json" statusCode)"
assert_eq "public form after approve success" "true" "$(json_get "$TMP_DIR/pub_form_ok.json" success)"

curl -s -o "$TMP_DIR/pub_wheel_ok.json" "${BASE_URL}/api/LuckyWheelPublic/${SLUG_WHEEL}" >/dev/null
assert_eq "public wheel after approve" "200" "$(json_get "$TMP_DIR/pub_wheel_ok.json" statusCode)"
assert_eq "public wheel after approve success" "true" "$(json_get "$TMP_DIR/pub_wheel_ok.json" success)"

# 6) Reject flow on card edit resets pending
req POST "/api/BusinessCard/${CARD_ID}/update-info" "$TMP_DIR/bc_upd.json" \
  -d '{"title":"کارت ویرایش‌شده"}' >/dev/null
if [[ "$(json_get "$TMP_DIR/bc_upd.json" success)" != "true" ]]; then
  req POST "/api/BusinessCard/${CARD_ID}/info" "$TMP_DIR/bc_upd.json" \
    -d '{"title":"کارت ویرایش‌شده"}' >/dev/null
fi
AS=$(json_get "$TMP_DIR/bc_upd.json" data.approvalStatus)
if [[ "$AS" != "Pending" ]]; then
  req GET "/api/BusinessCard/${CARD_ID}" "$TMP_DIR/bc_get.json" >/dev/null
  AS=$(json_get "$TMP_DIR/bc_get.json" data.approvalStatus)
fi
assert_eq "edit resets card to Pending" "Pending" "$AS"

curl -s -o "$TMP_DIR/pub_card_reblock.json" "${BASE_URL}/api/BusinessCardPublic/${SLUG_CARD}" >/dev/null
assert_eq "public card blocked after edit" "403" "$(json_get "$TMP_DIR/pub_card_reblock.json" statusCode)"

req POST "/api/Admin/QuickSendApproval/BusinessCard/${CARD_ID}/reject" "$TMP_DIR/rej_bc.json" \
  -d '{"reason":"محتوای نامناسب برای تست"}' >/dev/null
assert_eq "reject card" "200" "$(json_get "$TMP_DIR/rej_bc.json" statusCode)"

curl -s -o "$TMP_DIR/pub_card_rej.json" "${BASE_URL}/api/BusinessCardPublic/${SLUG_CARD}" >/dev/null
SC=$(json_get "$TMP_DIR/pub_card_rej.json" statusCode)
MSG=$(json_get "$TMP_DIR/pub_card_rej.json" message)
assert_eq "public card rejected statusCode" "403" "$SC"
assert_eq "public card rejected errorCode" "CONTENT_REJECTED" "$(json_get "$TMP_DIR/pub_card_rej.json" errorCode)"
assert_contains "public card rejected message" "در دسترس عمومی نیست" "$MSG"

# 7) Admin filters default all (no status param returns items)
req GET "/api/Admin/QuickSendApproval?page=1&pageSize=5" "$TMP_DIR/admin_all.json" >/dev/null
TC=$(json_get "$TMP_DIR/admin_all.json" data.totalCount)
assert_eq "admin all list has items" "true" "$([[ -n "$TC" && "$TC" != "0" ]] && echo true || echo false)"

# 8) Invalid slug public -> 404
curl -s -o "$TMP_DIR/nf.json" "${BASE_URL}/api/BusinessCardPublic/does-not-exist-$TS" >/dev/null
assert_eq "invalid slug 404" "404" "$(json_get "$TMP_DIR/nf.json" statusCode)"

# 9) Dashboard pendingQuickSendApprovals field
req GET "/api/Admin/Dashboard/stats" "$TMP_DIR/stats.json" >/dev/null
PQ=$(json_get "$TMP_DIR/stats.json" data.pendingQuickSendApprovals)
echo "pendingQuickSendApprovals=$PQ"
assert_eq "dashboard pendingQuickSend field" "true" "$([[ -n "$PQ" ]] && echo true || echo false)"

echo ""
echo "=== RESULT: PASS=$PASS FAIL=$FAIL ==="
if [[ "$FAIL" -gt 0 ]]; then
  exit 1
fi
exit 0
