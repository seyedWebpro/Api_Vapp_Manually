#!/usr/bin/env bash
# Crawl/smoke تست یادآوری نوبت: تنظیم offset → نوبت تأییدشده due → انتظار SMS → بررسی ReminderSentAt + گزارش
# Usage:
#   BASE_URL=http://127.0.0.1:5054 \
#   CUSTOMER_MOBILE=09392615526 \
#   bash devops/scripts/crawl-booking-reminder.sh
set -euo pipefail

BASE_URL="${BASE_URL:-http://127.0.0.1:5054}"
CUSTOMER_MOBILE="${CUSTOMER_MOBILE:-09392615526}"
WAIT_SECONDS="${WAIT_SECONDS:-120}"
REMINDER_OFFSET_MINUTES="${REMINDER_OFFSET_MINUTES:-60}"
# چند offset همزمان (اختیاری): مثلاً "60,1440"
REMINDER_OFFSETS_CSV="${REMINDER_OFFSETS_CSV:-60,1440}"
TEST_OPT_OUT="${TEST_OPT_OUT:-1}"
TMP_DIR="$(mktemp -d)"
PASS=0
FAIL=0
SYSTEM_ID=""
SERVICE_ID=""
APPOINTMENT_ID=""
OPT_OUT_APPOINTMENT_ID=""

cleanup() {
  rm -rf "$TMP_DIR"
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

http_json() {
  local method="$1" path="$2" body="${3:-}" out="$4"
  local code
  : > "$out"
  if [[ -n "$body" ]]; then
    code="$(curl -sS -m 30 -o "$out" -w '%{http_code}' -X "$method" "$BASE_URL$path" \
      -H 'Content-Type: application/json' \
      -d "$body" || echo 000)"
  else
    code="$(curl -sS -m 30 -o "$out" -w '%{http_code}' -X "$method" "$BASE_URL$path" || echo 000)"
  fi
  if [[ ! -s "$out" ]]; then
    echo '{}' > "$out"
  fi
  echo "$code"
}

http_ok() {
  local code="$1"
  [[ "$code" == "200" || "$code" == "201" ]]
}

echo "=== Booking reminder crawl @ $BASE_URL ==="
echo "      customer=$CUSTOMER_MOBILE offsets=$REMINDER_OFFSETS_CSV wait=${WAIT_SECONDS}s"

# 0) health
code="$(curl -sS -m 10 -o /dev/null -w '%{http_code}' "$BASE_URL/health" || echo 000)"
check "GET /health → 200" "$([[ "$code" == "200" ]] && echo 1 || echo 0)"

# 0.05) reminder-info (no text approval + template)
INFO_OUT="$TMP_DIR/reminder_info.json"
code="$(http_json GET /api/BookingSystem/reminder-info "" "$INFO_OUT")"
REQ_APPROVAL="$(json_get "$INFO_OUT" data.requiresTextApproval)"
SAMPLE="$(json_get "$INFO_OUT" data.sampleMessage)"
check "GET reminder-info → 200" "$([[ "$code" == "200" ]] && echo 1 || echo 0)"
check "requiresTextApproval=false" "$([[ "$REQ_APPROVAL" == "false" ]] && echo 1 || echo 0)"
HAS_TITLE="$(python3 - "$SAMPLE" <<'PY'
import sys
t=sys.argv[1]
print('1' if ('یادآوری نوبت' in t) else '0')
PY
)"
HAS_CANCEL="$(python3 - "$SAMPLE" <<'PY'
import sys
t=sys.argv[1]
print('1' if ('لغو11' in t) else '0')
PY
)"
check "sampleMessage contains یادآوری نوبت" "$([[ "$HAS_TITLE" == "1" ]] && echo 1 || echo 0)"
check "sampleMessage contains لغو11" "$([[ "$HAS_CANCEL" == "1" ]] && echo 1 || echo 0)"
echo "      sample=$SAMPLE"

