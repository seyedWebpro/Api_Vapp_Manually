#!/usr/bin/env bash
# Crawl عمیق عنوان ارسال سریع (SmsCaption) برای آیتم‌های لینک‌دار
# Usage: BASE_URL=http://127.0.0.1:5054 bash devops/scripts/crawl-quick-send-sms-caption.sh
set -euo pipefail

BASE_URL="${BASE_URL:-http://127.0.0.1:5054}"
TMP_DIR="$(mktemp -d)"
PASS=0
FAIL=0
TS="$(date +%s)"
CONTACT_ID=""
CARD_ID=""
FORM_ID=""
WHEEL_ID=""
BOOKING_ID=""
USER_ID=""

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
    echo "FAIL: $name missing='$needle' in='${hay:0:200}'"
    FAIL=$((FAIL+1))
  fi
}

assert_true() {
  local name="$1" cond="$2"
  if [[ "$cond" == "1" || "$cond" == "true" ]]; then
    echo "PASS: $name"
    PASS=$((PASS+1))
  else
    echo "FAIL: $name"
    FAIL=$((FAIL+1))
  fi
}

req() {
  local method="$1" path="$2" out="$3"
  shift 3
  local http
  http=$(curl -sS -m 45 -w "%{http_code}" -o "$out" -X "$method" "${BASE_URL}${path}" \
    -H "Content-Type: application/json; charset=utf-8" \
    -H "Accept: application/json" \
    "$@" || echo 000)
  echo "$http"
}

sql() {
  local q="$1"
  docker exec vapp_sqlserver_dev /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'Vapp@Secure2025!' -C -d DbVapp -I -h -1 -W -Q "$q" 2>/dev/null \
    || docker exec vapp_sqlserver_dev /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P 'Vapp@Secure2025!' -C -d DbVapp -h -1 -W -Q "$q" 2>/dev/null
}

# محتوای چندخطی SMS را در یک خط بخوان (sqlcmd فقط خط اول را می‌دهد)
last_message_content() {
  sql "SET NOCOUNT ON; SELECT TOP 1 REPLACE(REPLACE(ISNULL(Content,''), CHAR(13), ''), CHAR(10), N' | ') FROM Messages WHERE UserId=${USER_ID} AND IsDeleted=0 ORDER BY Id DESC;" \
    | head -1 | tr -d '\r' | sed 's/[[:space:]]*$//'
}

echo "=== QuickSend SmsCaption Deep Crawl @ $BASE_URL ==="

# --- health ---
HTTP=$(curl -sS -m 10 -o "$TMP_DIR/health.json" -w "%{http_code}" "$BASE_URL/health" || echo 000)
assert_eq "health http" "200" "$HTTP"

# --- profile / contact ---
HTTP=$(req GET "/api/User/profile" "$TMP_DIR/profile.json")
USER_ID=$(json_get "$TMP_DIR/profile.json" data.id)
assert_true "profile user id" "$([[ -n "$USER_ID" && "$USER_ID" != "0" ]] && echo true || echo false)"
echo "USER_ID=$USER_ID"

HTTP=$(req GET "/api/Contact/notebook/2?pageNumber=1&pageSize=1" "$TMP_DIR/contacts.json")
CONTACT_ID=$(python3 - "$TMP_DIR/contacts.json" <<'PY'
import json,sys
d=json.load(open(sys.argv[1],encoding='utf-8'))
data=d.get('data') or {}
items=data.get('contacts') or data.get('items') or []
print(items[0]['id'] if items else '')
PY
)
if [[ -z "$CONTACT_ID" ]]; then
  HTTP=$(req GET "/api/ContactNotebook?pageNumber=1&pageSize=5&isActive=true" "$TMP_DIR/nbs.json")
  NB_ID=$(python3 - "$TMP_DIR/nbs.json" <<'PY'
import json,sys
d=json.load(open(sys.argv[1],encoding='utf-8'))
data=d.get('data') or {}
items=data.get('items') or data.get('notebooks') or []
print(items[0]['id'] if items else '')
PY
)
  if [[ -n "$NB_ID" ]]; then
    HTTP=$(req GET "/api/Contact/notebook/${NB_ID}?pageNumber=1&pageSize=1" "$TMP_DIR/contacts.json")
    CONTACT_ID=$(python3 - "$TMP_DIR/contacts.json" <<'PY'
import json,sys
d=json.load(open(sys.argv[1],encoding='utf-8'))
data=d.get('data') or {}
items=data.get('contacts') or data.get('items') or []
print(items[0]['id'] if items else '')
PY
)
  fi
