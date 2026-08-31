#!/usr/bin/env bash
# Crawl تست پیش‌نمایش ادمین (فرم / کارت ویزیت) — همان جریانی که پنل Admin_Vapp اجرا می‌کند
# Usage: BASE_URL=http://127.0.0.1:5054 bash devops/scripts/crawl-quick-send-preview.sh
set -euo pipefail

BASE_URL="${BASE_URL:-http://127.0.0.1:5054}"
TMP_DIR="$(mktemp -d)"
PASS=0
FAIL=0
FORM_ID=""
CARD_ID=""
SLUG_FORM=""
SLUG_CARD=""

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

echo "=== QuickSend Admin Preview Crawl @ $BASE_URL ==="

TS=$(date +%s)
SLUG_FORM="preview-form-$TS"
SLUG_CARD="preview-card-$TS"
FORM_TITLE="فرم پیش‌نمایش تست $TS"
FIELD_LABEL="نام و نام خانوادگی"

# --- 1) UserForm publish -> Pending ---
req POST "/api/UserForm" "$TMP_DIR/form_draft.json" \
  -d "{\"title\":\"$FORM_TITLE\",\"templateKey\":\"default\",\"fields\":[{\"fieldKey\":\"full_name\",\"fieldType\":\"text\",\"label\":\"$FIELD_LABEL\",\"isRequired\":true,\"displayOrder\":0},{\"fieldKey\":\"mobile\",\"fieldType\":\"mobile\",\"label\":\"موبایل\",\"isRequired\":true,\"displayOrder\":1}]}" >/dev/null
FORM_ID=$(json_get "$TMP_DIR/form_draft.json" data.id)
assert_eq "form draft created" "true" "$([[ -n "$FORM_ID" ]] && echo true || echo false)"

req POST "/api/UserForm/${FORM_ID}/publish" "$TMP_DIR/pub_form.json" -d "{\"slug\":\"$SLUG_FORM\"}" >/dev/null
AS=$(json_get "$TMP_DIR/pub_form.json" data.approvalStatus)
assert_eq "published form Pending" "Pending" "$AS"

# --- 2) Public slug blocked while Pending ---
curl -s -o "$TMP_DIR/pub_form_blocked.json" "${BASE_URL}/api/FormPublic/${SLUG_FORM}" >/dev/null
assert_eq "public form blocked while pending" "403" "$(json_get "$TMP_DIR/pub_form_blocked.json" statusCode)"

# --- 3) Admin preview-token for UserForm ---
req POST "/api/Admin/QuickSendApproval/UserForm/${FORM_ID}/preview-token" "$TMP_DIR/form_token.json" >/dev/null
SC=$(json_get "$TMP_DIR/form_token.json" statusCode)
TOKEN=$(json_get "$TMP_DIR/form_token.json" data.token)
PREVIEW_PATH=$(json_get "$TMP_DIR/form_token.json" data.previewPath)
assert_eq "form preview-token status" "200" "$SC"
assert_eq "form preview-token has token" "true" "$([[ -n "$TOKEN" ]] && echo true || echo false)"
assert_contains "form previewPath starts with /preview/" "/preview/" "$PREVIEW_PATH"

# --- 4) Fetch preview content (Admin_Vapp fetchQuickSendPreview) ---
curl -s -o "$TMP_DIR/form_preview.json" "${BASE_URL}/api/Public/QuickSendPreview/${TOKEN}" >/dev/null
SC=$(json_get "$TMP_DIR/form_preview.json" statusCode)
ITEM_TYPE=$(json_get "$TMP_DIR/form_preview.json" data.itemType)
APPROVAL=$(json_get "$TMP_DIR/form_preview.json" data.approvalStatus)
IS_ADMIN=$(json_get "$TMP_DIR/form_preview.json" data.isAdminPreview)
PREVIEW_TITLE=$(json_get "$TMP_DIR/form_preview.json" data.form.title)
FIELD_COUNT=$(python3 - "$TMP_DIR/form_preview.json" <<'PY'
import json,sys
d=json.load(open(sys.argv[1],encoding='utf-8'))
fields=((d.get('data') or {}).get('form') or {}).get('fields') or []
print(len(fields))
PY
)
FIELD0=$(python3 - "$TMP_DIR/form_preview.json" <<'PY'
import json,sys
d=json.load(open(sys.argv[1],encoding='utf-8'))
fields=((d.get('data') or {}).get('form') or {}).get('fields') or []
print(fields[0].get('label','') if fields else '')
PY
)
assert_eq "form preview API status" "200" "$SC"
assert_eq "form preview itemType" "UserForm" "$ITEM_TYPE"
assert_eq "form preview approvalStatus" "Pending" "$APPROVAL"
assert_eq "form preview isAdminPreview" "true" "$IS_ADMIN"
assert_eq "form preview title matches" "$FORM_TITLE" "$PREVIEW_TITLE"
assert_eq "form preview has 2 fields" "2" "$FIELD_COUNT"
assert_contains "form preview field label" "$FIELD_LABEL" "$FIELD0"

