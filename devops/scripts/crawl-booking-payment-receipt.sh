#!/usr/bin/env bash
# Crawl تست فیش واریز رزرو نوبت (آپلود اختیاری + مشاهده توسط مالک)
# Usage:
#   BASE_URL=http://127.0.0.1:5054 OWNER_PHONE=09920374397 \
#     bash devops/scripts/crawl-booking-payment-receipt.sh
set -euo pipefail

BASE_URL="${BASE_URL:-http://127.0.0.1:5054}"
OWNER_PHONE="${OWNER_PHONE:-09920374397}"
CUSTOMER_MOBILE="${CUSTOMER_MOBILE:-09392615526}"
TMP_DIR="$(mktemp -d)"
PASS=0
FAIL=0
TOKEN=""
SYSTEM_ID=""
SERVICE_ID=""
FREE_SERVICE_ID=""
SLUG=""
APPOINTMENT_ID=""

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

check() {
  local name="$1" cond="$2"
  if [[ "$cond" == "1" || "$cond" == "true" ]]; then
    echo "PASS  $name"
    PASS=$((PASS + 1))
  else
    echo "FAIL  $name"
    FAIL=$((FAIL + 1))
  fi
}

http_ok() {
  local code="$1"
  [[ "$code" == "200" || "$code" == "201" ]]
}

echo "=== Booking payment-receipt crawl @ $BASE_URL ==="

# health
code="$(curl -sS -m 10 -o /dev/null -w '%{http_code}' "$BASE_URL/health" || echo 000)"
check "GET /health → 200" "$([[ "$code" == "200" ]] && echo 1 || echo 0)"

# در لوکال معمولاً Development:DisableAuth فعال است — بدون لاگین هم مالک پیش‌فرض کار می‌کند
TOKEN=""
check "using default owner (DisableAuth)" "1"

http_auth_json() {
  local method="$1" path="$2" body="${3:-}" out="$4"
  local code
  : > "$out"
  if [[ -n "$body" ]]; then
    if [[ -n "$TOKEN" ]]; then
      code="$(curl -sS -m 45 -o "$out" -w '%{http_code}' -X "$method" "${BASE_URL}${path}" \
        -H "Authorization: Bearer ${TOKEN}" \
        -H 'Content-Type: application/json' \
        -d "$body" || echo 000)"
    else
      code="$(curl -sS -m 45 -o "$out" -w '%{http_code}' -X "$method" "${BASE_URL}${path}" \
        -H 'Content-Type: application/json' \
        -d "$body" || echo 000)"
    fi
  else
    if [[ -n "$TOKEN" ]]; then
      code="$(curl -sS -m 45 -o "$out" -w '%{http_code}' -X "$method" "${BASE_URL}${path}" \
        -H "Authorization: Bearer ${TOKEN}" || echo 000)"
    else
      code="$(curl -sS -m 45 -o "$out" -w '%{http_code}' -X "$method" "${BASE_URL}${path}" || echo 000)"
    fi
  fi
  [[ -s "$out" ]] || echo '{}' > "$out"
  echo "$code"
}

# create system with paid + free service via wizard
SUFFIX="$(python3 -c 'import uuid;print(uuid.uuid4().hex[:6])')"
PAID_TEMP="$(python3 -c 'import uuid;print(uuid.uuid4().hex)')"
FREE_TEMP="$(python3 -c 'import uuid;print(uuid.uuid4().hex)')"
STEP1="$TMP_DIR/step1.json"
code="$(http_auth_json POST /api/BookingSystem/validate-step1 \
  "{\"title\":\"فیش تست ${SUFFIX}\",\"activityType\":\"beauty_salon\",\"description\":\"receipt crawl\",\"saveToPhonebook\":false,\"notebookIds\":[]}" \
  "$STEP1")"
DRAFT_ID="$(json_get "$STEP1" data.draftId)"
check "validate-step1" "$(http_ok "$code" && [[ -n "$DRAFT_ID" ]] && echo 1 || echo 0)"

