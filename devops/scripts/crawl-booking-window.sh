#!/usr/bin/env bash
# Crawl/smoke تست بازه رزرو عمومی (BookingWindowDays per system)
# Usage: BASE_URL=http://127.0.0.1:5054 bash devops/scripts/crawl-booking-window.sh
set -euo pipefail

BASE_URL="${BASE_URL:-http://127.0.0.1:5054}"
TMP_DIR="$(mktemp -d)"
PASS=0
FAIL=0
SYSTEM_ID=""
SERVICE_ID=""
SLUG=""

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

http_json() {
  local method="$1" path="$2" body="${3:-}" out="$4"
  local code
  : > "$out"
  if [[ -n "$body" ]]; then
    code="$(curl -sS -m 45 -o "$out" -w '%{http_code}' -X "$method" "$BASE_URL$path" \
      -H 'Content-Type: application/json' \
      -d "$body" || echo 000)"
  else
    code="$(curl -sS -m 45 -o "$out" -w '%{http_code}' -X "$method" "$BASE_URL$path" || echo 000)"
  fi
  if [[ ! -s "$out" ]]; then echo '{}' > "$out"; fi
  echo "$code"
}

echo "=== Booking window crawl @ $BASE_URL ==="

code="$(curl -sS -m 10 -o /dev/null -w '%{http_code}' "$BASE_URL/health" || echo 000)"
check "GET /health → 200" "$([[ "$code" == "200" ]] && echo 1 || echo 0)"

# 1) create system with bookingWindowDays=7 via wizard
SUFFIX="$(python3 -c 'import uuid;print(uuid.uuid4().hex[:6])')"
STEP1="$TMP_DIR/step1.json"
code="$(http_json POST /api/BookingSystem/validate-step1 \
  "{\"title\":\"بازه تست ${SUFFIX}\",\"activityType\":\"beauty_salon\",\"description\":\"crawl window\",\"saveToPhonebook\":false,\"notebookIds\":[],\"bookingWindowDays\":7}" \
  "$STEP1")"
DRAFT_ID="$(json_get "$STEP1" data.draftId)"
check "validate-step1 with bookingWindowDays=7" "$(http_ok "$code" && [[ -n "$DRAFT_ID" ]] && echo 1 || echo 0)"

TEMP_ID="$(python3 -c 'import uuid;print(uuid.uuid4().hex)')"
STEP2="$TMP_DIR/step2.json"
code="$(http_json POST /api/BookingSystem/validate-step2 \
  "{\"draftId\":\"$DRAFT_ID\",\"services\":[{\"serviceTempId\":\"$TEMP_ID\",\"title\":\"خدمت بازه\",\"durationMinutes\":30,\"hasCost\":false}]}" \
  "$STEP2")"
check "validate-step2" "$(http_ok "$code" && echo 1 || echo 0)"

DAYS_JSON="$(python3 - <<'PY'
import json
days=[]
for i in range(7):
  days.append({"dayOfWeek":i,"isOpen":True,"startTimeUtc":"00:00:00","endTimeUtc":"23:59:00"})
print(json.dumps(days))
PY
)"
STEP3="$TMP_DIR/step3.json"
code="$(http_json POST /api/BookingSystem/validate-step3 \
  "{\"draftId\":\"$DRAFT_ID\",\"serviceSchedules\":[{\"serviceTempId\":\"$TEMP_ID\",\"weeklyDays\":$DAYS_JSON,\"exceptions\":[]}]}" \
  "$STEP3")"
check "validate-step3" "$(http_ok "$code" && echo 1 || echo 0)"

STEP4="$TMP_DIR/step4.json"
code="$(http_json POST /api/BookingSystem/validate-step4 \
  "{\"draftId\":\"$DRAFT_ID\",\"serviceSettings\":[{\"serviceTempId\":\"$TEMP_ID\",\"bufferMinutesBetweenAppointments\":0,\"maxDailyReservations\":50,\"reminderOffsetMinutes\":60}]}" \
  "$STEP4")"
check "validate-step4" "$(http_ok "$code" && echo 1 || echo 0)"

CONF="$TMP_DIR/confirm.json"
code="$(http_json POST /api/BookingSystem/confirm "{\"draftId\":\"$DRAFT_ID\"}" "$CONF")"
SYSTEM_ID="$(json_get "$CONF" data.system.id)"
SLUG="$(json_get "$CONF" data.system.slug)"
BW_CONFIGURED="$(json_get "$CONF" data.system.bookingWindowDays)"
BW_EFFECTIVE="$(json_get "$CONF" data.system.effectiveBookingWindowDays)"
check "confirm system" "$(http_ok "$code" && [[ -n "$SYSTEM_ID" ]] && echo 1 || echo 0)"
check "confirmed bookingWindowDays=7" "$([[ "$BW_CONFIGURED" == "7" && "$BW_EFFECTIVE" == "7" ]] && echo 1 || echo 0)"
echo "      systemId=$SYSTEM_ID slug=$SLUG"

# 2) invalid step1 window
BAD1="$TMP_DIR/bad1.json"
code="$(http_json POST /api/BookingSystem/validate-step1 \
  "{\"title\":\"بازه بد ${SUFFIX}\",\"activityType\":\"beauty_salon\",\"saveToPhonebook\":false,\"notebookIds\":[],\"bookingWindowDays\":999}" \
  "$BAD1")"
