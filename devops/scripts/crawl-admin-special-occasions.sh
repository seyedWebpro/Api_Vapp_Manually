#!/usr/bin/env bash
# Crawl مدیریت مناسبت‌های سیستمی ادمین — list/create/update/toggle/delete/validation
#
# Usage:
#   BASE_URL=http://127.0.0.1:5054 bash devops/scripts/crawl-admin-special-occasions.sh
#   SKIP_API_RESTART=1 SKIP_BUILD=1 bash devops/scripts/crawl-admin-special-occasions.sh
set -euo pipefail
export PATH="/usr/bin:/bin:/usr/sbin:/sbin:/usr/local/bin:${PATH:-}"

ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
cd "$ROOT"
BASE="${BASE_URL:-http://127.0.0.1:5054}"
ADMIN_PHONE="${ADMIN_PHONE:-09920374397}"
SKIP_API_RESTART="${SKIP_API_RESTART:-0}"
SKIP_BUILD="${SKIP_BUILD:-0}"
DOTNET="${DOTNET:-/usr/local/share/dotnet/dotnet}"
LOG=/tmp/vapp-admin-special-occasions-crawl.log
PIDFILE=/tmp/vapp-admin-special-occasions-crawl.pid
TMP="$(mktemp -d)"
PASS=0
FAIL=0
SUFFIX="$(date +%s)"
TOKEN=""
CREATED_ID=""
CREATED_CODE="CRAWL_OCC_${SUFFIX}"

cleanup() {
  if [[ -n "${CREATED_ID:-}" ]]; then
    req POST "/api/Admin/SpecialOccasion/${CREATED_ID}/delete" "$TMP/cleanup.json" >/dev/null || true
  fi
  rm -rf "$TMP"
}
trap cleanup EXIT