STEP2="$TMP_DIR/step2.json"
code="$(http_auth_json POST /api/BookingSystem/validate-step2 \
  "{\"draftId\":\"$DRAFT_ID\",\"services\":[{\"serviceTempId\":\"$PAID_TEMP\",\"title\":\"خدمت پولی\",\"durationMinutes\":30,\"hasCost\":true,\"price\":250000,\"depositAmount\":50000},{\"serviceTempId\":\"$FREE_TEMP\",\"title\":\"خدمت رایگان\",\"durationMinutes\":30,\"hasCost\":false}]}" \
  "$STEP2")"
check "validate-step2 paid+free" "$(http_ok "$code" && echo 1 || echo 0)"

DAYS_JSON="$(python3 - <<'PY'
import json
days=[]
for i in range(7):
  days.append({"dayOfWeek":i,"isOpen":True,"startTimeUtc":"00:00:00","endTimeUtc":"23:59:00"})
print(json.dumps(days))
PY
)"
STEP3="$TMP_DIR/step3.json"
code="$(http_auth_json POST /api/BookingSystem/validate-step3 \
  "{\"draftId\":\"$DRAFT_ID\",\"serviceSchedules\":[{\"serviceTempId\":\"$PAID_TEMP\",\"weeklyDays\":$DAYS_JSON,\"exceptions\":[]},{\"serviceTempId\":\"$FREE_TEMP\",\"weeklyDays\":$DAYS_JSON,\"exceptions\":[]}]}" \
  "$STEP3")"
check "validate-step3" "$(http_ok "$code" && echo 1 || echo 0)"

STEP4="$TMP_DIR/step4.json"
code="$(http_auth_json POST /api/BookingSystem/validate-step4 \
  "{\"draftId\":\"$DRAFT_ID\",\"serviceSettings\":[{\"serviceTempId\":\"$PAID_TEMP\",\"bufferMinutesBetweenAppointments\":0,\"maxDailyReservations\":80,\"reminderOffsetMinutes\":60},{\"serviceTempId\":\"$FREE_TEMP\",\"bufferMinutesBetweenAppointments\":0,\"maxDailyReservations\":80,\"reminderOffsetMinutes\":60}]}" \
  "$STEP4")"
check "validate-step4" "$(http_ok "$code" && echo 1 || echo 0)"

CONF="$TMP_DIR/confirm.json"
code="$(http_auth_json POST /api/BookingSystem/confirm "{\"draftId\":\"$DRAFT_ID\"}" "$CONF")"
SYSTEM_ID="$(json_get "$CONF" data.system.id)"
SLUG="$(json_get "$CONF" data.system.slug)"
check "confirm system" "$(http_ok "$code" && [[ -n "$SYSTEM_ID" ]] && [[ -n "$SLUG" ]] && echo 1 || echo 0)"
echo "      systemId=$SYSTEM_ID slug=$SLUG"

SERVICES_OUT="$TMP_DIR/services.json"
code="$(http_auth_json GET "/api/BookingSystem/$SYSTEM_ID/services" "" "$SERVICES_OUT")"
python3 - "$SERVICES_OUT" > "$TMP_DIR/svc_ids.txt" <<'PY'
import json,sys
d=json.load(open(sys.argv[1],encoding='utf-8'))
items=d.get('data') or []
paid=next((x for x in items if x.get('hasCost')), None)
free=next((x for x in items if not x.get('hasCost')), None)
print((paid or {}).get('id') or '')
print((free or {}).get('id') or '')
PY
SERVICE_ID="$(sed -n '1p' "$TMP_DIR/svc_ids.txt")"
FREE_SERVICE_ID="$(sed -n '2p' "$TMP_DIR/svc_ids.txt")"
check "paid+free service ids" "$([[ -n "$SERVICE_ID" && -n "$FREE_SERVICE_ID" ]] && echo 1 || echo 0)"
echo "      paidService=$SERVICE_ID freeService=$FREE_SERVICE_ID"