# --- 5) Invalid preview token ---
# فرمت نامعتبر (طول/کاراکتر) → 400
curl -s -o "$TMP_DIR/bad_format.json" "${BASE_URL}/api/Public/QuickSendPreview/not-a-valid-token" >/dev/null
assert_eq "invalid token format 400" "400" "$(json_get "$TMP_DIR/bad_format.json" statusCode)"
assert_eq "invalid token format errorCode" "TOKEN_INVALID" "$(json_get "$TMP_DIR/bad_format.json" errorCode)"

# فرمت درست ولی در cache نیست → 404
FAKE32="abcdefghijklmnopqrstuvwxyz123456"
curl -s -o "$TMP_DIR/bad_token.json" "${BASE_URL}/api/Public/QuickSendPreview/${FAKE32}" >/dev/null
assert_eq "unknown preview token 404" "404" "$(json_get "$TMP_DIR/bad_token.json" statusCode)"
assert_eq "unknown preview token errorCode" "TOKEN_INVALID" "$(json_get "$TMP_DIR/bad_token.json" errorCode)"

curl -s -o "$TMP_DIR/malformed_token.json" "${BASE_URL}/api/Public/QuickSendPreview/%21%21%21" >/dev/null
assert_eq "malformed preview token 400" "400" "$(json_get "$TMP_DIR/malformed_token.json" statusCode)"
assert_eq "malformed preview token errorCode" "TOKEN_INVALID" "$(json_get "$TMP_DIR/malformed_token.json" errorCode)"

# --- 6) Non-visual type rejected for preview-token ---
req POST "/api/LuckyWheel" "$TMP_DIR/wheel_draft.json" \
  -d '{"title":"گردونه پیش‌نمایش","templateKey":"default"}' >/dev/null
WHEEL_ID=$(json_get "$TMP_DIR/wheel_draft.json" data.id)
req POST "/api/LuckyWheel/${WHEEL_ID}/items" "$TMP_DIR/wheel_items.json" \
  -d '{"items":[{"title":"جایزه ۱","weight":1,"displayOrder":0},{"title":"جایزه ۲","weight":1,"displayOrder":1}]}' >/dev/null
req POST "/api/LuckyWheel/${WHEEL_ID}/publish" "$TMP_DIR/pub_wheel.json" -d "{\"slug\":\"preview-wheel-$TS\"}" >/dev/null
req POST "/api/Admin/QuickSendApproval/LuckyWheel/${WHEEL_ID}/preview-token" "$TMP_DIR/wheel_token.json" >/dev/null
assert_eq "wheel preview-token rejected" "400" "$(json_get "$TMP_DIR/wheel_token.json" statusCode)"

# --- 7) BusinessCard publish -> preview ---
req POST "/api/BusinessCard" "$TMP_DIR/card.json" \
  -d '{"templateKey":"business","title":"کارت پیش‌نمایش تست","descriptionEnabled":true,"descriptionText":"توضیح تست","contactEnabled":true,"contactPhone":"09121234567"}' >/dev/null
CARD_ID=$(json_get "$TMP_DIR/card.json" data.id)
if [[ -z "$CARD_ID" ]]; then
  req POST "/api/BusinessCard/draft" "$TMP_DIR/card.json" \
    -d '{"templateKey":"business","title":"کارت پیش‌نمایش تست","descriptionEnabled":true,"descriptionText":"توضیح تست","contactEnabled":true,"contactPhone":"09121234567"}' >/dev/null
  CARD_ID=$(json_get "$TMP_DIR/card.json" data.id)
fi
assert_eq "card created" "true" "$([[ -n "$CARD_ID" ]] && echo true || echo false)"