# 0.1) profile + wallet
PROFILE_OUT="$TMP_DIR/profile.json"
code="$(http_json GET /api/User/profile "" "$PROFILE_OUT")"
USER_ID="$(json_get "$PROFILE_OUT" data.id)"
WALLET="$(json_get "$PROFILE_OUT" data.walletBalance)"
check "GET /api/User/profile → 200" "$([[ "$code" == "200" ]] && echo 1 || echo 0)"
check "profile has user id" "$([[ -n "$USER_ID" && "$USER_ID" != "0" ]] && echo 1 || echo 0)"
echo "      userId=$USER_ID wallet=$WALLET"

# 0.2) ensure wallet has balance (billing may be on)
python3 - "$WALLET" <<'PY' >/dev/null
import sys
w=float(sys.argv[1] or 0)
sys.exit(0 if w >= 1000 else 1)
PY
WALLET_OK=$?
if [[ "$WALLET_OK" -ne 0 ]]; then
  echo "WARN  wallet balance low ($WALLET) — attempt SQL top-up via docker vapp_sqlserver_dev"
  docker exec vapp_sqlserver_dev /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'Vapp@Secure2025!' -C -d DbVapp \
    -Q "UPDATE Users SET WalletBalance = CASE WHEN WalletBalance < 50000 THEN 50000 ELSE WalletBalance END WHERE Id = ${USER_ID}; SELECT WalletBalance FROM Users WHERE Id = ${USER_ID};" \
    2>/dev/null || docker exec vapp_sqlserver_dev /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P 'Vapp@Secure2025!' -C -d DbVapp \
    -Q "UPDATE Users SET WalletBalance = CASE WHEN WalletBalance < 50000 THEN 50000 ELSE WalletBalance END WHERE Id = ${USER_ID};" \
    2>/dev/null || echo "WARN  SQL top-up failed — reminder may skip if billing enabled"
  PROFILE_OUT2="$TMP_DIR/profile2.json"
  http_json GET /api/User/profile "" "$PROFILE_OUT2" >/dev/null || true
  WALLET="$(json_get "$PROFILE_OUT2" data.walletBalance)"
  echo "      wallet after top-up attempt=$WALLET"
fi

# 1) pick or create booking system
LIST_OUT="$TMP_DIR/list.json"
code="$(http_json GET /api/BookingSystem "" "$LIST_OUT")"
SYSTEM_ID="$(python3 - "$LIST_OUT" <<'PY'
import json,sys
d=json.load(open(sys.argv[1],encoding='utf-8'))
systems=(d.get('data') or {}).get('systems') or []
# فقط سیستمی که خدمت دارد را انتخاب کن (بعداً services چک می‌شود)
for s in systems:
  if s.get('isActive'):
    print(s.get('id') or ''); break
else:
  if systems: print(systems[0].get('id') or '')
  else: print('')
PY
)"
check "GET BookingSystem list → 200" "$([[ "$code" == "200" ]] && echo 1 || echo 0)"