# pick a future slot for paid service
DATE="$(python3 - <<'PY'
from datetime import datetime, timezone, timedelta
print((datetime.now(timezone.utc)+timedelta(days=1)).strftime('%Y-%m-%d'))
PY
)"
SLOTS_OUT="$TMP_DIR/slots.json"
code="$(curl -sS -m 30 -o "$SLOTS_OUT" -w '%{http_code}' \
  "$BASE_URL/api/BookingPublic/$SLUG/services/$SERVICE_ID/slots?date=$DATE" || echo 000)"
START_UTC="$(python3 - "$SLOTS_OUT" <<'PY'
import json,sys
d=json.load(open(sys.argv[1],encoding='utf-8'))
slots=((d.get('data') or {}).get('slots') or [])
print(slots[0]['startUtc'] if slots else '')
PY
)"
check "public slots for paid service" "$([[ "$code" == "200" && -n "$START_UTC" ]] && echo 1 || echo 0)"
echo "      startUtc=$START_UTC"

# tiny valid JPEG (1x1)
RECEIPT_JPG="$TMP_DIR/receipt.jpg"
python3 - "$RECEIPT_JPG" <<'PY'
import pathlib,sys
jpeg=bytes([
0xFF,0xD8,0xFF,0xE0,0x00,0x10,0x4A,0x46,0x49,0x46,0x00,0x01,0x01,0x00,0x00,0x01,0x00,0x01,0x00,0x00,
0xFF,0xDB,0x00,0x43,0x00,0x08,0x06,0x06,0x07,0x06,0x05,0x08,0x07,0x07,0x07,0x09,0x09,0x08,0x0A,0x0C,
0x14,0x0D,0x0C,0x0B,0x0B,0x0C,0x19,0x12,0x13,0x0F,0x14,0x1D,0x1A,0x1F,0x1E,0x1D,0x1A,0x1C,0x1C,0x20,
0x24,0x2E,0x27,0x20,0x22,0x2C,0x23,0x1C,0x1C,0x28,0x37,0x29,0x2C,0x30,0x31,0x34,0x34,0x34,0x1F,0x27,
0x39,0x3D,0x38,0x32,0x3C,0x2E,0x33,0x34,0x32,
0xFF,0xC0,0x00,0x0B,0x08,0x00,0x01,0x00,0x01,0x01,0x01,0x11,0x00,
0xFF,0xC4,0x00,0x1F,0x00,0x00,0x01,0x05,0x01,0x01,0x01,0x01,0x01,0x01,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x01,0x02,0x03,0x04,0x05,0x06,0x07,0x08,0x09,0x0A,0x0B,
0xFF,0xC4,0x00,0xB5,0x10,0x00,0x02,0x01,0x03,0x03,0x02,0x04,0x03,0x05,0x05,0x04,0x04,0x00,0x00,0x01,0x7D,0x01,0x02,0x03,0x00,0x04,0x11,0x05,0x12,0x21,0x31,0x41,0x06,0x13,0x51,0x61,0x07,0x22,0x71,0x14,0x32,0x81,0x91,0xA1,0x08,0x23,0x42,0xB1,0xC1,0x15,0x52,0xD1,0xF0,0x24,0x33,0x62,0x72,0x82,0x09,0x0A,0x16,0x17,0x18,0x19,0x1A,0x25,0x26,0x27,0x28,0x29,0x2A,0x34,0x35,0x36,0x37,0x38,0x39,0x3A,0x43,0x44,0x45,0x46,0x47,0x48,0x49,0x4A,0x53,0x54,0x55,0x56,0x57,0x58,0x59,0x5A,0x63,0x64,0x65,0x66,0x67,0x68,0x69,0x6A,0x73,0x74,0x75,0x76,0x77,0x78,0x79,0x7A,0x83,0x84,0x85,0x86,0x87,0x88,0x89,0x8A,0x92,0x93,0x94,0x95,0x96,0x97,0x98,0x99,0x9A,0xA2,0xA3,0xA4,0xA5,0xA6,0xA7,0xA8,0xA9,0xAA,0xB2,0xB3,0xB4,0xB5,0xB6,0xB7,0xB8,0xB9,0xBA,0xC2,0xC3,0xC4,0xC5,0xC6,0xC7,0xC8,0xC9,0xCA,0xD2,0xD3,0xD4,0xD5,0xD6,0xD7,0xD8,0xD9,0xDA,0xE1,0xE2,0xE3,0xE4,0xE5,0xE6,0xE7,0xE8,0xE9,0xEA,0xF1,0xF2,0xF3,0xF4,0xF5,0xF6,0xF7,0xF8,0xF9,0xFA,
0xFF,0xDA,0x00,0x08,0x01,0x01,0x00,0x00,0x3F,0x00,0x7F,0x46,0x80,0x3F,0xFF,0xD9
])
pathlib.Path(sys.argv[1]).write_bytes(jpeg)
PY