fi
echo "CONTACT_ID=$CONTACT_ID"
assert_true "contact found" "$([[ -n "$CONTACT_ID" ]] && echo true || echo false)"

# wallet top-up for quick-send send path
sql "UPDATE Users SET WalletBalance = CASE WHEN WalletBalance < 50000 THEN 50000 ELSE WalletBalance END WHERE Id = ${USER_ID};" >/dev/null || true

# =============================================================================
# A) BusinessCard — caption create / admin preview / SMS content / reset / clear
# =============================================================================
echo ""
echo "--- BusinessCard ---"
CAPTION_BC="کارت تست عنوان ارسال ${TS}"
SLUG_BC="qs-cap-bc-${TS}"

HTTP=$(req POST "/api/BusinessCard" "$TMP_DIR/bc_create.json" \
  -d "{\"templateKey\":\"business\",\"title\":\"کارت SmsCaption\",\"smsCaption\":\"${CAPTION_BC}\",\"descriptionEnabled\":true,\"descriptionText\":\"تست\",\"contactEnabled\":true,\"contactPhone\":\"09121234567\"}")
CARD_ID=$(json_get "$TMP_DIR/bc_create.json" data.id)
if [[ -z "$CARD_ID" ]]; then
  HTTP=$(req POST "/api/BusinessCard/draft" "$TMP_DIR/bc_create.json" \
    -d "{\"templateKey\":\"business\",\"title\":\"کارت SmsCaption\",\"smsCaption\":\"${CAPTION_BC}\",\"descriptionEnabled\":true,\"descriptionText\":\"تست\",\"contactEnabled\":true,\"contactPhone\":\"09121234567\"}")
  CARD_ID=$(json_get "$TMP_DIR/bc_create.json" data.id)
fi
assert_true "card created" "$([[ -n "$CARD_ID" ]] && echo true || echo false)"
assert_eq "card create smsCaption" "$CAPTION_BC" "$(json_get "$TMP_DIR/bc_create.json" data.smsCaption)"
echo "CARD_ID=$CARD_ID"

# validation: too long caption
LONG=$(python3 -c 'print("ا"*101)')
HTTP=$(req POST "/api/BusinessCard/${CARD_ID}/update-info" "$TMP_DIR/bc_long.json" \
  -d "{\"smsCaption\":\"${LONG}\"}")
SC=$(json_get "$TMP_DIR/bc_long.json" statusCode)
assert_eq "card caption maxLength 400" "400" "$SC"

HTTP=$(req POST "/api/BusinessCard/${CARD_ID}/publish" "$TMP_DIR/bc_pub.json" -d "{\"slug\":\"${SLUG_BC}\"}")
assert_eq "card publish" "200" "$(json_get "$TMP_DIR/bc_pub.json" statusCode)"
assert_eq "card published Pending" "Pending" "$(json_get "$TMP_DIR/bc_pub.json" data.approvalStatus)"
PUBLIC_BC=$(json_get "$TMP_DIR/bc_pub.json" data.publicUrl)
assert_contains "card publicUrl has slug" "$SLUG_BC" "$PUBLIC_BC"

# list includes smsCaption
HTTP=$(req GET "/api/BusinessCard?pageNumber=1&pageSize=50" "$TMP_DIR/bc_list.json")
FOUND_CAP=$(python3 - "$TMP_DIR/bc_list.json" "$CARD_ID" "$CAPTION_BC" <<'PY'
import json,sys
d=json.load(open(sys.argv[1],encoding='utf-8'))
cid=int(sys.argv[2]); cap=sys.argv[3]
items=(((d.get('data') or {}).get('cards') or {}).get('items')) or []
hit=next((i for i in items if i.get('id')==cid), None)
print('yes' if hit and hit.get('smsCaption')==cap else 'no')
PY
)
assert_eq "card list has smsCaption" "yes" "$FOUND_CAP"