check "validate-step1 invalid window → 400" "$([[ "$code" == "400" ]] && echo 1 || echo 0)"

# 3) public GET returns effective window
PUB="$TMP_DIR/public.json"
code="$(http_json GET "/api/BookingPublic/$SLUG" "" "$PUB")"
PUB_DAYS="$(json_get "$PUB" data.bookingWindowDays)"
PUB_END="$(json_get "$PUB" data.bookingWindowEndDate)"
check "GET public system → 200" "$(http_ok "$code" && echo 1 || echo 0)"
check "public bookingWindowDays=7" "$([[ "$PUB_DAYS" == "7" ]] && echo 1 || echo 0)"

# 4) services + slots inside/outside window
SERVICES="$TMP_DIR/services.json"
code="$(http_json GET "/api/BookingSystem/$SYSTEM_ID/services" "" "$SERVICES")"
SERVICE_ID="$(python3 - "$SERVICES" <<'PY'
import json,sys
d=json.load(open(sys.argv[1],encoding='utf-8'))
items=d.get('data') or []
print(items[0].get('id') if items else '')
PY
)"
check "GET services → 200" "$(http_ok "$code" && [[ -n "$SERVICE_ID" ]] && echo 1 || echo 0)"

IN_DATE="$(python3 - <<'PY'
from datetime import datetime, timedelta, timezone
print((datetime.now(timezone.utc).date() + timedelta(days=5)).isoformat())
PY
)"
OUT_DATE="$(python3 - <<'PY'
from datetime import datetime, timedelta, timezone
print((datetime.now(timezone.utc).date() + timedelta(days=20)).isoformat())
PY
)"

SLOTS_IN="$TMP_DIR/slots_in.json"
code="$(http_json GET "/api/BookingPublic/$SLUG/services/$SERVICE_ID/slots?date=$IN_DATE" "" "$SLOTS_IN")"
check "GET slots inside window (day+5) → 200" "$(http_ok "$code" && echo 1 || echo 0)"

SLOTS_OUT="$TMP_DIR/slots_out.json"
code="$(http_json GET "/api/BookingPublic/$SLUG/services/$SERVICE_ID/slots?date=$OUT_DATE" "" "$SLOTS_OUT")"
OUT_SUCCESS="$(json_get "$SLOTS_OUT" success)"
OUT_MSG="$(json_get "$SLOTS_OUT" message)"
check "GET slots outside window (day+20) → 400" "$([[ "$code" == "400" && "$OUT_SUCCESS" == "false" ]] && echo 1 || echo 0)"
check "outside window message mentions 7 days" "$(python3 - "$OUT_MSG" <<'PY'
import sys
print('1' if '7' in sys.argv[1] else '0')
PY
)"

# 5) update to 30 days
UPD="$TMP_DIR/update.json"
code="$(http_json POST "/api/BookingSystem/$SYSTEM_ID/update" '{"bookingWindowDays":30}' "$UPD")"
UPD_BW="$(json_get "$UPD" data.bookingWindowDays)"
UPD_EFF="$(json_get "$UPD" data.effectiveBookingWindowDays)"
check "POST update bookingWindowDays=30 → 200" "$(http_ok "$code" && echo 1 || echo 0)"
check "update persisted bookingWindowDays=30" "$([[ "$UPD_BW" == "30" && "$UPD_EFF" == "30" ]] && echo 1 || echo 0)"

PUB2="$TMP_DIR/public2.json"
code="$(http_json GET "/api/BookingPublic/$SLUG" "" "$PUB2")"
PUB2_DAYS="$(json_get "$PUB2" data.bookingWindowDays)"
check "public reflects update bookingWindowDays=30" "$([[ "$PUB2_DAYS" == "30" ]] && echo 1 || echo 0)"

MID_DATE="$(python3 - <<'PY'
from datetime import datetime, timedelta, timezone
print((datetime.now(timezone.utc).date() + timedelta(days=20)).isoformat())
PY
)"
SLOTS_MID="$TMP_DIR/slots_mid.json"
code="$(http_json GET "/api/BookingPublic/$SLUG/services/$SERVICE_ID/slots?date=$MID_DATE" "" "$SLOTS_MID")"
check "GET slots day+20 after extend → 200" "$(http_ok "$code" && echo 1 || echo 0)"

# 6) reset to default
UPD2="$TMP_DIR/update2.json"
code="$(http_json POST "/api/BookingSystem/$SYSTEM_ID/update" '{"useDefaultBookingWindow":true}' "$UPD2")"
UPD2_BW="$(json_get "$UPD2" data.bookingWindowDays)"
check "POST useDefaultBookingWindow → 200" "$(http_ok "$code" && echo 1 || echo 0)"
check "configured bookingWindowDays cleared (null/empty)" "$([[ -z "$UPD2_BW" ]] && echo 1 || echo 0)"

# 7) invalid update
BADUPD="$TMP_DIR/badupd.json"
code="$(http_json POST "/api/BookingSystem/$SYSTEM_ID/update" '{"bookingWindowDays":0}' "$BADUPD")"
check "POST update invalid window → 400" "$([[ "$code" == "400" ]] && echo 1 || echo 0)"

echo ""
echo "=== Summary: PASS=$PASS FAIL=$FAIL ==="
if [[ "$FAIL" -gt 0 ]]; then exit 1; fi