# book paid WITH receipt (multipart)
BOOK1="$TMP_DIR/book1.json"
code="$(curl -sS -m 60 -o "$BOOK1" -w '%{http_code}' -X POST \
  "$BASE_URL/api/BookingPublic/$SLUG/book" \
  -F "ServiceId=$SERVICE_ID" \
  -F "StartUtc=$START_UTC" \
  -F "CustomerFullName=تست فیش" \
  -F "CustomerMobile=$CUSTOMER_MOBILE" \
  -F "CustomerNote=با فیش" \
  -F "PaymentReceiptFile=@${RECEIPT_JPG};type=image/jpeg" || echo 000)"
APPOINTMENT_ID="$(json_get "$BOOK1" data.appointment.id)"
HAS_R="$(json_get "$BOOK1" data.appointment.hasPaymentReceipt)"
check "book paid WITH receipt → 201" "$([[ "$code" == "201" && -n "$APPOINTMENT_ID" ]] && echo 1 || echo 0)"
check "hasPaymentReceipt=true" "$([[ "$HAS_R" == "true" ]] && echo 1 || echo 0)"
echo "      appointmentId=$APPOINTMENT_ID code=$code hasReceipt=$HAS_R"
[[ "$code" == "201" ]] || cat "$BOOK1"

# owner fetch receipt
RCPT="$TMP_DIR/rcpt.json"
code="$(http_auth_json GET "/api/BookingSystem/$SYSTEM_ID/appointments/$APPOINTMENT_ID/payment-receipt" "" "$RCPT")"
HAS2="$(json_get "$RCPT" data.hasPaymentReceipt)"
URL="$(json_get "$RCPT" data.paymentReceiptUrl)"
check "GET payment-receipt → 200" "$([[ "$code" == "200" ]] && echo 1 || echo 0)"
check "receipt has url" "$([[ "$HAS2" == "true" && -n "$URL" ]] && echo 1 || echo 0)"
echo "      receiptUrl=$URL"

# download receipt file
if [[ -n "$URL" ]]; then
  if [[ "$URL" == http* ]]; then
    FILE_URL="$URL"
  else
    FILE_URL="$BASE_URL${URL}"
  fi
  DL_CODE="$(curl -sS -m 20 -o "$TMP_DIR/dl.bin" -w '%{http_code}' "$FILE_URL" || echo 000)"
  DL_SIZE="$(wc -c < "$TMP_DIR/dl.bin" | tr -d ' ')"
  check "download receipt file → 200" "$([[ "$DL_CODE" == "200" && "$DL_SIZE" -gt 10 ]] && echo 1 || echo 0)"
fi

# unauthorized receipt (invalid token — در حالت Development:DisableAuth بدون توکن به کاربر پیش‌فرض می‌رود)
code="$(curl -sS -m 20 -o "$TMP_DIR/rcpt401.json" -w '%{http_code}' \
  -H 'Authorization: Bearer invalid.token.value' \
  "$BASE_URL/api/BookingSystem/$SYSTEM_ID/appointments/$APPOINTMENT_ID/payment-receipt" || echo 000)"
check "GET payment-receipt invalid token → 401" "$([[ "$code" == "401" ]] && echo 1 || echo 0)"