# admin ContentPreview = caption + url
HTTP=$(req GET "/api/Admin/QuickSendApproval/BusinessCard/${CARD_ID}" "$TMP_DIR/bc_admin.json")
assert_eq "admin get card 200" "200" "$(json_get "$TMP_DIR/bc_admin.json" statusCode)"
PREV=$(json_get "$TMP_DIR/bc_admin.json" data.contentPreview)
ADMIN_URL=$(json_get "$TMP_DIR/bc_admin.json" data.publicUrl)
assert_contains "admin preview has caption" "$CAPTION_BC" "$PREV"
assert_contains "admin preview has url" "$ADMIN_URL" "$PREV"
assert_contains "admin preview has newline draft" $'\n' "$PREV"

HTTP=$(req POST "/api/Admin/QuickSendApproval/BusinessCard/${CARD_ID}/approve" "$TMP_DIR/bc_ap.json")
assert_eq "card approve" "200" "$(json_get "$TMP_DIR/bc_ap.json" statusCode)"

# quick-send should create message with caption+url (not 202)
HTTP=$(req POST "/api/BusinessCard/quick-send" "$TMP_DIR/bc_qs.json" \
  -d "{\"contactId\":${CONTACT_ID},\"businessCardId\":${CARD_ID}}")
SC=$(json_get "$TMP_DIR/bc_qs.json" statusCode)
echo "card quick-send status=$SC msg=$(json_get "$TMP_DIR/bc_qs.json" message)"
assert_true "card qs not pending queue" "$([[ "$SC" != "202" ]] && echo true || echo false)"

MSG_CONTENT=$(last_message_content)
echo "last message content=<<$MSG_CONTENT>>"
assert_contains "SMS has caption" "$CAPTION_BC" "$MSG_CONTENT"
assert_contains "SMS has card slug/url" "$SLUG_BC" "$MSG_CONTENT"

# change caption -> Pending
NEW_CAP="عنوان جدید کارت ${TS}"
HTTP=$(req POST "/api/BusinessCard/${CARD_ID}/update-info" "$TMP_DIR/bc_upd.json" \
  -d "{\"smsCaption\":\"${NEW_CAP}\"}")
assert_eq "card caption update 200" "200" "$(json_get "$TMP_DIR/bc_upd.json" statusCode)"
assert_eq "card caption updated value" "$NEW_CAP" "$(json_get "$TMP_DIR/bc_upd.json" data.smsCaption)"
assert_eq "caption change resets Pending" "Pending" "$(json_get "$TMP_DIR/bc_upd.json" data.approvalStatus)"

# clear caption
HTTP=$(req POST "/api/BusinessCard/${CARD_ID}/update-info" "$TMP_DIR/bc_clear.json" -d '{"smsCaption":""}')
assert_eq "card clear caption 200" "200" "$(json_get "$TMP_DIR/bc_clear.json" statusCode)"
CLEARED=$(json_get "$TMP_DIR/bc_clear.json" data.smsCaption)
assert_eq "card caption cleared" "" "$CLEARED"

# same caption must not reset Approved
HTTP=$(req POST "/api/Admin/QuickSendApproval/BusinessCard/${CARD_ID}/approve" "$TMP_DIR/bc_ap2.json")
assert_eq "card re-approve after clear" "200" "$(json_get "$TMP_DIR/bc_ap2.json" statusCode)"
STABLE_BC="کارت ثابت ${TS}"
HTTP=$(req POST "/api/BusinessCard/${CARD_ID}/update-info" "$TMP_DIR/bc_setcap.json" \
  -d "{\"smsCaption\":\"${STABLE_BC}\"}")
assert_eq "card set caption Pending" "Pending" "$(json_get "$TMP_DIR/bc_setcap.json" data.approvalStatus)"
HTTP=$(req POST "/api/Admin/QuickSendApproval/BusinessCard/${CARD_ID}/approve" "$TMP_DIR/bc_ap3.json")
assert_eq "card approve stable caption" "200" "$(json_get "$TMP_DIR/bc_ap3.json" statusCode)"
HTTP=$(req POST "/api/BusinessCard/${CARD_ID}/update-info" "$TMP_DIR/bc_same.json" \
  -d "{\"smsCaption\":\"${STABLE_BC}\"}")
assert_eq "card same caption stays Approved" "Approved" "$(json_get "$TMP_DIR/bc_same.json" data.approvalStatus)"

# =============================================================================
# B) UserForm
# =============================================================================
echo ""
echo "--- UserForm ---"
CAPTION_UF="فرم تست عنوان ${TS}"
SLUG_UF="qs-cap-uf-${TS}"