create_system_via_wizard() {
  local STEP1="$TMP_DIR/step1.json"
  local SUFFIX
  SUFFIX="$(python3 -c 'import uuid;print(uuid.uuid4().hex[:6])')"
  local code DRAFT_ID TEMP_ID DAYS_JSON
  code="$(http_json POST /api/BookingSystem/validate-step1 \
    "{\"title\":\"یادآوری تست ${SUFFIX}\",\"activityType\":\"beauty_salon\",\"description\":\"crawl reminder\",\"saveToPhonebook\":false,\"notebookIds\":[]}" \
    "$STEP1")"
  DRAFT_ID="$(json_get "$STEP1" data.draftId)"
  check "validate-step1" "$(http_ok "$code" && [[ -n "$DRAFT_ID" ]] && echo 1 || echo 0)"
  TEMP_ID="$(python3 -c 'import uuid;print(uuid.uuid4().hex)')"
  local STEP2="$TMP_DIR/step2.json"
  code="$(http_json POST /api/BookingSystem/validate-step2 \
    "{\"draftId\":\"$DRAFT_ID\",\"services\":[{\"serviceTempId\":\"$TEMP_ID\",\"title\":\"خدمت تست\",\"durationMinutes\":30,\"hasCost\":false}]}" \
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
  local STEP3="$TMP_DIR/step3.json"
  code="$(http_json POST /api/BookingSystem/validate-step3 \
    "{\"draftId\":\"$DRAFT_ID\",\"serviceSchedules\":[{\"serviceTempId\":\"$TEMP_ID\",\"weeklyDays\":$DAYS_JSON,\"exceptions\":[]}]}" \
    "$STEP3")"
  check "validate-step3" "$(http_ok "$code" && echo 1 || echo 0)"
  local STEP4="$TMP_DIR/step4.json"
  code="$(http_json POST /api/BookingSystem/validate-step4 \
    "{\"draftId\":\"$DRAFT_ID\",\"serviceSettings\":[{\"serviceTempId\":\"$TEMP_ID\",\"bufferMinutesBetweenAppointments\":0,\"maxDailyReservations\":50,\"reminderOffsetMinutes\":$REMINDER_OFFSET_MINUTES}]}" \
    "$STEP4")"
  check "validate-step4 reminderOffset=$REMINDER_OFFSET_MINUTES" "$(http_ok "$code" && echo 1 || echo 0)"
  local CONF="$TMP_DIR/confirm.json"
  code="$(http_json POST /api/BookingSystem/confirm "{\"draftId\":\"$DRAFT_ID\"}" "$CONF")"
  SYSTEM_ID="$(json_get "$CONF" data.system.id)"
  check "confirm system" "$(http_ok "$code" && [[ -n "$SYSTEM_ID" ]] && echo 1 || echo 0)"
}

if [[ -z "$SYSTEM_ID" || "$SYSTEM_ID" == "0" ]]; then
  echo "      no system found — creating via wizard"
  create_system_via_wizard
fi
echo "      systemId=$SYSTEM_ID"

# 2) services + set reminder offset
SERVICES_OUT="$TMP_DIR/services.json"
code="$(http_json GET "/api/BookingSystem/$SYSTEM_ID/services" "" "$SERVICES_OUT")"
SERVICE_ID="$(python3 - "$SERVICES_OUT" <<'PY'
import json,sys
try:
  d=json.load(open(sys.argv[1],encoding='utf-8'))
except Exception:
  print(''); raise SystemExit
items=d.get('data') or []
print(items[0].get('id') if items else '')
PY
)"

if [[ -z "$SERVICE_ID" || "$SERVICE_ID" == "0" ]]; then
  echo "      system $SYSTEM_ID has no services — creating fresh system"
  create_system_via_wizard
  code="$(http_json GET "/api/BookingSystem/$SYSTEM_ID/services" "" "$SERVICES_OUT")"
  SERVICE_ID="$(python3 - "$SERVICES_OUT" <<'PY'
import json,sys
d=json.load(open(sys.argv[1],encoding='utf-8'))
items=d.get('data') or []
print(items[0].get('id') if items else '')
PY
)"
fi
check "GET services → has service" "$([[ "$code" == "200" && -n "$SERVICE_ID" && "$SERVICE_ID" != "0" ]] && echo 1 || echo 0)"
OFFSETS_JSON="$(python3 - "$REMINDER_OFFSETS_CSV" <<'PY'
import sys,json
parts=[int(x) for x in sys.argv[1].split(',') if x.strip()]
print(json.dumps(parts))
PY
)"
UPD="$TMP_DIR/svc_upd.json"
code="$(http_json POST "/api/BookingSystem/$SYSTEM_ID/services/$SERVICE_ID/update" \
  "{\"reminderOffsetsMinutes\":$OFFSETS_JSON}" "$UPD")"
OFFSETS_SAVED="$(python3 - "$UPD" <<'PY'
import json,sys
d=json.load(open(sys.argv[1],encoding='utf-8'))
print(','.join(str(x) for x in ((d.get('data') or {}).get('reminderOffsetsMinutes') or [])))
PY
)"
check "update reminderOffsetsMinutes=$REMINDER_OFFSETS_CSV" \
  "$(http_ok "$code" && [[ -n "$OFFSETS_SAVED" ]] && echo 1 || echo 0)"