json_get() {
  python3 - "$1" "$2" <<'PY'
import json,sys
def pick(obj, key):
    if not isinstance(obj, dict): return None
    if key in obj: return obj[key]
    p = key[:1].upper()+key[1:] if key else key
    if p in obj: return obj[p]
    return None
path=sys.argv[2].split(".")
with open(sys.argv[1], encoding="utf-8") as f:
    data=json.load(f)
cur=data
for p in path:
    cur=pick(cur, p)
    if cur is None: break
if isinstance(cur, bool): print("true" if cur else "false")
elif cur is None: print("")
else: print(cur)
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

auth_hdr=()
set_token() {
  TOKEN="$1"
  if [[ -n "$TOKEN" ]]; then
    auth_hdr=(-H "Authorization: Bearer $TOKEN")
  else
    auth_hdr=()
  fi
}

req() {
  local method="$1" path="$2" out="$3"
  shift 3
  local code
  local -a curl_args=(-sS -m 30 -o "$out" -w "%{http_code}" -X "$method" -H "Accept: application/json")
  if [[ ${#auth_hdr[@]} -gt 0 ]]; then
    curl_args+=("${auth_hdr[@]}")
  fi
  if [[ $# -gt 0 ]]; then
    curl_args+=(-H "Content-Type: application/json" "$BASE$path" "$@")
  else
    curl_args+=("$BASE$path")
  fi
  code=$(curl "${curl_args[@]}" || echo 000)
  echo "$code"
}

health_ok() {
  local c
  c=$(curl -sS -m 5 -o /dev/null -w "%{http_code}" "$BASE/health" || echo 000)
  [[ "$c" == "200" ]]
}

ensure_api() {
  if [[ "$SKIP_API_RESTART" == "1" ]]; then
    if ! health_ok; then
      echo "API not healthy at $BASE and SKIP_API_RESTART=1"
      exit 1
    fi
    echo "API_READY (existing)"
    return
  fi

  echo "===== BUILD + RESTART API ====="
  export DOTNET_ROOT="/usr/local/share/dotnet"
  export PATH="/usr/local/share/dotnet:$PATH"
  export ASPNETCORE_ENVIRONMENT=Development
  export DatabaseProvider="${DatabaseProvider:-LocalDocker}"

  if [[ "$SKIP_BUILD" != "1" || ! -f "$ROOT/bin/Debug/net8.0/Api_Vapp.dll" ]]; then
    "$DOTNET" build Api_Vapp.csproj -v q
  fi

  if command -v lsof >/dev/null 2>&1; then
    local pids
    pids=$(lsof -t -iTCP:5054 -sTCP:LISTEN 2>/dev/null || true)
    if [[ -n "$pids" ]]; then
      echo "$pids" | xargs -n1 kill 2>/dev/null || true
      sleep 2
      pids=$(lsof -t -iTCP:5054 -sTCP:LISTEN 2>/dev/null || true)
      if [[ -n "$pids" ]]; then
        echo "$pids" | xargs -n1 kill -9 2>/dev/null || true
        sleep 1
      fi
    fi
  fi

  : >"$LOG"
  "$DOTNET" exec bin/Debug/net8.0/Api_Vapp.dll --urls http://127.0.0.1:5054 >>"$LOG" 2>&1 &
  echo $! >"$PIDFILE"
  echo "API_PID=$(cat "$PIDFILE")"

  local i
  for i in $(seq 1 90); do
    if ! kill -0 "$(cat "$PIDFILE")" 2>/dev/null; then
      echo "API_DIED_EARLY"
      tail -n 80 "$LOG" || true
      exit 1
    fi
    if health_ok; then
      echo "API_READY"
      return
    fi
    sleep 2
  done
  echo "API_NOT_READY"
  tail -n 80 "$LOG" || true
  exit 1
}

admin_login() {
  local login_out="$TMP/admin_login.json" verify_out="$TMP/admin_verify.json"
  local code otp token
  code=$(curl -sS -m 25 -o "$login_out" -w "%{http_code}" -X POST \
    -H "Content-Type: application/json" \
    -d "{\"phoneNumber\":\"$ADMIN_PHONE\"}" \
    "$BASE/api/Auth/admin/login" || echo 000)
  if [[ "$code" != "200" ]]; then
    echo "admin login HTTP=$code body=$(head -c 300 "$login_out")"
    return 1
  fi
  otp=$(json_get "$login_out" otpCode)
  if [[ -z "$otp" && -n "${TEST_OTP:-}" ]]; then
    otp="$TEST_OTP"
  fi
  if [[ -z "$otp" ]]; then
    echo "missing otpCode (Development OTP required)"
    return 1
  fi
  code=$(curl -sS -m 25 -o "$verify_out" -w "%{http_code}" -X POST \
    -H "Content-Type: application/json" \
    -d "{\"phoneNumber\":\"$ADMIN_PHONE\",\"otpCode\":\"$otp\"}" \
    "$BASE/api/Auth/admin/verify-login" || echo 000)
  if [[ "$code" != "200" ]]; then
    echo "admin verify HTTP=$code body=$(head -c 300 "$verify_out")"
    return 1
  fi
  token=$(python3 - "$verify_out" <<'PY'
import json,sys
d=json.load(open(sys.argv[1],encoding='utf-8'))
t=(d.get('tokens') or d.get('Tokens') or {})
print(t.get('accessToken') or t.get('AccessToken') or '')
PY
)
  [[ -n "$token" ]] || return 1
  set_token "$token"
  echo "ADMIN_AUTH_OK"
}

echo "=== crawl-admin-special-occasions ==="
echo "BASE=$BASE SUFFIX=$SUFFIX"
ensure_api
check "GET /health -> 200" "$(health_ok && echo 1 || echo 0)"

# Prefer admin JWT; fall back to DisableAuth anonymous if Admin endpoints allow it.
HTTP=$(req GET "/api/Admin/SpecialOccasion?includeInactive=true" "$TMP/list0.json")
if [[ "$HTTP" != "200" ]]; then
  echo "Admin list without token HTTP=$HTTP — trying admin login..."
  if ! admin_login; then
    echo "FAIL  could not authenticate as admin"
    exit 1
  fi
  HTTP=$(req GET "/api/Admin/SpecialOccasion?includeInactive=true" "$TMP/list0.json")
else
  echo "INFO  Admin list allowed without token (DisableAuth/dev)"
fi

check "GET Admin/SpecialOccasion -> 200" "$([[ "$HTTP" == "200" ]] && echo 1 || echo 0)"
check "list success=true" "$([[ "$(json_get "$TMP/list0.json" success)" == "true" ]] && echo 1 || echo 0)"
LIST_COUNT=$(python3 - "$TMP/list0.json" <<'PY'
import json,sys
d=json.load(open(sys.argv[1],encoding='utf-8'))
print(len(d.get('data') or d.get('Data') or []))
PY
)
check "list has items (seeded catalog)" "$([[ "$LIST_COUNT" -gt 0 ]] && echo 1 || echo 0)"
echo "      list_count=$LIST_COUNT"

# --- validation ---
HTTP=$(req POST "/api/Admin/SpecialOccasion/create" "$TMP/bad_name.json" \
  -d "{\"name\":\"\",\"type\":\"Custom\",\"calendarType\":\"Jalali\",\"month\":1,\"day\":1,\"isActive\":true}")
check "create empty name -> 400" "$([[ "$HTTP" == "400" ]] && echo 1 || echo 0)"

HTTP=$(req POST "/api/Admin/SpecialOccasion/create" "$TMP/bad_month.json" \
  -d "{\"name\":\"بد ماه\",\"type\":\"Custom\",\"calendarType\":\"Jalali\",\"month\":13,\"day\":1,\"isActive\":true}")
check "create month 13 -> 400" "$([[ "$HTTP" == "400" ]] && echo 1 || echo 0)"

HTTP=$(req POST "/api/Admin/SpecialOccasion/create" "$TMP/bad_type.json" \
  -d "{\"name\":\"بد نوع\",\"type\":\"Nope\",\"calendarType\":\"Jalali\",\"month\":1,\"day\":1,\"isActive\":true}")
check "create bad type -> 400" "$([[ "$HTTP" == "400" ]] && echo 1 || echo 0)"

HTTP=$(req POST "/api/Admin/SpecialOccasion/create" "$TMP/bad_hijri.json" \
  -d "{\"name\":\"قمری ممنوع\",\"type\":\"Custom\",\"calendarType\":\"Hijri\",\"month\":7,\"day\":13,\"isActive\":true}")
check "create Hijri -> 400" "$([[ "$HTTP" == "400" ]] && echo 1 || echo 0)"
check "create Hijri persian message" "$(python3 - "$TMP/bad_hijri.json" <<'PY'
import json,sys
d=json.load(open(sys.argv[1],encoding='utf-8'))
msg=(d.get('message') or '')
print(1 if 'شمسی' in msg else 0)
PY
)"

NO_HIJRI=$(python3 - "$TMP/list0.json" <<'PY'
import json,sys
d=json.load(open(sys.argv[1],encoding='utf-8'))
items=d.get('data') or d.get('Data') or []
print(0 if any(str(x.get('calendarType') or x.get('CalendarType') or '').lower()=='hijri' for x in items) else 1)
PY
)
check "catalog has no Hijri rows" "$NO_HIJRI"
FATHER=$(python3 - "$TMP/list0.json" <<'PY'
import json,sys
d=json.load(open(sys.argv[1],encoding='utf-8'))
items=d.get('data') or d.get('Data') or []
row=next((x for x in items if (x.get('code') or x.get('Code'))=='FATHER_DAY'), None)
if not row:
    print(0)
else:
    cal=str(row.get('calendarType') or row.get('CalendarType'))
    print(1 if cal=='Jalali' and int(row.get('month') or row.get('Month'))==10 and int(row.get('day') or row.get('Day'))==2 else 0)
PY
)
check "FATHER_DAY is Jalali 10/2" "$FATHER"

# --- create ---
NAME="کراول مناسبت $SUFFIX"
MSG="سلام {{نام}} عزیز! کراول مناسبت $SUFFIX {{نام شرکت}}"
CREATE_BODY=$(python3 - <<PY
import json
print(json.dumps({
  "name": "$NAME",
  "code": "$CREATED_CODE",
  "type": "Custom",
  "category": "Congratulation",
  "calendarType": "Jalali",
  "month": 7,
  "day": 15,
  "defaultMessage": "$MSG",
  "sortOrder": 7777,
  "isActive": True,
}, ensure_ascii=False))
PY
)
HTTP=$(req POST "/api/Admin/SpecialOccasion/create" "$TMP/create.json" -d "$CREATE_BODY")
check "create -> 200" "$([[ "$HTTP" == "200" ]] && echo 1 || echo 0)"
check "create success" "$([[ "$(json_get "$TMP/create.json" success)" == "true" ]] && echo 1 || echo 0)"
CREATED_ID="$(json_get "$TMP/create.json" data.id)"
check "create id present" "$([[ -n "$CREATED_ID" ]] && echo 1 || echo 0)"
check "create code stored" "$([[ "$(json_get "$TMP/create.json" data.code)" == "$CREATED_CODE" ]] && echo 1 || echo 0)"
check "create isSystem fields" "$([[ "$(json_get "$TMP/create.json" data.month)" == "7" && "$(json_get "$TMP/create.json" data.day)" == "15" ]] && echo 1 || echo 0)"
check "create categoryPersian تبریک" "$([[ "$(json_get "$TMP/create.json" data.categoryPersian)" == "تبریک" ]] && echo 1 || echo 0)"
echo "      created_id=$CREATED_ID"

# duplicate code
HTTP=$(req POST "/api/Admin/SpecialOccasion/create" "$TMP/dup.json" \
  -d "{\"name\":\"تکراری\",\"code\":\"$CREATED_CODE\",\"type\":\"Custom\",\"calendarType\":\"Jalali\",\"month\":1,\"day\":1,\"isActive\":true}")
check "duplicate code -> 400" "$([[ "$HTTP" == "400" ]] && echo 1 || echo 0)"
check "duplicate VALIDATION_FAILED" "$([[ "$(json_get "$TMP/dup.json" errorCode)" == "VALIDATION_FAILED" ]] && echo 1 || echo 0)"

# get by id
HTTP=$(req GET "/api/Admin/SpecialOccasion/${CREATED_ID}" "$TMP/get.json")
check "GET by id -> 200" "$([[ "$HTTP" == "200" ]] && echo 1 || echo 0)"
check "GET name matches" "$([[ "$(json_get "$TMP/get.json" data.name)" == "$NAME" ]] && echo 1 || echo 0)"

HTTP=$(req GET "/api/Admin/SpecialOccasion/999999991" "$TMP/nf.json")
check "GET missing -> 404" "$([[ "$HTTP" == "404" ]] && echo 1 || echo 0)"

# update
EDITED_NAME="کراول ویرایش $SUFFIX"
HTTP=$(req POST "/api/Admin/SpecialOccasion/${CREATED_ID}/update" "$TMP/update.json" \
  -d "{\"name\":\"$EDITED_NAME\",\"type\":\"Holiday\",\"category\":\"Congratulation\",\"calendarType\":\"Jalali\",\"month\":10,\"day\":2,\"defaultMessage\":\"متن ویرایش‌شده $SUFFIX\",\"sortOrder\":8888,\"isActive\":true}")
check "update -> 200" "$([[ "$HTTP" == "200" ]] && echo 1 || echo 0)"
check "update success" "$([[ "$(json_get "$TMP/update.json" success)" == "true" ]] && echo 1 || echo 0)"
check "update name" "$([[ "$(json_get "$TMP/update.json" data.name)" == "$EDITED_NAME" ]] && echo 1 || echo 0)"
check "update calendar stays Jalali" "$([[ "$(json_get "$TMP/update.json" data.calendarType)" == "Jalali" ]] && echo 1 || echo 0)"
check "update day/month" "$([[ "$(json_get "$TMP/update.json" data.month)" == "10" && "$(json_get "$TMP/update.json" data.day)" == "2" ]] && echo 1 || echo 0)"

HTTP=$(req POST "/api/Admin/SpecialOccasion/${CREATED_ID}/update" "$TMP/bad_cal.json" \
  -d "{\"name\":\"$EDITED_NAME\",\"type\":\"Holiday\",\"category\":\"Congratulation\",\"calendarType\":\"Gregorian\",\"month\":12,\"day\":25,\"defaultMessage\":\"متن\",\"sortOrder\":1,\"isActive\":true}")
check "update Gregorian -> 400" "$([[ "$HTTP" == "400" ]] && echo 1 || echo 0)"

# deactivate
HTTP=$(req POST "/api/Admin/SpecialOccasion/${CREATED_ID}/update" "$TMP/off.json" \
  -d "{\"name\":\"$EDITED_NAME\",\"type\":\"Holiday\",\"category\":\"Congratulation\",\"calendarType\":\"Jalali\",\"month\":10,\"day\":2,\"defaultMessage\":\"متن ویرایش‌شده $SUFFIX\",\"sortOrder\":8888,\"isActive\":false}")
check "deactivate -> 200" "$([[ "$HTTP" == "200" ]] && echo 1 || echo 0)"
check "deactivate isActive=false" "$([[ "$(json_get "$TMP/off.json" data.isActive)" == "false" ]] && echo 1 || echo 0)"

HTTP=$(req GET "/api/Admin/SpecialOccasion?includeInactive=false" "$TMP/active_only.json")
check "active-only list -> 200" "$([[ "$HTTP" == "200" ]] && echo 1 || echo 0)"
ACTIVE_HAS=$(python3 - "$TMP/active_only.json" "$CREATED_ID" <<'PY'
import json,sys
d=json.load(open(sys.argv[1],encoding='utf-8'))
want=int(sys.argv[2])
items=d.get('data') or d.get('Data') or []
print(1 if any(int(x.get('id') or x.get('Id') or 0)==want for x in items) else 0)
PY
)
check "inactive hidden when includeInactive=false" "$([[ "$ACTIVE_HAS" == "0" ]] && echo 1 || echo 0)"

HTTP=$(req GET "/api/Admin/SpecialOccasion?includeInactive=true" "$TMP/all.json")
ALL_HAS=$(python3 - "$TMP/all.json" "$CREATED_ID" <<'PY'
import json,sys
d=json.load(open(sys.argv[1],encoding='utf-8'))
want=int(sys.argv[2])
items=d.get('data') or d.get('Data') or []
print(1 if any(int(x.get('id') or x.get('Id') or 0)==want for x in items) else 0)
PY
)
check "inactive visible when includeInactive=true" "$([[ "$ALL_HAS" == "1" ]] && echo 1 || echo 0)"

# reactivate
python3 - "$TMP/on_body.json" "$EDITED_NAME" "$SUFFIX" <<'PY'
import json,sys
open(sys.argv[1],"w",encoding="utf-8").write(json.dumps({
  "name": sys.argv[2],
  "type": "Holiday",
  "category": "Congratulation",
  "calendarType": "Jalali",
  "month": 10,
  "day": 2,
  "defaultMessage": f"متن ویرایش شده {sys.argv[3]}",
  "sortOrder": 8888,
  "isActive": True,
}, ensure_ascii=False))
PY
HTTP=$(req POST "/api/Admin/SpecialOccasion/${CREATED_ID}/update" "$TMP/on.json" \
  -d @"$TMP/on_body.json")
check "reactivate -> 200" "$([[ "$HTTP" == "200" ]] && echo 1 || echo 0)"
check "reactivate isActive=true" "$([[ "$(json_get "$TMP/on.json" data.isActive)" == "true" ]] && echo 1 || echo 0)"

# soft delete
HTTP=$(req POST "/api/Admin/SpecialOccasion/${CREATED_ID}/delete" "$TMP/del.json")
check "delete -> 200" "$([[ "$HTTP" == "200" ]] && echo 1 || echo 0)"
check "delete success" "$([[ "$(json_get "$TMP/del.json" success)" == "true" ]] && echo 1 || echo 0)"
DELETED_ID="$CREATED_ID"
CREATED_ID=""

HTTP=$(req GET "/api/Admin/SpecialOccasion/${DELETED_ID}" "$TMP/gone.json")
check "GET deleted -> 404" "$([[ "$HTTP" == "404" ]] && echo 1 || echo 0)"

HTTP=$(req POST "/api/Admin/SpecialOccasion/${DELETED_ID}/delete" "$TMP/del2.json")
check "delete again -> 404" "$([[ "$HTTP" == "404" ]] && echo 1 || echo 0)"

# recreate same code after soft-delete
HTTP=$(req POST "/api/Admin/SpecialOccasion/create" "$TMP/recreate.json" \
  -d "{\"name\":\"بازتولد $SUFFIX\",\"code\":\"$CREATED_CODE\",\"type\":\"Custom\",\"calendarType\":\"Jalali\",\"month\":1,\"day\":2,\"isActive\":true,\"sortOrder\":10}")
check "recreate same code after delete -> 200" "$([[ "$HTTP" == "200" ]] && echo 1 || echo 0)"
CREATED_ID="$(json_get "$TMP/recreate.json" data.id)"
check "recreate new id" "$([[ -n "$CREATED_ID" && "$CREATED_ID" != "$DELETED_ID" ]] && echo 1 || echo 0)"

# final delete
HTTP=$(req POST "/api/Admin/SpecialOccasion/${CREATED_ID}/delete" "$TMP/final_del.json")
check "final delete -> 200" "$([[ "$HTTP" == "200" ]] && echo 1 || echo 0)"
CREATED_ID=""

echo ""
echo "PASS=$PASS FAIL=$FAIL"
if [[ "$FAIL" -gt 0 ]]; then
  exit 1
fi
exit 0