HTTP=$(req POST "/api/UserForm" "$TMP_DIR/uf_create.json" \
  -d "{\"title\":\"فرم SmsCaption\",\"smsCaption\":\"${CAPTION_UF}\",\"saveToPhonebook\":false,\"fields\":[{\"fieldKey\":\"full_name\",\"fieldType\":\"text\",\"label\":\"نام\",\"isActive\":true,\"isRequired\":true,\"displayOrder\":1},{\"fieldKey\":\"mobile\",\"fieldType\":\"mobile\",\"label\":\"موبایل\",\"isActive\":true,\"isRequired\":true,\"displayOrder\":2}]}")
FORM_ID=$(json_get "$TMP_DIR/uf_create.json" data.id)
assert_true "form created" "$([[ -n "$FORM_ID" ]] && echo true || echo false)"
assert_eq "form create smsCaption" "$CAPTION_UF" "$(json_get "$TMP_DIR/uf_create.json" data.smsCaption)"
echo "FORM_ID=$FORM_ID"

HTTP=$(req POST "/api/UserForm/${FORM_ID}/publish" "$TMP_DIR/uf_pub.json" -d "{\"slug\":\"${SLUG_UF}\"}")
assert_eq "form publish" "200" "$(json_get "$TMP_DIR/uf_pub.json" statusCode)"
PUBLIC_UF=$(json_get "$TMP_DIR/uf_pub.json" data.publicUrl)

HTTP=$(req GET "/api/Admin/QuickSendApproval/UserForm/${FORM_ID}" "$TMP_DIR/uf_admin.json")
PREV=$(json_get "$TMP_DIR/uf_admin.json" data.contentPreview)
assert_contains "form admin preview caption" "$CAPTION_UF" "$PREV"
assert_contains "form admin preview url" "$(json_get "$TMP_DIR/uf_admin.json" data.publicUrl)" "$PREV"

HTTP=$(req POST "/api/Admin/QuickSendApproval/UserForm/${FORM_ID}/approve" "$TMP_DIR/uf_ap.json")
assert_eq "form approve" "200" "$(json_get "$TMP_DIR/uf_ap.json" statusCode)"

HTTP=$(req POST "/api/UserForm/quick-send" "$TMP_DIR/uf_qs.json" \
  -d "{\"contactId\":${CONTACT_ID},\"userFormId\":${FORM_ID}}")
SC=$(json_get "$TMP_DIR/uf_qs.json" statusCode)
echo "form quick-send status=$SC"
assert_true "form qs not pending" "$([[ "$SC" != "202" ]] && echo true || echo false)"
MSG_CONTENT=$(last_message_content)
assert_contains "form SMS caption" "$CAPTION_UF" "$MSG_CONTENT"
assert_contains "form SMS slug" "$SLUG_UF" "$MSG_CONTENT"

HTTP=$(req POST "/api/UserForm/${FORM_ID}/update-info" "$TMP_DIR/uf_upd.json" \
  -d "{\"smsCaption\":\"فرم ویرایش ${TS}\"}")
assert_eq "form caption reset Pending" "Pending" "$(json_get "$TMP_DIR/uf_upd.json" data.approvalStatus)"

# list smsCaption
HTTP=$(req GET "/api/UserForm?pageNumber=1&pageSize=50" "$TMP_DIR/uf_list.json")
FOUND_CAP=$(python3 - "$TMP_DIR/uf_list.json" "$FORM_ID" <<'PY'
import json,sys
d=json.load(open(sys.argv[1],encoding='utf-8'))
fid=int(sys.argv[2])
items=(((d.get('data') or {}).get('forms') or {}).get('items')) or []
hit=next((i for i in items if i.get('id')==fid), None)
print('yes' if hit and hit.get('smsCaption') else 'no')
PY
)
assert_eq "form list has smsCaption" "yes" "$FOUND_CAP"

STABLE_UF="فرم ثابت ${TS}"
HTTP=$(req POST "/api/UserForm/${FORM_ID}/update-info" "$TMP_DIR/uf_set.json" \
  -d "{\"smsCaption\":\"${STABLE_UF}\"}")
assert_eq "form set stable Pending" "Pending" "$(json_get "$TMP_DIR/uf_set.json" data.approvalStatus)"
HTTP=$(req POST "/api/Admin/QuickSendApproval/UserForm/${FORM_ID}/approve" "$TMP_DIR/uf_ap2.json")
assert_eq "form re-approve" "200" "$(json_get "$TMP_DIR/uf_ap2.json" statusCode)"
HTTP=$(req POST "/api/UserForm/${FORM_ID}/update-info" "$TMP_DIR/uf_same.json" \
  -d "{\"smsCaption\":\"${STABLE_UF}\"}")