echo "      serviceId=$SERVICE_ID offsets=$OFFSETS_SAVED"

# 3) pick a free public slot that is already due for reminder (StartUtc - offset <= now < StartUtc)
GET_SYS="$TMP_DIR/sys.json"
http_json GET "/api/BookingSystem/$SYSTEM_ID" "" "$GET_SYS" >/dev/null || true
SLUG="$(json_get "$GET_SYS" data.slug)"
echo "      slug=$SLUG"

START_UTC="$(python3 - "$BASE_URL" "$SLUG" "$SERVICE_ID" "$REMINDER_OFFSETS_CSV" <<'PY'
import json,sys,urllib.request
from datetime import datetime,timedelta,timezone
base,slug,svc=sys.argv[1],sys.argv[2],sys.argv[3]
offsets=[int(x) for x in sys.argv[4].split(',') if x.strip()] or [60]
max_offset=max(offsets)
now=datetime.now(timezone.utc)
picked=""
for day in range(0,3):
  date=(now+timedelta(days=day)).strftime('%Y-%m-%d')
  url=f"{base}/api/BookingPublic/{slug}/services/{svc}/slots?date={date}"
  try:
    with urllib.request.urlopen(url, timeout=20) as r:
      d=json.load(r)
  except Exception:
    continue
  slots=((d.get('data') or {}).get('slots') or [])
  for s in slots:
    raw=s.get('startUtc') or ''
    try:
      start=datetime.fromisoformat(raw.replace('Z','+00:00'))
    except Exception:
      continue
    if start.tzinfo is None:
      start=start.replace(tzinfo=timezone.utc)
    if start <= now:
      continue
    # due for at least one configured offset
    if any(start - timedelta(minutes=o) <= now for o in offsets):
      picked=start.strftime('%Y-%m-%dT%H:%M:%SZ')
      break
  if picked:
    break
if not picked:
  for day in range(0,3):
    date=(now+timedelta(days=day)).strftime('%Y-%m-%d')
    url=f"{base}/api/BookingPublic/{slug}/services/{svc}/slots?date={date}"
    try:
      with urllib.request.urlopen(url, timeout=20) as r:
        d=json.load(r)
    except Exception:
      continue
    slots=((d.get('data') or {}).get('slots') or [])
    for s in slots:
      raw=s.get('startUtc') or ''
      try:
        start=datetime.fromisoformat(raw.replace('Z','+00:00'))
      except Exception:
        continue
      if start.tzinfo is None:
        start=start.replace(tzinfo=timezone.utc)
      if start > now:
        picked=start.strftime('%Y-%m-%dT%H:%M:%SZ')
        break
    if picked:
      break
print(picked)
PY
)"
echo "      target StartUtc=$START_UTC (aligned public slot)"
if [[ -z "$START_UTC" ]]; then
  echo "FAIL  no available slot found"
  echo "Summary: PASS=$PASS FAIL=$((FAIL+1))"
  exit 1
fi

MANUAL="$TMP_DIR/manual.json"
code="$(http_json POST "/api/BookingSystem/$SYSTEM_ID/appointments/manual" \
  "{\"customerFullName\":\"تست یادآوری کراول\",\"customerMobile\":\"$CUSTOMER_MOBILE\",\"serviceId\":$SERVICE_ID,\"startUtc\":\"$START_UTC\",\"remindersEnabled\":true}" \
  "$MANUAL")"
APPOINTMENT_ID="$(json_get "$MANUAL" data.id)"
STATUS="$(json_get "$MANUAL" data.status)"
REMINDER_BEFORE="$(json_get "$MANUAL" data.reminderSentAt)"
REMINDERS_ON="$(json_get "$MANUAL" data.remindersEnabled)"
check "POST manual appointment → 201/200" "$(http_ok "$code" && echo 1 || echo 0)"
check "appointment Confirmed" "$([[ "$STATUS" == "Confirmed" ]] && echo 1 || echo 0)"
check "remindersEnabled=true" "$([[ "$REMINDERS_ON" == "true" ]] && echo 1 || echo 0)"
check "reminderSentAt initially null" "$([[ -z "$REMINDER_BEFORE" ]] && echo 1 || echo 0)"
if [[ -z "$APPOINTMENT_ID" || "$APPOINTMENT_ID" == "0" ]]; then
  echo "FAIL  could not create appointment — body:"
  cat "$MANUAL"
  echo
  echo "Summary: PASS=$PASS FAIL=$((FAIL+1))"
  exit 1
