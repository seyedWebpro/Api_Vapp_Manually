#!/usr/bin/env bash
# E2E شبیه‌سازی صفحه پیش‌نمایش پنل ادمین — همان ۳ درخواست API که QuickSendApprovalPreviewPage می‌زند
# Usage: BASE_URL=http://127.0.0.1:5054 bash devops/scripts/crawl-admin-preview-page-e2e.sh
set -euo pipefail

BASE_URL="${BASE_URL:-http://127.0.0.1:5054}"
TMP_DIR="$(mktemp -d)"
PASS=0
FAIL=0

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
    echo "PASS: $name"
    PASS=$((PASS+1))
  else
    echo "FAIL: $name expected=$expected got=$actual"
    FAIL=$((FAIL+1))
  fi
}

assert_true() {
  local name="$1" cond="$2"
  assert_eq "$name" "true" "$cond"
}

echo "=== Admin Preview Page E2E (API flow) @ $BASE_URL ==="

# مرحله ۰: لیست تأیید محتوا — مثل کلیک اپراتور روی «تأیید محتوا»
curl -s -o "$TMP_DIR/list.json" "${BASE_URL}/api/Admin/QuickSendApproval?status=Pending&itemType=UserForm&page=1&pageSize=5"
assert_eq "approval list status" "200" "$(json_get "$TMP_DIR/list.json" statusCode)"

python3 - "$TMP_DIR/list.json" "$TMP_DIR/target.json" <<'PY'
import json,sys
data=json.load(open(sys.argv[1],encoding='utf-8'))
items=((data.get('data') or {}).get('items') or [])
target=next((i for i in items if i.get('itemType')=='UserForm'), None)
json.dump(target or {}, open(sys.argv[2],'w',encoding='utf-8'))
PY

ITEM_ID=$(json_get "$TMP_DIR/target.json" id)
ITEM_TYPE=$(json_get "$TMP_DIR/target.json" itemType)
ITEM_TITLE=$(json_get "$TMP_DIR/target.json" title)

if [[ -z "$ITEM_ID" ]]; then
  echo "INFO: no pending UserForm in list — creating one for E2E"
  TS=$(date +%s)
  curl -s -o "$TMP_DIR/create.json" -X POST "${BASE_URL}/api/UserForm" \
    -H "Content-Type: application/json" \
    -d "{\"title\":\"E2E فرم $TS\",\"templateKey\":\"default\",\"fields\":[{\"fieldKey\":\"q1\",\"fieldType\":\"text\",\"label\":\"سوال تست\",\"isRequired\":true,\"displayOrder\":0}]}"
  ITEM_ID=$(json_get "$TMP_DIR/create.json" data.id)
  curl -s -o "$TMP_DIR/pub.json" -X POST "${BASE_URL}/api/UserForm/${ITEM_ID}/publish" \
    -H "Content-Type: application/json" \
    -d "{\"slug\":\"e2e-form-$TS\"}"
  ITEM_TYPE="UserForm"
  ITEM_TITLE=$(json_get "$TMP_DIR/pub.json" data.title)
fi

echo "TARGET: $ITEM_TYPE #$ITEM_ID — $ITEM_TITLE"
assert_true "target form selected" "$([[ -n "$ITEM_ID" && "$ITEM_TYPE" == "UserForm" ]] && echo true || echo false)"

# مرحله ۱: باز کردن صفحه /admin/quick-send-approvals/UserForm/{id}/preview
# → getById + createPreviewToken (موازی)
curl -s -o "$TMP_DIR/item.json" "${BASE_URL}/api/Admin/QuickSendApproval/${ITEM_TYPE}/${ITEM_ID}"
curl -s -o "$TMP_DIR/token.json" -X POST "${BASE_URL}/api/Admin/QuickSendApproval/${ITEM_TYPE}/${ITEM_ID}/preview-token"

assert_eq "step1 getById" "200" "$(json_get "$TMP_DIR/item.json" statusCode)"
assert_eq "step1 preview-token" "200" "$(json_get "$TMP_DIR/token.json" statusCode)"

TOKEN=$(json_get "$TMP_DIR/token.json" data.token)
assert_true "step1 token issued" "$([[ -n "$TOKEN" ]] && echo true || echo false)"

# مرحله ۲: fetchQuickSendPreview(token) — همان کاری که Admin_Vapp الان انجام می‌دهد
curl -s -o "$TMP_DIR/preview.json" "${BASE_URL}/api/Public/QuickSendPreview/${TOKEN}"
assert_eq "step2 preview content" "200" "$(json_get "$TMP_DIR/preview.json" statusCode)"

PREVIEW_TITLE=$(json_get "$TMP_DIR/preview.json" data.form.title)
FIELD_LABEL=$(python3 - "$TMP_DIR/preview.json" <<'PY'
import json,sys
d=json.load(open(sys.argv[1],encoding='utf-8'))
fields=((d.get('data') or {}).get('form') or {}).get('fields') or []
print(fields[0].get('label','') if fields else '')
PY
)
IS_ADMIN=$(json_get "$TMP_DIR/preview.json" data.isAdminPreview)

assert_eq "step2 preview title matches item" "$ITEM_TITLE" "$PREVIEW_TITLE"
assert_true "step2 has at least one field" "$([[ -n "$FIELD_LABEL" ]] && echo true || echo false)"
assert_eq "step2 isAdminPreview flag" "true" "$IS_ADMIN"

# مرحله ۳: UI باید فرم را نشان دهد نه داشبورد — یعنی داده form پر باشد
HAS_FORM=$(python3 - "$TMP_DIR/preview.json" <<'PY'
import json,sys
d=json.load(open(sys.argv[1],encoding='utf-8'))
form=(d.get('data') or {}).get('form')
print('true' if form and form.get('fields') else 'false')
PY
)
assert_eq "step3 inline preview payload ready" "true" "$HAS_FORM"

# مرحله ۴: لینک عمومی هنوز بسته (Pending) — کاربر عادی نمی‌بیند
APPROVAL=$(json_get "$TMP_DIR/item.json" data.approvalStatus)
if [[ "$APPROVAL" == "Pending" ]]; then
  SLUG=$(json_get "$TMP_DIR/item.json" data.slug)
  if [[ -n "$SLUG" ]]; then
    curl -s -o "$TMP_DIR/public.json" "${BASE_URL}/api/FormPublic/${SLUG}"
    assert_eq "step4 public still blocked" "403" "$(json_get "$TMP_DIR/public.json" statusCode)"
  fi
fi

echo ""
echo "=== Admin page would render: title='$PREVIEW_TITLE', firstField='$FIELD_LABEL' ==="
echo "=== RESULT: PASS=$PASS FAIL=$FAIL ==="
[[ "$FAIL" -eq 0 ]]