assert_eq "form same caption stays Approved" "Approved" "$(json_get "$TMP_DIR/uf_same.json" data.approvalStatus)"

# =============================================================================
# C) LuckyWheel
# =============================================================================
echo ""
echo "--- LuckyWheel ---"
CAPTION_LW="گردونه تست عنوان ${TS}"
SLUG_LW="qs-cap-lw-${TS}"

HTTP=$(req POST "/api/LuckyWheel" "$TMP_DIR/lw_create.json" \
  -d "{\"title\":\"گردونه SmsCaption\",\"smsCaption\":\"${CAPTION_LW}\",\"saveToPhonebook\":false,\"notebookIds\":[]}")
WHEEL_ID=$(json_get "$TMP_DIR/lw_create.json" data.id)
assert_true "wheel created" "$([[ -n "$WHEEL_ID" ]] && echo true || echo false)"
assert_eq "wheel create smsCaption" "$CAPTION_LW" "$(json_get "$TMP_DIR/lw_create.json" data.smsCaption)"
echo "WHEEL_ID=$WHEEL_ID"

# add items then publish
HTTP=$(req POST "/api/LuckyWheel/${WHEEL_ID}/items/add" "$TMP_DIR/lw_items.json" \
  -d '{"items":[{"name":"جایزه ۱","probability":50,"displayOrder":1},{"name":"جایزه ۲","probability":50,"displayOrder":2}]}')
if [[ "$(json_get "$TMP_DIR/lw_items.json" success)" != "true" ]]; then
  HTTP=$(req POST "/api/LuckyWheel/${WHEEL_ID}/items/update" "$TMP_DIR/lw_items.json" \
    -d '{"items":[{"name":"جایزه ۱","probability":50,"displayOrder":1},{"name":"جایزه ۲","probability":50,"displayOrder":2}]}')
fi
assert_eq "wheel items ok" "true" "$(json_get "$TMP_DIR/lw_items.json" success)"

HTTP=$(req POST "/api/LuckyWheel/${WHEEL_ID}/publish" "$TMP_DIR/lw_pub.json" -d "{\"slug\":\"${SLUG_LW}\"}")
assert_eq "wheel publish" "200" "$(json_get "$TMP_DIR/lw_pub.json" statusCode)"

HTTP=$(req GET "/api/Admin/QuickSendApproval/LuckyWheel/${WHEEL_ID}" "$TMP_DIR/lw_admin.json")
PREV=$(json_get "$TMP_DIR/lw_admin.json" data.contentPreview)
assert_contains "wheel admin preview caption" "$CAPTION_LW" "$PREV"

HTTP=$(req POST "/api/Admin/QuickSendApproval/LuckyWheel/${WHEEL_ID}/approve" "$TMP_DIR/lw_ap.json")
assert_eq "wheel approve" "200" "$(json_get "$TMP_DIR/lw_ap.json" statusCode)"

HTTP=$(req POST "/api/LuckyWheel/quick-send" "$TMP_DIR/lw_qs.json" \
  -d "{\"contactId\":${CONTACT_ID},\"luckyWheelId\":${WHEEL_ID}}")
SC=$(json_get "$TMP_DIR/lw_qs.json" statusCode)
echo "wheel quick-send status=$SC"
assert_true "wheel qs not pending" "$([[ "$SC" != "202" ]] && echo true || echo false)"
MSG_CONTENT=$(last_message_content)
assert_contains "wheel SMS caption" "$CAPTION_LW" "$MSG_CONTENT"
assert_contains "wheel SMS slug" "$SLUG_LW" "$MSG_CONTENT"

HTTP=$(req POST "/api/LuckyWheel/${WHEEL_ID}/update-info" "$TMP_DIR/lw_upd.json" \
  -d "{\"smsCaption\":\"گردونه ویرایش ${TS}\"}")
assert_eq "wheel caption reset Pending" "Pending" "$(json_get "$TMP_DIR/lw_upd.json" data.approvalStatus)"

STABLE_LW="گردونه ثابت ${TS}"
HTTP=$(req POST "/api/LuckyWheel/${WHEEL_ID}/update-info" "$TMP_DIR/lw_set.json" \
  -d "{\"smsCaption\":\"${STABLE_LW}\"}")
