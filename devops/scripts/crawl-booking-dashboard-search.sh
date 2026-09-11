#!/usr/bin/env bash
# Crawl داشبورد رزرو: آمار (تهران) + سرچ مشتری
#
# Usage:
#   BASE_URL=http://127.0.0.1:5054 bash devops/scripts/crawl-booking-dashboard-search.sh
#   SKIP_API_RESTART=1 bash devops/scripts/crawl-booking-dashboard-search.sh
#   SKIP_BUILD=1 bash devops/scripts/crawl-booking-dashboard-search.sh
set -euo pipefail
export PATH="/usr/bin:/bin:/usr/sbin:/sbin:/usr/local/bin:${PATH:-}"

ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
cd "$ROOT"
BASE="${BASE_URL:-http://127.0.0.1:5054}"
SKIP_API_RESTART="${SKIP_API_RESTART:-0}"
SKIP_BUILD="${SKIP_BUILD:-0}"
LOG=/tmp/vapp-booking-dashboard-crawl.log
PIDFILE=/tmp/vapp-booking-dashboard-crawl.pid
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

def pick(obj, key):
    if not isinstance(obj, dict):
        return None
    if key in obj:
        return obj[key]
    pascal = key[:1].upper() + key[1:] if key else key
    return obj.get(pascal)

path = sys.argv[2].split(".")
with open(sys.argv[1], encoding="utf-8") as f:
    data = json.load(f)
cur = data
for p in path:
    cur = pick(cur, p)
    if cur is None:
        break
if isinstance(cur, bool):
    print("true" if cur else "false")
elif cur is None:
    print("")
else:
    print(cur)
PY
}

check() {
  local name="$1" ok="$2"
  if [[ "$ok" == "1" || "$ok" == "true" ]]; then
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
    code="$(curl -sS -m 45 -o "$out" -w '%{http_code}' -X "$method" "$BASE$path" \
      -H 'Content-Type: application/json' \
      -H 'Accept: application/json' \
      -d "$body" || echo 000)"
  else
    code="$(curl -sS -m 45 -o "$out" -w '%{http_code}' -X "$method" "$BASE$path" \
      -H 'Accept: application/json' || echo 000)"
  fi
  if [[ ! -s "$out" ]]; then echo '{}' > "$out"; fi
  echo "$code"
}

ensure_api() {
  if [[ "$SKIP_API_RESTART" == "1" ]]; then
    local code
    code=$(curl -sS -m 5 -o /dev/null -w "%{http_code}" "$BASE/health" 2>/dev/null || echo 000)
    if [[ "$code" != "200" ]]; then
      echo "API not healthy at $BASE (health=$code) and SKIP_API_RESTART=1"
      exit 1
    fi
    echo "API_READY (existing)"
    return
  fi

  echo "===== BUILD + RESTART API ====="
  export DOTNET_ROOT="${DOTNET_ROOT:-/usr/local/share/dotnet}"
  local DOTNET_BIN="${DOTNET_ROOT}/dotnet"
  [[ -x "$DOTNET_BIN" ]] || DOTNET_BIN="$(command -v dotnet)"

  if [[ "$SKIP_BUILD" == "1" && -f bin/Debug/net8.0/Api_Vapp.dll ]]; then
    echo "SKIP_BUILD=1 — using existing dll"
  else
    "$DOTNET_BIN" build Api_Vapp.csproj --disable-build-servers -p:UseSharedCompilation=false -v q
  fi

  for p in $(lsof -t -iTCP:5054 -sTCP:LISTEN 2>/dev/null || true); do kill "$p" 2>/dev/null || true; done
  sleep 2
  for p in $(lsof -t -iTCP:5054 -sTCP:LISTEN 2>/dev/null || true); do kill -9 "$p" 2>/dev/null || true; done
  sleep 1

  nohup env ASPNETCORE_ENVIRONMENT=Development DatabaseProvider="${DatabaseProvider:-LocalDocker}" \
    "$DOTNET_BIN" exec bin/Debug/net8.0/Api_Vapp.dll --urls "http://127.0.0.1:5054" \
    > "$LOG" 2>&1 &
  echo $! > "$PIDFILE"

  local READY=0
  for _ in $(seq 1 90); do
    local code
    code=$(curl -sS -m 5 -o /dev/null -w "%{http_code}" "$BASE/health" 2>/dev/null || echo 000)
    if [[ "$code" == "200" ]]; then
      READY=1
      break
    fi
    if [[ -f "$PIDFILE" ]] && ! kill -0 "$(cat "$PIDFILE")" 2>/dev/null; then
      echo "API_DIED_EARLY"
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
}