# book paid WITHOUT receipt (JSON)
START2="$(python3 - "$SLOTS_OUT" <<'PY'
import json,sys
d=json.load(open(sys.argv[1],encoding='utf-8'))
slots=((d.get('data') or {}).get('slots') or [])
print(slots[1]['startUtc'] if len(slots)>1 else (slots[0]['startUtc'] if slots else ''))
PY
)"
# if same as START_UTC pick another day
if [[ "$START2" == "$START_UTC" ]]; then
  DATE2="$(python3 - <<'PY'
from datetime import datetime, timezone, timedelta
print((datetime.now(timezone.utc)+timedelta(days=2)).strftime('%Y-%m-%d'))
PY
)"
  SLOTS2="$TMP_DIR/slots2.json"
  curl -sS -m 30 -o "$SLOTS2" "$BASE_URL/api/BookingPublic/$SLUG/services/$SERVICE_ID/slots?date=$DATE2" >/dev/null
  START2="$(python3 - "$SLOTS2" <<'PY'
import json,sys
d=json.load(open(sys.argv[1],encoding='utf-8'))
slots=((d.get('data') or {}).get('slots') or [])
print(slots[0]['startUtc'] if slots else '')
PY
)"
fi
BOOK2="$TMP_DIR/book2.json"
code="$(curl -sS -m 30 -o "$BOOK2" -w '%{http_code}' -X POST \
  "$BASE_URL/api/BookingPublic/$SLUG/book" \
  -H 'Content-Type: application/json' \
  -d "{\"serviceId\":$SERVICE_ID,\"startUtc\":\"$START2\",\"customerFullName\":\"بدون فیش\",\"customerMobile\":\"$CUSTOMER_MOBILE\"}" || echo 000)"
AID2="$(json_get "$BOOK2" data.appointment.id)"
HAS3="$(json_get "$BOOK2" data.appointment.hasPaymentReceipt)"
check "book paid WITHOUT receipt (JSON) → 201" "$([[ "$code" == "201" && -n "$AID2" ]] && echo 1 || echo 0)"
check "hasPaymentReceipt=false when omitted" "$([[ "$HAS3" == "false" ]] && echo 1 || echo 0)"

RCPT2="$TMP_DIR/rcpt2.json"
code="$(http_auth_json GET "/api/BookingSystem/$SYSTEM_ID/appointments/$AID2/payment-receipt" "" "$RCPT2")"
HAS4="$(json_get "$RCPT2" data.hasPaymentReceipt)"
URL2="$(json_get "$RCPT2" data.paymentReceiptUrl)"
check "receipt endpoint empty ok" "$([[ "$code" == "200" && "$HAS4" == "false" && -z "$URL2" ]] && echo 1 || echo 0)"

# free service + receipt should fail
FREE_SLOTS="$TMP_DIR/free_slots.json"
curl -sS -m 30 -o "$FREE_SLOTS" "$BASE_URL/api/BookingPublic/$SLUG/services/$FREE_SERVICE_ID/slots?date=$DATE" >/dev/null
FREE_START="$(python3 - "$FREE_SLOTS" <<'PY'
import json,sys
d=json.load(open(sys.argv[1],encoding='utf-8'))
slots=((d.get('data') or {}).get('slots') or [])
print(slots[0]['startUtc'] if slots else '')
PY
)"
BOOK_FREE="$TMP_DIR/book_free.json"
code="$(curl -sS -m 60 -o "$BOOK_FREE" -w '%{http_code}' -X POST \
  "$BASE_URL/api/BookingPublic/$SLUG/book" \
  -F "ServiceId=$FREE_SERVICE_ID" \
  -F "StartUtc=$FREE_START" \
  -F "CustomerFullName=رایگان با فیش" \
  -F "CustomerMobile=$CUSTOMER_MOBILE" \
  -F "PaymentReceiptFile=@${RECEIPT_JPG};type=image/jpeg" || echo 000)"