assert_eq "wheel set stable Pending" "Pending" "$(json_get "$TMP_DIR/lw_set.json" data.approvalStatus)"
HTTP=$(req POST "/api/Admin/QuickSendApproval/LuckyWheel/${WHEEL_ID}/approve" "$TMP_DIR/lw_ap2.json")
assert_eq "wheel re-approve" "200" "$(json_get "$TMP_DIR/lw_ap2.json" statusCode)"
HTTP=$(req POST "/api/LuckyWheel/${WHEEL_ID}/update-info" "$TMP_DIR/lw_same.json" \
  -d "{\"smsCaption\":\"${STABLE_LW}\"}")
assert_eq "wheel same caption stays Approved" "Approved" "$(json_get "$TMP_DIR/lw_same.json" data.approvalStatus)"

# =============================================================================
# D) BookingSystem — wizard with caption + update + admin + quick-send
# =============================================================================
echo ""
echo "--- BookingSystem ---"
CAPTION_BK="رزرو تست عنوان ${TS}"
SLUG_BK="qs-cap-bk-${TS}"
SUFFIX=$(python3 -c 'import uuid;print(uuid.uuid4().hex[:6])')
TEMP_ID=$(python3 -c 'import uuid;print(uuid.uuid4().hex)')

HTTP=$(req POST "/api/BookingSystem/validate-step1" "$TMP_DIR/bk_s1.json" \
  -d "{\"title\":\"رزرو SmsCaption ${SUFFIX}\",\"activityType\":\"beauty_salon\",\"description\":\"crawl caption\",\"smsCaption\":\"${CAPTION_BK}\",\"customSlug\":\"${SLUG_BK}\",\"saveToPhonebook\":false,\"notebookIds\":[]}")
DRAFT_ID=$(json_get "$TMP_DIR/bk_s1.json" data.draftId)
assert_true "booking step1 draft" "$([[ -n "$DRAFT_ID" ]] && echo true || echo false)"

HTTP=$(req POST "/api/BookingSystem/validate-step2" "$TMP_DIR/bk_s2.json" \
  -d "{\"draftId\":\"${DRAFT_ID}\",\"services\":[{\"serviceTempId\":\"${TEMP_ID}\",\"title\":\"خدمت تست\",\"durationMinutes\":30,\"hasCost\":false}]}")
assert_eq "booking step2" "true" "$(json_get "$TMP_DIR/bk_s2.json" success)"

DAYS_JSON=$(python3 - <<'PY'
import json
days=[]
for d in range(7):
    days.append({"dayOfWeek":d,"isOpen":True,"startTimeUtc":"09:00:00","endTimeUtc":"17:00:00"})
print(json.dumps(days,separators=(',',':')))
PY
)
HTTP=$(req POST "/api/BookingSystem/validate-step3" "$TMP_DIR/bk_s3.json" \
  -d "{\"draftId\":\"${DRAFT_ID}\",\"serviceSchedules\":[{\"serviceTempId\":\"${TEMP_ID}\",\"weeklyDays\":${DAYS_JSON},\"exceptions\":[]}]}")
assert_eq "booking step3" "true" "$(json_get "$TMP_DIR/bk_s3.json" success)"

HTTP=$(req POST "/api/BookingSystem/validate-step4" "$TMP_DIR/bk_s4.json" \
  -d "{\"draftId\":\"${DRAFT_ID}\",\"serviceSettings\":[{\"serviceTempId\":\"${TEMP_ID}\",\"bufferMinutesBetweenAppointments\":0,\"maxDailyReservations\":50,\"reminderOffsetMinutes\":60}]}")
assert_eq "booking step4" "true" "$(json_get "$TMP_DIR/bk_s4.json" success)"

HTTP=$(req POST "/api/BookingSystem/confirm" "$TMP_DIR/bk_confirm.json" -d "{\"draftId\":\"${DRAFT_ID}\"}")
BOOKING_ID=$(json_get "$TMP_DIR/bk_confirm.json" data.system.id)
assert_true "booking confirmed" "$([[ -n "$BOOKING_ID" ]] && echo true || echo false)"
assert_eq "booking confirm smsCaption" "$CAPTION_BK" "$(json_get "$TMP_DIR/bk_confirm.json" data.system.smsCaption)"
echo "BOOKING_ID=$BOOKING_ID"