fi
echo "      appointmentId=$APPOINTMENT_ID status=$STATUS"

# 3.1) opt-out appointment should NOT get reminder
if [[ "$TEST_OPT_OUT" == "1" ]]; then
  START2="$(python3 - "$BASE_URL" "$SLUG" "$SERVICE_ID" <<'PY'
import json,sys,urllib.request
from datetime import datetime,timedelta,timezone
base,slug,svc=sys.argv[1],sys.argv[2],sys.argv[3]
now=datetime.now(timezone.utc)
for day in range(0,3):
  date=(now+timedelta(days=day)).strftime('%Y-%m-%d')
  url=f"{base}/api/BookingPublic/{slug}/services/{svc}/slots?date={date}"
  try:
    with urllib.request.urlopen(url, timeout=20) as r:
      d=json.load(r)
  except Exception:
    continue
  for s in ((d.get('data') or {}).get('slots') or []):
    raw=s.get('startUtc') or ''
    try:
      start=datetime.fromisoformat(raw.replace('Z','+00:00'))
    except Exception:
      continue
    if start.tzinfo is None:
      start=start.replace(tzinfo=timezone.utc)
    if start > now:
      print(start.strftime('%Y-%m-%dT%H:%M:%SZ'))
      raise SystemExit
print('')
PY
)"
  if [[ -n "$START2" ]]; then
    OPT="$TMP_DIR/optout.json"
    code="$(http_json POST "/api/BookingSystem/$SYSTEM_ID/appointments/manual" \
      "{\"customerFullName\":\"تست بدون یادآوری\",\"customerMobile\":\"$CUSTOMER_MOBILE\",\"serviceId\":$SERVICE_ID,\"startUtc\":\"$START2\",\"remindersEnabled\":false}" \
      "$OPT")"
    OPT_OUT_APPOINTMENT_ID="$(json_get "$OPT" data.id)"
    OPT_FLAG="$(json_get "$OPT" data.remindersEnabled)"
    check "opt-out appointment created" "$(http_ok "$code" && [[ -n "$OPT_OUT_APPOINTMENT_ID" ]] && echo 1 || echo 0)"
    check "opt-out remindersEnabled=false" "$([[ "$OPT_FLAG" == "false" ]] && echo 1 || echo 0)"
    echo "      optOutAppointmentId=$OPT_OUT_APPOINTMENT_ID start2=$START2"
  else
    echo "WARN  no second slot for opt-out test"
  fi
fi

# 4) wait for background BookingReminderBackgroundService (1 min tick + 45s startup)
echo "      waiting up to ${WAIT_SECONDS}s for ReminderSentAt..."
DEADLINE=$((SECONDS + WAIT_SECONDS))
GOT_SENT=""
OFFSETS_SENT=""
while (( SECONDS < DEADLINE )); do
  GET_APPT="$TMP_DIR/appt.json"
  http_json GET "/api/BookingSystem/$SYSTEM_ID/appointments/$APPOINTMENT_ID" "" "$GET_APPT" >/dev/null || true
  GOT_SENT="$(json_get "$GET_APPT" data.reminderSentAt)"
  OFFSETS_SENT="$(python3 - "$GET_APPT" <<'PY'
import json,sys
d=json.load(open(sys.argv[1],encoding='utf-8'))
print(','.join(str(x) for x in ((d.get('data') or {}).get('reminderOffsetsSent') or [])))
PY
)"
  if [[ -n "$GOT_SENT" ]]; then
    break
  fi
  sleep 5