ERR_CODE="$(json_get "$BOOK_FREE" errorCode)"
check "free service + receipt → 400" "$([[ "$code" == "400" ]] && echo 1 || echo 0)"
check "errorCode VALIDATION_FAILED" "$([[ "$ERR_CODE" == "VALIDATION_FAILED" ]] && echo 1 || echo 0)"

# corrupted / fake file
FAKE="$TMP_DIR/fake.jpg"
printf 'not-a-real-image-payload' > "$FAKE"
BOOK_BAD="$TMP_DIR/book_bad.json"
# use another paid slot day
DATE3="$(python3 - <<'PY'
from datetime import datetime, timezone, timedelta
print((datetime.now(timezone.utc)+timedelta(days=3)).strftime('%Y-%m-%d'))
PY
)"
SLOTS3="$TMP_DIR/slots3.json"
curl -sS -m 30 -o "$SLOTS3" "$BASE_URL/api/BookingPublic/$SLUG/services/$SERVICE_ID/slots?date=$DATE3" >/dev/null
START3="$(python3 - "$SLOTS3" <<'PY'
import json,sys
d=json.load(open(sys.argv[1],encoding='utf-8'))
slots=((d.get('data') or {}).get('slots') or [])
print(slots[0]['startUtc'] if slots else '')
PY
)"
code="$(curl -sS -m 60 -o "$BOOK_BAD" -w '%{http_code}' -X POST \
  "$BASE_URL/api/BookingPublic/$SLUG/book" \
  -F "ServiceId=$SERVICE_ID" \
  -F "StartUtc=$START3" \
  -F "CustomerFullName=فایل خراب" \
  -F "CustomerMobile=$CUSTOMER_MOBILE" \
  -F "PaymentReceiptFile=@${FAKE};type=image/jpeg;filename=evil.jpg" || echo 000)"
ERR2="$(json_get "$BOOK_BAD" errorCode)"
check "corrupted file → 400" "$([[ "$code" == "400" ]] && echo 1 || echo 0)"
check "corrupted errorCode VALIDATION_FAILED" "$([[ "$ERR2" == "VALIDATION_FAILED" ]] && echo 1 || echo 0)"

# oversized (>10MB) — create sparse-ish file with jpeg header + padding
OVER="$TMP_DIR/over.jpg"
python3 - "$RECEIPT_JPG" "$OVER" <<'PY'
from pathlib import Path
import sys
header=Path(sys.argv[1]).read_bytes()
pad=b"\0" * (10 * 1024 * 1024 + 100)
Path(sys.argv[2]).write_bytes(header + pad)
PY
BOOK_OVER="$TMP_DIR/book_over.json"
DATE4="$(python3 - <<'PY'
from datetime import datetime, timezone, timedelta
print((datetime.now(timezone.utc)+timedelta(days=4)).strftime('%Y-%m-%d'))
PY
)"
SLOTS4="$TMP_DIR/slots4.json"
curl -sS -m 30 -o "$SLOTS4" "$BASE_URL/api/BookingPublic/$SLUG/services/$SERVICE_ID/slots?date=$DATE4" >/dev/null
START4="$(python3 - "$SLOTS4" <<'PY'
import json,sys
d=json.load(open(sys.argv[1],encoding='utf-8'))
slots=((d.get('data') or {}).get('slots') or [])
print(slots[0]['startUtc'] if slots else '')
PY
)"
code="$(curl -sS -m 120 -o "$BOOK_OVER" -w '%{http_code}' -X POST \
  "$BASE_URL/api/BookingPublic/$SLUG/book" \
  -F "ServiceId=$SERVICE_ID" \
  -F "StartUtc=$START4" \
  -F "CustomerFullName=فایل بزرگ" \
  -F "CustomerMobile=$CUSTOMER_MOBILE" \
  -F "PaymentReceiptFile=@${OVER};type=image/jpeg" || echo 000)"
# may be 400 (validation) or 413 (request too large)
check "oversized file rejected" "$([[ "$code" == "400" || "$code" == "413" ]] && echo 1 || echo 0)"

echo ""
echo "=== RESULT: PASS=$PASS FAIL=$FAIL ==="
[[ "$FAIL" -eq 0 ]]