HTTP=$(req GET "/api/Admin/QuickSendApproval/BookingSystem/${BOOKING_ID}" "$TMP_DIR/bk_admin.json")
PREV=$(json_get "$TMP_DIR/bk_admin.json" data.contentPreview)
assert_contains "booking admin preview caption" "$CAPTION_BK" "$PREV"
assert_contains "booking admin preview url" "$(json_get "$TMP_DIR/bk_admin.json" data.publicUrl)" "$PREV"

HTTP=$(req POST "/api/Admin/QuickSendApproval/BookingSystem/${BOOKING_ID}/approve" "$TMP_DIR/bk_ap.json")
assert_eq "booking approve" "200" "$(json_get "$TMP_DIR/bk_ap.json" statusCode)"

HTTP=$(req POST "/api/BookingSystem/quick-send" "$TMP_DIR/bk_qs.json" \
  -d "{\"contactId\":${CONTACT_ID},\"bookingSystemId\":${BOOKING_ID}}")
SC=$(json_get "$TMP_DIR/bk_qs.json" statusCode)
echo "booking quick-send status=$SC msg=$(json_get "$TMP_DIR/bk_qs.json" message)"
assert_true "booking qs not pending" "$([[ "$SC" != "202" ]] && echo true || echo false)"
MSG_CONTENT=$(last_message_content)
assert_contains "booking SMS caption" "$CAPTION_BK" "$MSG_CONTENT"
assert_contains "booking SMS slug" "$SLUG_BK" "$MSG_CONTENT"

# update caption only -> Pending; same-value should not reset after re-approve
HTTP=$(req POST "/api/BookingSystem/${BOOKING_ID}/update" "$TMP_DIR/bk_upd.json" \
  -d "{\"smsCaption\":\"رزرو ویرایش ${TS}\"}")
assert_eq "booking caption update 200" "200" "$(json_get "$TMP_DIR/bk_upd.json" statusCode)"
assert_eq "booking caption reset Pending" "Pending" "$(json_get "$TMP_DIR/bk_upd.json" data.approvalStatus)"

HTTP=$(req POST "/api/Admin/QuickSendApproval/BookingSystem/${BOOKING_ID}/approve" "$TMP_DIR/bk_ap2.json")
assert_eq "booking re-approve" "200" "$(json_get "$TMP_DIR/bk_ap2.json" statusCode)"

# same caption again should stay Approved
HTTP=$(req POST "/api/BookingSystem/${BOOKING_ID}/update" "$TMP_DIR/bk_same.json" \
  -d "{\"smsCaption\":\"رزرو ویرایش ${TS}\"}")
assert_eq "booking same caption stays Approved" "Approved" "$(json_get "$TMP_DIR/bk_same.json" data.approvalStatus)"

# clear caption
HTTP=$(req POST "/api/BookingSystem/${BOOKING_ID}/update" "$TMP_DIR/bk_clear.json" -d '{"smsCaption":""}')
assert_eq "booking clear caption Pending" "Pending" "$(json_get "$TMP_DIR/bk_clear.json" data.approvalStatus)"
assert_eq "booking caption empty" "" "$(json_get "$TMP_DIR/bk_clear.json" data.smsCaption)"

# without caption: admin preview = url only
HTTP=$(req GET "/api/Admin/QuickSendApproval/BookingSystem/${BOOKING_ID}" "$TMP_DIR/bk_admin2.json")
PREV=$(json_get "$TMP_DIR/bk_admin2.json" data.contentPreview)
ADMIN_URL=$(json_get "$TMP_DIR/bk_admin2.json" data.publicUrl)
assert_eq "booking preview without caption equals url" "$ADMIN_URL" "$PREV"

# backward compatible: approve + quick-send URL-only message
HTTP=$(req POST "/api/Admin/QuickSendApproval/BookingSystem/${BOOKING_ID}/approve" "$TMP_DIR/bk_ap3.json")
assert_eq "booking approve url-only" "200" "$(json_get "$TMP_DIR/bk_ap3.json" statusCode)"
HTTP=$(req POST "/api/BookingSystem/quick-send" "$TMP_DIR/bk_qs2.json" \
  -d "{\"contactId\":${CONTACT_ID},\"bookingSystemId\":${BOOKING_ID}}")