create_system() {
  local SUFFIX DRAFT_ID TEMP_ID DAYS_JSON code
  SUFFIX="$(python3 -c 'import uuid;print(uuid.uuid4().hex[:6])')"
  TEMP_ID="$(python3 -c 'import uuid;print(uuid.uuid4().hex)')"

  code="$(http_json POST /api/BookingSystem/validate-step1 \
    "{\"title\":\"داشبورد تست ${SUFFIX}\",\"activityType\":\"beauty_salon\",\"description\":\"dashboard crawl\",\"saveToPhonebook\":false,\"notebookIds\":[],\"bookingWindowDays\":30}" \
    "$TMP_DIR/step1.json")"
  DRAFT_ID="$(json_get "$TMP_DIR/step1.json" data.draftId)"
  check "wizard step1" "$(http_ok "$code" && [[ -n "$DRAFT_ID" ]] && echo 1 || echo 0)"

  code="$(http_json POST /api/BookingSystem/validate-step2 \
    "{\"draftId\":\"$DRAFT_ID\",\"services\":[{\"serviceTempId\":\"$TEMP_ID\",\"title\":\"خدمت داشبورد\",\"durationMinutes\":30,\"hasCost\":false}]}" \
    "$TMP_DIR/step2.json")"
  check "wizard step2" "$(http_ok "$code" && echo 1 || echo 0)"

  DAYS_JSON="$(python3 - <<'PY'
import json
days=[]
for i in range(7):
  days.append({"dayOfWeek":i,"isOpen":True,"startTimeUtc":"00:00:00","endTimeUtc":"23:59:00"})
print(json.dumps(days))
PY
)"
  code="$(http_json POST /api/BookingSystem/validate-step3 \
    "{\"draftId\":\"$DRAFT_ID\",\"serviceSchedules\":[{\"serviceTempId\":\"$TEMP_ID\",\"weeklyDays\":$DAYS_JSON,\"exceptions\":[]}]}" \
    "$TMP_DIR/step3.json")"
  check "wizard step3" "$(http_ok "$code" && echo 1 || echo 0)"

  code="$(http_json POST /api/BookingSystem/validate-step4 \
    "{\"draftId\":\"$DRAFT_ID\",\"serviceSettings\":[{\"serviceTempId\":\"$TEMP_ID\",\"bufferMinutesBetweenAppointments\":0,\"maxDailyReservations\":50,\"reminderOffsetMinutes\":60}]}" \
    "$TMP_DIR/step4.json")"
  check "wizard step4" "$(http_ok "$code" && echo 1 || echo 0)"

  code="$(http_json POST /api/BookingSystem/confirm "{\"draftId\":\"$DRAFT_ID\"}" "$TMP_DIR/confirm.json")"
  SYSTEM_ID="$(json_get "$TMP_DIR/confirm.json" data.system.id)"
  SLUG="$(json_get "$TMP_DIR/confirm.json" data.system.slug)"
  check "wizard confirm" "$(http_ok "$code" && [[ -n "$SYSTEM_ID" ]] && echo 1 || echo 0)"
  echo "      systemId=$SYSTEM_ID slug=$SLUG"

  code="$(http_json POST "/api/Admin/QuickSendApproval/BookingSystem/$SYSTEM_ID/approve" "" "$TMP_DIR/approve.json")"
  local msg
  msg="$(json_get "$TMP_DIR/approve.json" message)"
  local ok=0
  if http_ok "$code"; then ok=1; fi
  if [[ "$code" == "400" && "$msg" == *"قبلاً بررسی"* ]]; then ok=1; fi
  check "admin approve" "$([[ "$ok" == "1" ]] && echo 1 || echo 0)"

  code="$(http_json GET "/api/BookingSystem/$SYSTEM_ID/services" "" "$TMP_DIR/services.json")"
  SERVICE_ID="$(python3 - "$TMP_DIR/services.json" <<'PY'