done
check "ReminderSentAt set by background job" "$([[ -n "$GOT_SENT" ]] && echo 1 || echo 0)"
check "reminderOffsetsSent not empty" "$([[ -n "$OFFSETS_SENT" ]] && echo 1 || echo 0)"
echo "      reminderSentAt=$GOT_SENT offsetsSent=$OFFSETS_SENT"

# 4.1) opt-out must remain unsent
if [[ -n "$OPT_OUT_APPOINTMENT_ID" ]]; then
  sleep 2
  GET_OPT="$TMP_DIR/opt_appt.json"
  http_json GET "/api/BookingSystem/$SYSTEM_ID/appointments/$OPT_OUT_APPOINTMENT_ID" "" "$GET_OPT" >/dev/null || true
  OPT_SENT="$(json_get "$GET_OPT" data.reminderSentAt)"
  check "opt-out ReminderSentAt stays null" "$([[ -z "$OPT_SENT" ]] && echo 1 || echo 0)"
fi

# 5) SMS delivery report for BookingReminder
REPORTS="$TMP_DIR/reports.json"
code="$(http_json GET "/api/sms/delivery-reports?sourceModule=BookingReminder&pageSize=20&pageNumber=1" "" "$REPORTS")"
FOUND_SMS="$(python3 - "$REPORTS" "$CUSTOMER_MOBILE" "$APPOINTMENT_ID" <<'PY'
import json,sys
try:
  d=json.load(open(sys.argv[1],encoding='utf-8'))
except Exception:
  print('0'); raise SystemExit
mobile=sys.argv[2]
aid=int(sys.argv[3])
data=d.get('data') or {}
items=data.get('items') or data.get('reports') or []
if isinstance(data, list):
  items=data
ok=False
for it in items:
  m=(it.get('mobile') or it.get('recipientMobile') or '')
  sid=it.get('sourceEntityId')
  mod=(it.get('sourceModule') or '')
  txt=(it.get('messageText') or it.get('message') or '')
  if mod and mod!='BookingReminder':
    continue
  if str(sid)==str(aid) or (mobile[-10:] in m and 'یادآوری' in txt):
    ok=True
    break
print('1' if ok else '0')
PY
)"
# اگر گزارش به خاطر فیچر اشتراک 403 شد، لاگ بک‌گراند را به‌عنوان fallback قبول نکن — فقط هشدار
if [[ "$code" == "403" || "$code" == "401" ]]; then
  echo "WARN  SMS delivery report HTTP $code (subscription/auth) — checking DB fallback"
  FOUND_SMS="$(docker exec vapp_sqlserver_dev /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'Vapp@Secure2025!' -C -d DbVapp -h -1 -W -Q "SET NOCOUNT ON; SET QUOTED_IDENTIFIER ON; SELECT TOP 1 CASE WHEN COUNT(*)>0 THEN 1 ELSE 0 END FROM SmsDeliveryRecords WHERE SourceModule='BookingReminder' AND SourceEntityId=${APPOINTMENT_ID};" 2>/dev/null | tr -d '[:space:]' || echo 0)"
fi
check "SMS delivery report has BookingReminder for appointment" "$([[ "$FOUND_SMS" == "1" ]] && echo 1 || echo 0)"

# 6) cancel appointments to free slots (best effort)
CANCEL="$TMP_DIR/cancel.json"
http_json POST "/api/BookingSystem/$SYSTEM_ID/appointments/$APPOINTMENT_ID/cancel" \
  '{"cancellationReason":"crawl cleanup"}' "$CANCEL" >/dev/null || true
if [[ -n "$OPT_OUT_APPOINTMENT_ID" ]]; then
  http_json POST "/api/BookingSystem/$SYSTEM_ID/appointments/$OPT_OUT_APPOINTMENT_ID/cancel" \
    '{"cancellationReason":"crawl cleanup opt-out"}' "$CANCEL" >/dev/null || true
fi

echo
echo "=== Summary: PASS=$PASS FAIL=$FAIL ==="
if [[ "$FAIL" -gt 0 ]]; then
  exit 1
fi
exit 0