req POST "/api/BusinessCard/${CARD_ID}/publish" "$TMP_DIR/pub_card.json" -d "{\"slug\":\"$SLUG_CARD\"}" >/dev/null
assert_eq "published card Pending" "Pending" "$(json_get "$TMP_DIR/pub_card.json" data.approvalStatus)"

curl -s -o "$TMP_DIR/pub_card_blocked.json" "${BASE_URL}/api/BusinessCardPublic/${SLUG_CARD}" >/dev/null
assert_eq "public card blocked while pending" "403" "$(json_get "$TMP_DIR/pub_card_blocked.json" statusCode)"

req POST "/api/Admin/QuickSendApproval/BusinessCard/${CARD_ID}/preview-token" "$TMP_DIR/card_token.json" >/dev/null
CARD_TOKEN=$(json_get "$TMP_DIR/card_token.json" data.token)
assert_eq "card preview-token status" "200" "$(json_get "$TMP_DIR/card_token.json" statusCode)"

curl -s -o "$TMP_DIR/card_preview.json" "${BASE_URL}/api/Public/QuickSendPreview/${CARD_TOKEN}" >/dev/null
assert_eq "card preview API status" "200" "$(json_get "$TMP_DIR/card_preview.json" statusCode)"
assert_eq "card preview itemType" "BusinessCard" "$(json_get "$TMP_DIR/card_preview.json" data.itemType)"
assert_contains "card preview has businessCard title" "کارت پیش‌نمایش تست" "$(json_get "$TMP_DIR/card_preview.json" data.businessCard.title)"

# --- 8) Admin getById matches (QuickSendApprovalPreviewPage load step 1) ---
req GET "/api/Admin/QuickSendApproval/UserForm/${FORM_ID}" "$TMP_DIR/form_admin.json" >/dev/null
assert_eq "admin getById form status" "200" "$(json_get "$TMP_DIR/form_admin.json" statusCode)"
assert_eq "admin getById form Pending" "Pending" "$(json_get "$TMP_DIR/form_admin.json" data.approvalStatus)"
assert_eq "admin getById form title" "$FORM_TITLE" "$(json_get "$TMP_DIR/form_admin.json" data.title)"

# --- 9) Approve form -> public accessible, preview still works ---
req POST "/api/Admin/QuickSendApproval/UserForm/${FORM_ID}/approve" "$TMP_DIR/ap_form.json" >/dev/null
assert_eq "approve form" "200" "$(json_get "$TMP_DIR/ap_form.json" statusCode)"

curl -s -o "$TMP_DIR/pub_form_ok.json" "${BASE_URL}/api/FormPublic/${SLUG_FORM}" >/dev/null
assert_eq "public form after approve" "200" "$(json_get "$TMP_DIR/pub_form_ok.json" statusCode)"

req POST "/api/Admin/QuickSendApproval/UserForm/${FORM_ID}/preview-token" "$TMP_DIR/form_token2.json" >/dev/null
TOKEN2=$(json_get "$TMP_DIR/form_token2.json" data.token)
curl -s -o "$TMP_DIR/form_preview2.json" "${BASE_URL}/api/Public/QuickSendPreview/${TOKEN2}" >/dev/null
assert_eq "preview still works after approve" "200" "$(json_get "$TMP_DIR/form_preview2.json" statusCode)"
assert_eq "preview after approve status Approved" "Approved" "$(json_get "$TMP_DIR/form_preview2.json" data.approvalStatus)"

# --- 10) Security: invalid bearer on preview-token (when auth enforced) ---
HTTP=$(curl -s -w "%{http_code}" -o "$TMP_DIR/bad_admin_token.json" \
  -H "Authorization: Bearer not-a-valid-admin-token" \
  -X POST "${BASE_URL}/api/Admin/QuickSendApproval/UserForm/${FORM_ID}/preview-token")
SC=$(json_get "$TMP_DIR/bad_admin_token.json" statusCode)
if [[ "$SC" == "401" ]]; then
  assert_eq "preview-token invalid bearer 401" "401" "$SC"
elif [[ "$SC" == "200" ]]; then
  echo "INFO: DisableAuth active — preview-token without bearer returned 200 (dev only)"
  assert_eq "preview-token dev DisableAuth" "200" "$SC"
else
  assert_eq "preview-token invalid bearer" "401" "$SC"
fi

echo ""
echo "=== RESULT: PASS=$PASS FAIL=$FAIL ==="
if [[ "$FAIL" -gt 0 ]]; then
  exit 1
fi
exit 0