import json,sys
d=json.load(open(sys.argv[1],encoding='utf-8'))
items=d.get('data') or []
print(items[0].get('id') if items else '')
PY
)"
  check "GET services" "$(http_ok "$code" && [[ -n "$SERVICE_ID" ]] && echo 1 || echo 0)"
}

first_slot() {
  local date="$1" out="$2"
  local code
  code="$(http_json GET "/api/BookingPublic/$SLUG/services/$SERVICE_ID/slots?date=$date" "" "$out")"
  python3 - "$out" "$code" <<'PY'
import json,sys
code=sys.argv[2]
d=json.load(open(sys.argv[1],encoding='utf-8'))
slots=((d.get('data') or {}).get('slots')) or []
print(code)
print(slots[0]['startUtc'] if slots else '')
PY
}

echo "=== crawl-booking-dashboard-search ==="
echo "BASE=$BASE"
ensure_api
create_system

# --- تاریخ‌های تهران / UTC برای تست تایم ---
eval "$(python3 - <<'PY'
from datetime import datetime, timedelta, timezone
try:
    from zoneinfo import ZoneInfo
    tehran = ZoneInfo("Asia/Tehran")
except Exception:
    tehran = timezone(timedelta(hours=3, minutes=30))

now_utc = datetime.now(timezone.utc)
now_teh = now_utc.astimezone(tehran)
today_teh = now_teh.date()
# اسلات روی تاریخ UTC معادل «دیروز تهران» → نوبت ساعت ~01:00 تهرانِ امروز
utc_yesterday_for_tehran_today = (today_teh - timedelta(days=1)).isoformat()
# نوبت آینده برای آمار وضعیت کلی
future = (now_utc.date() + timedelta(days=5)).isoformat()
print(f'TEHRAN_TODAY="{today_teh.isoformat()}"')
print(f'UTC_YDAY_FOR_TEHRAN_TODAY="{utc_yesterday_for_tehran_today}"')
print(f'FUTURE_DATE="{future}"')
print(f'NOW_UTC="{now_utc.isoformat()}"')
print(f'NOW_TEHRAN="{now_teh.isoformat()}"')
PY
)"
echo "      nowUtc=$NOW_UTC"
echo "      nowTehran=$NOW_TEHRAN"
echo "      tehranToday=$TEHRAN_TODAY"
echo "      edgeUtcDate=$UTC_YDAY_FOR_TEHRAN_TODAY (01:00 Tehran today)"

sqlcmd() {
  local q="$1"
  docker exec vapp_sqlserver_dev /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'Vapp@Secure2025!' -C -d DbVapp -h -1 -W \
    -Q "SET QUOTED_IDENTIFIER ON; SET ANSI_NULLS ON; SET NOCOUNT ON; $q" 2>/dev/null \
    || docker exec vapp_sqlserver_dev /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P 'Vapp@Secure2025!' -C -d DbVapp -h -1 -W \
    -Q "SET QUOTED_IDENTIFIER ON; SET ANSI_NULLS ON; SET NOCOUNT ON; $q" 2>/dev/null
}

# --- ۱) نوبت آینده (خارج از امروز) + رزرو دستی Confirmed روی امروز UTC/تهران ---
FUT_META="$TMP_DIR/fut_slots.txt"
first_slot "$FUTURE_DATE" "$TMP_DIR/fut_slots.json" > "$FUT_META"
FUT_CODE="$(sed -n '1p' "$FUT_META")"
FUT_START="$(sed -n '2p' "$FUT_META")"
check "slots future day → 200" "$([[ "$FUT_CODE" == "200" && -n "$FUT_START" ]] && echo 1 || echo 0)"

CUSTOMER_FUT="علی رضایی FUTURE"
code="$(http_json POST "/api/BookingPublic/$SLUG/book" \
  "{\"serviceId\":$SERVICE_ID,\"startUtc\":\"$FUT_START\",\"customerFullName\":\"$CUSTOMER_FUT\",\"customerMobile\":\"09121110002\"}" \
  "$TMP_DIR/book_fut.json")"