SC=$(json_get "$TMP_DIR/bk_qs2.json" statusCode)
assert_true "booking url-only qs not pending" "$([[ "$SC" != "202" ]] && echo true || echo false)"
MSG_CONTENT=$(last_message_content)
assert_contains "url-only SMS has slug" "$SLUG_BK" "$MSG_CONTENT"
NO_OLD=$(python3 - "$MSG_CONTENT" "$CAPTION_BK" <<'PY'
import sys
msg, old = sys.argv[1], sys.argv[2]
print('yes' if old not in msg else 'no')
PY
)
assert_eq "url-only SMS without old caption" "yes" "$NO_OLD"

# =============================================================================
# E) SocialMediaLink — caption + URL
# =============================================================================
echo ""
echo "--- SocialMediaLink ---"
CAPTION_SM="لینک اینستاگرام تست ${TS}"
LINK_URL="https://instagram.com/qs_cap_${TS}"

HTTP=$(req POST "/api/SocialMediaLink" "$TMP_DIR/sm_create.json" \
  -d "{\"platform\":\"Instagram\",\"linkUrl\":\"${LINK_URL}\",\"smsCaption\":\"${CAPTION_SM}\",\"isDefault\":false}")
SM_ID=$(json_get "$TMP_DIR/sm_create.json" data.id)
assert_true "social link created" "$([[ -n "$SM_ID" ]] && echo true || echo false)"
assert_eq "social create smsCaption" "$CAPTION_SM" "$(json_get "$TMP_DIR/sm_create.json" data.smsCaption)"
echo "SM_ID=$SM_ID"

HTTP=$(req GET "/api/Admin/QuickSendApproval/SocialMediaLink/${SM_ID}" "$TMP_DIR/sm_admin.json")
PREV=$(json_get "$TMP_DIR/sm_admin.json" data.contentPreview)
assert_contains "social admin preview caption" "$CAPTION_SM" "$PREV"
assert_contains "social admin preview url" "$LINK_URL" "$PREV"

HTTP=$(req POST "/api/Admin/QuickSendApproval/SocialMediaLink/${SM_ID}/approve" "$TMP_DIR/sm_ap.json")
assert_eq "social approve" "200" "$(json_get "$TMP_DIR/sm_ap.json" statusCode)"

HTTP=$(req POST "/api/SocialMediaLink/quick-send" "$TMP_DIR/sm_qs.json" \
  -d "{\"contactId\":${CONTACT_ID},\"linkId\":${SM_ID}}")
SC=$(json_get "$TMP_DIR/sm_qs.json" statusCode)
echo "social quick-send status=$SC"
assert_true "social qs not pending" "$([[ "$SC" != "202" ]] && echo true || echo false)"
MSG_CONTENT=$(last_message_content)
assert_contains "social SMS caption" "$CAPTION_SM" "$MSG_CONTENT"
assert_contains "social SMS url" "$LINK_URL" "$MSG_CONTENT"

HTTP=$(req POST "/api/SocialMediaLink/${SM_ID}/update" "$TMP_DIR/sm_upd.json" \
  -d "{\"smsCaption\":\"لینک ویرایش ${TS}\"}")
assert_eq "social caption reset Pending" "Pending" "$(json_get "$TMP_DIR/sm_upd.json" data.approvalStatus)"

HTTP=$(req POST "/api/Admin/QuickSendApproval/SocialMediaLink/${SM_ID}/approve" "$TMP_DIR/sm_ap2.json")
assert_eq "social re-approve" "200" "$(json_get "$TMP_DIR/sm_ap2.json" statusCode)"

HTTP=$(req POST "/api/SocialMediaLink/${SM_ID}/update" "$TMP_DIR/sm_same.json" \
  -d "{\"smsCaption\":\"لینک ویرایش ${TS}\"}")
assert_eq "social same caption stays Approved" "Approved" "$(json_get "$TMP_DIR/sm_same.json" data.approvalStatus)"

HTTP=$(req POST "/api/SocialMediaLink/${SM_ID}/update" "$TMP_DIR/sm_clear.json" -d '{"smsCaption":""}')
assert_eq "social clear caption Pending" "Pending" "$(json_get "$TMP_DIR/sm_clear.json" data.approvalStatus)"
CLEARED=$(json_get "$TMP_DIR/sm_clear.json" data.smsCaption)
assert_eq "social caption cleared" "" "$CLEARED"

echo ""
echo "=== RESULT: PASS=$PASS FAIL=$FAIL ==="
if [[ "$FAIL" -gt 0 ]]; then
  exit 1
fi
exit 0
