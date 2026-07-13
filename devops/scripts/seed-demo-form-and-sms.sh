#!/usr/bin/env bash
# ایجاد فرم دمو، publish، ارسال SMS لینک — برای تست end-to-end
# Usage (روی سرور):
#   bash devops/scripts/seed-demo-form-and-sms.sh
#   OWNER_PHONE=09920374397 TARGET_PHONE=09392615526 bash devops/scripts/seed-demo-form-and-sms.sh
set -euo pipefail

API="${API:-http://127.0.0.1:8080}"
OWNER_PHONE="${OWNER_PHONE:-09920374397}"
TARGET_PHONE="${TARGET_PHONE:-09392615526}"
SLUG="${SLUG:-vapp-demo-form-$(date +%s | tail -c 6)}"

log() { echo "[seed] $*"; }

get_token() {
  local login verify otp token
  login=$(curl -sS -X POST "$API/api/Auth/login" \
    -H "Content-Type: application/json" \
    -d "{\"phoneNumber\":\"$OWNER_PHONE\"}")
  otp=$(echo "$login" | python3 -c "import sys,json; d=json.load(sys.stdin); print(d.get('otpCode') or '')")
  if [[ -z "$otp" ]]; then
    otp=$(docker logs --tail 40 vapp_api_prod 2>&1 | grep "DEV OTP" | tail -1 | sed -n 's/.*>>> \([0-9]*\) <<<.*/\1/p')
  fi
  [[ -n "$otp" ]] || { echo "ERROR: OTP not found" >&2; exit 1; }
  verify=$(curl -sS -X POST "$API/api/Auth/verify-login" \
    -H "Content-Type: application/json" \
    -d "{\"phoneNumber\":\"$OWNER_PHONE\",\"otpCode\":\"$otp\"}")
  token=$(echo "$verify" | python3 -c "import sys,json; d=json.load(sys.stdin); print(d['tokens']['accessToken'])")
  echo "$token"
}

log "Login as $OWNER_PHONE ..."
TOKEN=$(get_token)

log "Create form draft slug=$SLUG ..."
CREATE=$(curl -sS -X POST "$API/api/UserForm" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d @- <<EOF
{
  "templateKey": "contact",
  "title": "فرم تست Vapp",
  "description": "لطفاً فرم را پر کنید",
  "slug": "$SLUG",
  "saveToPhonebook": false,
  "notebookIds": [],
  "fields": [
    {"fieldKey":"full_name","fieldType":"text","label":"نام و نام خانوادگی","isRequired":true,"displayOrder":1},
    {"fieldKey":"mobile","fieldType":"tel","label":"شماره موبایل","isRequired":true,"displayOrder":2},
    {"fieldKey":"message","fieldType":"textarea","label":"پیام شما","isRequired":false,"displayOrder":3}
  ]
}
EOF
)
FORM_ID=$(echo "$CREATE" | python3 -c "import sys,json; d=json.load(sys.stdin); print(d['data']['id'])")
log "Form id=$FORM_ID"

log "Publish ..."
PUBLISH=$(curl -sS -X POST "$API/api/UserForm/$FORM_ID/publish" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d "{\"slug\":\"$SLUG\"}")
PUBLIC_URL=$(echo "$PUBLISH" | python3 -c "import sys,json; d=json.load(sys.stdin); print(d['data'].get('publicUrl',''))")
log "publicUrl=$PUBLIC_URL"

log "Public GET ..."
curl -sS "$API/api/FormPublic/$SLUG" | python3 -c "import sys,json; d=json.load(sys.stdin); print('  success=', d.get('success'), 'title=', d.get('data',{}).get('title'))"

log "Send SMS to $TARGET_PHONE ..."
SMS_MSG="لینک فرم Vapp: $PUBLIC_URL"
SMS=$(curl -sS -X POST "$API/api/Sms/send" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d "{\"mobile\":\"$TARGET_PHONE\",\"message\":\"$SMS_MSG\",\"senderNumber\":\"\"}")
echo "$SMS" | python3 -c "import sys,json; d=json.load(sys.stdin); print('  sms success=', d.get('success'), 'message=', d.get('message'))"

log "Submit sample response ..."
SUBMIT=$(curl -sS -X POST "$API/api/FormPublic/$SLUG/submit" \
  -H "Content-Type: application/json" \
  -d "{\"participantFullName\":\"تست خودکار\",\"participantMobile\":\"$TARGET_PHONE\",\"values\":{\"full_name\":\"تست خودکار\",\"mobile\":\"$TARGET_PHONE\",\"message\":\"ارسال تست از سرور\"}}")
echo "$SUBMIT" | python3 -c "import sys,json; d=json.load(sys.stdin); print('  submit success=', d.get('success'), 'id=', d.get('data',{}).get('submissionId'))"

echo ""
echo "=== DONE ==="
echo "Browser: $PUBLIC_URL"
echo "Slug: $SLUG"
echo "FormId: $FORM_ID"