FUT_ID="$(json_get "$TMP_DIR/book_fut.json" data.appointment.id)"
check "book future Pending appointment" "$(http_ok "$code" && [[ -n "$FUT_ID" ]] && echo 1 || echo 0)"

first_slot "$TEHRAN_TODAY" "$TMP_DIR/manual_slots.json" > "$TMP_DIR/manual_meta.txt"
MANUAL_CODE="$(sed -n '1p' "$TMP_DIR/manual_meta.txt")"
MANUAL_START="$(sed -n '2p' "$TMP_DIR/manual_meta.txt")"
# اگر اسلات امروز خالی نبود (نادر)، روز آینده نزدیک را بگیر
if [[ -z "$MANUAL_START" ]]; then
  NEAR="$(python3 - <<'PY'
from datetime import datetime, timedelta, timezone
print((datetime.now(timezone.utc).date()+timedelta(days=1)).isoformat())
PY
)"
  first_slot "$NEAR" "$TMP_DIR/manual_slots.json" > "$TMP_DIR/manual_meta.txt"
  MANUAL_START="$(sed -n '2p' "$TMP_DIR/manual_meta.txt")"
fi
check "slots for manual day → 200" "$([[ -n "$MANUAL_START" ]] && echo 1 || echo 0)"
code="$(http_json POST "/api/BookingSystem/$SYSTEM_ID/appointments/manual" \
  "{\"serviceId\":$SERVICE_ID,\"startUtc\":\"$MANUAL_START\",\"customerFullName\":\"سمیه کریمی MANUAL\",\"customerMobile\":\"09121110003\"}" \
  "$TMP_DIR/book_manual.json")"
MANUAL_ID="$(json_get "$TMP_DIR/book_manual.json" data.id)"
MANUAL_STATUS="$(json_get "$TMP_DIR/book_manual.json" data.status)"
check "manual booking Confirmed" "$(http_ok "$code" && [[ "$MANUAL_STATUS" == "Confirmed" ]] && echo 1 || echo 0)"
echo "      manualId=$MANUAL_ID start=$MANUAL_START"

# baseline داشبورد قبل از نوبت لبه تایم
BASELINE="$TMP_DIR/dash_baseline.json"
http_json GET "/api/BookingSystem/$SYSTEM_ID/dashboard" "" "$BASELINE" >/dev/null
BASE_TODAY="$(json_get "$BASELINE" data.stats.todayTotal)"
BASE_PENDING="$(json_get "$BASELINE" data.stats.pending)"
echo "      baseline todayTotal=$BASE_TODAY pending=$BASE_PENDING"

# --- ۲) نوبت لبه تایم (SQL): 01:00 تهرانِ امروز = 21:30 UTC دیروز ---
# پنجرهٔ عمومی اجازهٔ رزرو «دیروز» را نمی‌دهد؛ برای تست timezone مستقیم در DB می‌گذاریم.
eval "$(python3 - <<'PY'
from datetime import datetime, timedelta, timezone
try:
    from zoneinfo import ZoneInfo
    tehran = ZoneInfo("Asia/Tehran")
except Exception:
    tehran = timezone(timedelta(hours=3, minutes=30))
now_utc = datetime.now(timezone.utc)
today_teh = now_utc.astimezone(tehran).date()
local = datetime(today_teh.year, today_teh.month, today_teh.day, 1, 0, 0, tzinfo=tehran)
start = local.astimezone(timezone.utc)
end = start + timedelta(minutes=30)
print(f'EDGE_START_UTC="{start.strftime("%Y-%m-%dT%H:%M:%SZ")}"')
print(f'EDGE_END_UTC="{end.strftime("%Y-%m-%dT%H:%M:%SZ")}"')
print(f'EDGE_UTC_DATE="{start.date().isoformat()}"')
print(f'EDGE_TEHRAN_DATE="{today_teh.isoformat()}"')
PY
)"
echo "      EDGE startUtc=$EDGE_START_UTC (utcDate=$EDGE_UTC_DATE tehranDate=$EDGE_TEHRAN_DATE)"
check "edge StartUtc is previous UTC calendar day vs Tehran today" "$([[ "$EDGE_UTC_DATE" != "$EDGE_TEHRAN_DATE" && "$EDGE_TEHRAN_DATE" == "$TEHRAN_TODAY" ]] && echo 1 || echo 0)"

CUSTOMER_EDGE="مسعود ابراهیم زاده EDGE"
SQL_OUT="$(sqlcmd "
INSERT INTO BookingAppointments
  (BookingSystemId, BookingServiceItemId, CustomerFullName, CustomerMobile, StartUtc, EndUtc, Status, RemindersEnabled, IsDeleted, CreatedAt)
VALUES
  ($SYSTEM_ID, $SERVICE_ID, N'$CUSTOMER_EDGE', N'09121110001', '$EDGE_START_UTC', '$EDGE_END_UTC', N'Pending', 1, 0, SYSUTCDATETIME());
SELECT CAST(SCOPE_IDENTITY() AS int);
" | tr -d '\r' | awk '/^[0-9]+$/{print; exit}')"
EDGE_ID="$SQL_OUT"
check "SQL insert Tehran-edge Pending appointment" "$([[ -n "$EDGE_ID" ]] && echo 1 || echo 0)"
echo "      edgeAppointmentId=$EDGE_ID"
if [[ -z "$EDGE_ID" ]]; then
  echo "DEBUG last appointment ids: $(sqlcmd "SELECT TOP 3 Id FROM BookingAppointments WHERE BookingSystemId=$SYSTEM_ID ORDER BY Id DESC;" | tr -d '\r' | head -5)"
fi

# --- ۳) داشبورد بدون date → امروز تهران باید edge را ببیند ---
DASH="$TMP_DIR/dash.json"
code="$(http_json GET "/api/BookingSystem/$SYSTEM_ID/dashboard" "" "$DASH")"
TODAY_TOTAL="$(json_get "$DASH" data.stats.todayTotal)"
CONFIRMED="$(json_get "$DASH" data.stats.confirmed)"
PENDING="$(json_get "$DASH" data.stats.pending)"
CANCELLED="$(json_get "$DASH" data.stats.cancelled)"
check "GET dashboard → 200" "$(http_ok "$code" && echo 1 || echo 0)"
echo "      stats todayTotal=$TODAY_TOTAL confirmed=$CONFIRMED pending=$PENDING cancelled=$CANCELLED"

check "Tehran todayTotal increased after edge insert" "$(python3 - <<PY
print('1' if int('${TODAY_TOTAL}' or 0) == int('${BASE_TODAY}' or 0) + 1 else '0')
PY
)"
check "pending is all-time (baseline+1 edge)" "$(python3 - <<PY
print('1' if int('${PENDING}' or 0) == int('${BASE_PENDING}' or 0) + 1 else '0')
PY
)"
check "confirmed is all-time (>=1)" "$(python3 - <<PY
print('1' if int('${CONFIRMED}' or 0) >= 1 else '0')
PY
)"

# اثبات ضد-رگرسیون UTC: اگر day را UTC date لبه (=دیروز) بدهیم، edge دیده می‌شود؛
# اگر فقط day=امروز UTC (=امروز تهران در این ساعت) با منطق قدیمی UTC midnight باشد،
# edge روی 21:30 دیروز UTC از پنجره [امروز 00:00 UTC, فردا) خارج است.
# ما با date=TehranToday (صریح) و بدون date چک می‌کنیم که API لبه را شمرده.
DASH2="$TMP_DIR/dash2.json"
code="$(http_json GET "/api/BookingSystem/$SYSTEM_ID/dashboard?date=$TEHRAN_TODAY" "" "$DASH2")"
TODAY2="$(json_get "$DASH2" data.stats.todayTotal)"
check "dashboard?date=TehranToday → 200" "$(http_ok "$code" && echo 1 || echo 0)"
check "explicit Tehran date todayTotal includes edge" "$(python3 - <<PY
print('1' if int('${TODAY2}' or 0) >= int('${TODAY_TOTAL}' or 0) and int('${TODAY2}' or 0) >= 1 else '0')
PY
)"

# date روی UTC calendar day لبه (دیروز) هم باید حداقل edge را ببیند (همان روز تهران نیست؛ بازه تهرانِ آن date)
DASH_EDGE_DAY="$TMP_DIR/dash_edge_day.json"
code="$(http_json GET "/api/BookingSystem/$SYSTEM_ID/dashboard?date=$EDGE_UTC_DATE" "" "$DASH_EDGE_DAY")"
EDGE_DAY_TOTAL="$(json_get "$DASH_EDGE_DAY" data.stats.todayTotal)"
check "dashboard?date=edgeUtcDate responds 200" "$(http_ok "$code" && echo 1 || echo 0)"
echo "      dashboard(date=$EDGE_UTC_DATE) todayTotal=$EDGE_DAY_TOTAL (Tehran day window for that date)"

# اگر امروز UTC با تهران یکی است، مقایسه با «فردای UTC به‌عنوان date» نباید edge را بشمارد اشتباه — چک اختیاری
# اثبات ضد-رگرسیون: date=دیروز تهران ممکن است ۰ یا مقدار دیگری بدهد؛ مهم این است today با edge ≥۱ باشد

# --- ۵) سرچ: با fromUtc/toUtc ماه اشتباه، باز هم باید پیدا شود ---
FAR_FROM="$(python3 - <<'PY'
from datetime import datetime, timedelta, timezone
print((datetime.now(timezone.utc)-timedelta(days=400)).strftime('%Y-%m-%dT%H:%M:%SZ'))
PY
)"
FAR_TO="$(python3 - <<'PY'
from datetime import datetime, timedelta, timezone
print((datetime.now(timezone.utc)-timedelta(days=370)).strftime('%Y-%m-%dT%H:%M:%SZ'))
PY
)"
SEARCH_Q="$(python3 -c 'import urllib.parse; print(urllib.parse.quote("مسعود ابراهیم زاده"))')"
SEARCH_OUT="$TMP_DIR/search.json"
code="$(http_json GET "/api/BookingSystem/$SYSTEM_ID/appointments?pageNumber=1&pageSize=20&fromUtc=$FAR_FROM&toUtc=$FAR_TO&searchName=$SEARCH_Q" "" "$SEARCH_OUT")"
SEARCH_COUNT="$(json_get "$SEARCH_OUT" data.totalCount)"
SEARCH_NAME="$(python3 - "$SEARCH_OUT" <<'PY'
import json,sys
d=json.load(open(sys.argv[1],encoding='utf-8'))
items=((d.get('data') or {}).get('appointments')) or []
print(items[0].get('customerFullName','') if items else '')
PY
)"
check "search with wrong month range still finds customer" "$(http_ok "$code" && [[ "${SEARCH_COUNT:-0}" -ge 1 ]] && echo 1 || echo 0)"
check "search result name contains مسعود" "$([[ "$SEARCH_NAME" == *"مسعود"* ]] && echo 1 || echo 0)"
echo "      search totalCount=$SEARCH_COUNT name=$SEARCH_NAME"

# سرچ موبایل
MOB_OUT="$TMP_DIR/search_mobile.json"
code="$(http_json GET "/api/BookingSystem/$SYSTEM_ID/appointments?pageNumber=1&pageSize=20&searchName=09121110002" "" "$MOB_OUT")"
MOB_COUNT="$(json_get "$MOB_OUT" data.totalCount)"
check "search by mobile finds future customer" "$(http_ok "$code" && [[ "${MOB_COUNT:-0}" -ge 1 ]] && echo 1 || echo 0)"

# بدون سرچ + بازه غلط → خالی
EMPTY_OUT="$TMP_DIR/empty.json"
code="$(http_json GET "/api/BookingSystem/$SYSTEM_ID/appointments?pageNumber=1&pageSize=20&fromUtc=$FAR_FROM&toUtc=$FAR_TO" "" "$EMPTY_OUT")"
EMPTY_COUNT="$(json_get "$EMPTY_OUT" data.totalCount)"
check "without search, wrong month returns 0" "$(http_ok "$code" && [[ "${EMPTY_COUNT:-1}" == "0" ]] && echo 1 || echo 0)"

echo
echo "=== SUMMARY pass=$PASS fail=$FAIL ==="
if [[ "$FAIL" -gt 0 ]]; then
  echo "DEBUG dashboard body:"
  head -c 800 "$DASH"; echo
  exit 1
fi
exit 0
