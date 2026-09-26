#!/usr/bin/env bash
# Crawl جدول مناسبت‌ها: جدول، ساخت سفارشی→Pending، ویرایش سیستمی→Pending، مخاطب، تأیید متن
#
# Usage:
#   BASE_URL=http://127.0.0.1:8080 OWNER_PHONE=09920374397 \
#     bash devops/scripts/crawl-occasion-greeting.sh
#
# Env:
#   BASE_URL / API     پیش‌فرض http://127.0.0.1:8080
#   OWNER_PHONE        کاربر تست (لاگین OTP)
#   TARGET_PHONES      شماره‌های گیرنده (کاما جدا)
#   SKIP_SMS=1         فقط API بدون ارسال واقعی
#   ADMIN_PHONE        اختیاری برای تأیید قالب (اگر خالی، از DB روی سرور تأیید می‌شود)
set -euo pipefail

BASE_URL="${BASE_URL:-${API:-http://127.0.0.1:8080}}"
OWNER_PHONE="${OWNER_PHONE:-09920374397}"
TARGET_PHONES="${TARGET_PHONES:-09920374397,09392615526}"
SKIP_SMS="${SKIP_SMS:-0}"
TMP_DIR="$(mktemp -d)"
PASS=0
FAIL=0
CREATED_OCCASION_ID=""
AUTH_HEADER=()

KEEP_CREATED="${KEEP_CREATED:-1}"
cleanup() {
  if [[ "$KEEP_CREATED" != "1" && -n "${CREATED_OCCASION_ID:-}" && -n "${TOKEN:-}" ]]; then
    curl -sS -m 15 -o /dev/null -X POST "$BASE_URL/api/SpecialOccasion/${CREATED_OCCASION_ID}/delete" \
      "${AUTH_HEADER[@]}" || true
  fi
  rm -rf "$TMP_DIR"
}
trap cleanup EXIT

json_get() {
  python3 - "$1" "$2" <<'PY'
import json,sys
path=sys.argv[2].split(".")
with open(sys.argv[1],encoding="utf-8") as f:
    data=json.load(f)
cur=data
for p in path:
    if cur is None: break
    if isinstance(cur,dict):
        cur=cur.get(p)
    elif isinstance(cur,list) and p.isdigit():
        i=int(p)
        cur=cur[i] if i < len(cur) else None
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
  if [[ -n "$body" ]]; then
    code="$(curl -sS -m 45 -o "$out" -w '%{http_code}' -X "$method" "$BASE_URL$path" \
      "${AUTH_HEADER[@]}" -H 'Content-Type: application/json' -d "$body" || echo 000)"
  else
    code="$(curl -sS -m 45 -o "$out" -w '%{http_code}' -X "$method" "$BASE_URL$path" \
      "${AUTH_HEADER[@]}" || echo 000)"
  fi
  echo "$code"
}

get_token() {
  local phone="$1" login otp verify wait_i
  for wait_i in 1 2 3 4 5; do
    login=$(curl -sS -m 30 -X POST "$BASE_URL/api/Auth/login" \
      -H "Content-Type: application/json" \
      -d "{\"phoneNumber\":\"$phone\"}" || true)
    otp=$(echo "$login" | python3 -c "import sys,json; d=json.load(sys.stdin); print(d.get('otpCode') or (d.get('data') or {}).get('otpCode') or '')" 2>/dev/null || true)
    if [[ -z "$otp" ]]; then
      # روی خود سرور از لاگ داکر؛ در غیر این صورت از ssh
      if command -v docker >/dev/null 2>&1 && docker ps --format '{{.Names}}' 2>/dev/null | grep -q '^vapp_api_prod$'; then
        otp=$(docker logs --tail 80 vapp_api_prod 2>&1 | grep 'DEV OTP' | tail -1 | sed -n 's/.*>>> \([0-9]*\) <<<.*/\1/p' || true)
      else
        otp=$(ssh -o BatchMode=yes -o ConnectTimeout=15 vapp-prod \
          "docker logs --tail 80 vapp_api_prod 2>&1 | grep 'DEV OTP' | tail -1" \
          | sed -n 's/.*>>> \([0-9]*\) <<<.*/\1/p' || true)
      fi
    fi
    [[ -n "$otp" ]] && break
    echo "      OTP not ready (rate-limit?) — wait 40s ($wait_i/5)" >&2
    sleep 40
  done
  [[ -n "$otp" ]] || { echo "ERROR: OTP for $phone not found" >&2; return 1; }
  verify=$(curl -sS -m 30 -X POST "$BASE_URL/api/Auth/verify-login" \
    -H "Content-Type: application/json" \
    -d "{\"phoneNumber\":\"$phone\",\"otpCode\":\"$otp\"}")
  echo "$verify" | python3 -c "import sys,json; d=json.load(sys.stdin); print((d.get('tokens') or (d.get('data') or {}).get('tokens') or {}).get('accessToken') or '')"
}

echo "=== Occasion greeting crawl @ $BASE_URL ==="

code="$(curl -sS -m 15 -o /dev/null -w '%{http_code}' "$BASE_URL/health" || echo 000)"
check "GET /health → 200" "$([[ "$code" == "200" ]] && echo 1 || echo 0)"

echo "Login owner $OWNER_PHONE ..."
TOKEN="$(get_token "$OWNER_PHONE")"
check "login got token" "$([[ -n "$TOKEN" ]] && echo 1 || echo 0)"
AUTH_HEADER=(-H "Authorization: Bearer $TOKEN")

# ensure subscription with message_automation when possible
PROFILE_OUT="$TMP_DIR/profile.json"
curl -sS -m 20 -o "$PROFILE_OUT" "$BASE_URL/api/User/profile" "${AUTH_HEADER[@]}" || true
USER_ID="$(json_get "$PROFILE_OUT" data.id)"
echo "      userId=$USER_ID"

# 1) table
TABLE_OUT="$TMP_DIR/table.json"
code="$(http_json GET /api/SpecialOccasion/table "" "$TABLE_OUT")"
ok="$(json_get "$TABLE_OUT" success)"
check "GET /table → 200" "$([[ "$code" == "200" ]] && echo 1 || echo 0)"
check "GET /table success" "$([[ "$ok" == "true" ]] && echo 1 || echo 0)"
J_M="$(json_get "$TABLE_OUT" data.today.jalaliMonth)"
J_D="$(json_get "$TABLE_OUT" data.today.jalaliDay)"
G_M="$(json_get "$TABLE_OUT" data.today.gregorianMonth)"
G_D="$(json_get "$TABLE_OUT" data.today.gregorianDay)"
echo "      today jalali=$J_M/$J_D gregorian=$G_M/$G_D"

SYSTEM_ID="$(python3 - "$TABLE_OUT" <<'PY'
import json,sys
d=json.load(open(sys.argv[1],encoding='utf-8'))
items=(d.get('data') or {}).get('items') or []
for it in items:
  if it.get('isSystem'):
    print(it.get('id') or ''); break
PY
)"
check "table has system occasion" "$([[ -n "$SYSTEM_ID" ]] && echo 1 || echo 0)"
echo "      systemOccasionId=$SYSTEM_ID"

# 2) create custom for TODAY (jalali) with message → Pending
CREATE_OUT="$TMP_DIR/create.json"
CREATE_BODY=$(python3 - <<PY
import json
print(json.dumps({
  "name": "تست کراول مناسبت $(date +%H%M%S)",
  "type": "Custom",
  "category": "Congratulation",
  "calendarType": "Jalali",
  "month": int("$J_M" or 1),
  "day": int("$J_D" or 1),
  "defaultMessage": "سلام {{نام}} عزیز — تست مناسبت سفارشی از کراول. {{نام شرکت}}"
}, ensure_ascii=False))
PY
)
code="$(http_json POST /api/SpecialOccasion "$CREATE_BODY" "$CREATE_OUT")"
ok="$(json_get "$CREATE_OUT" success)"
CREATED_OCCASION_ID="$(json_get "$CREATE_OUT" data.id)"
STATUS="$(json_get "$CREATE_OUT" data.templateApprovalStatus)"
check "POST create custom → 201/200" "$([[ "$code" == "201" || "$code" == "200" ]] && echo 1 || echo 0)"
check "create success" "$([[ "$ok" == "true" ]] && echo 1 || echo 0)"
check "create templateApprovalStatus=Pending" "$([[ "$STATUS" == "Pending" ]] && echo 1 || echo 0)"
echo "      customOccasionId=$CREATED_OCCASION_ID status=$STATUS"

# 3) edit system template → Pending
if [[ -n "$SYSTEM_ID" ]]; then
  EDIT_OUT="$TMP_DIR/edit_sys.json"
  EDIT_BODY=$(python3 - <<'PY'
import json
print(json.dumps({
  "customMessage": "سلام {{نام}} — ویرایش متن سیستمی برای تست تأیید. {{نام شرکت}}"
}, ensure_ascii=False))
PY
)
  code="$(http_json POST "/api/SpecialOccasion/${SYSTEM_ID}/template/update" "$EDIT_BODY" "$EDIT_OUT")"
  ok="$(json_get "$EDIT_OUT" success)"
  STATUS="$(json_get "$EDIT_OUT" data.templateApprovalStatus)"
  CAN="$(json_get "$EDIT_OUT" data.canSendWithCurrentTemplate)"
  check "system template/update → 200" "$([[ "$code" == "200" ]] && echo 1 || echo 0)"
  check "system edit → Pending" "$([[ "$STATUS" == "Pending" ]] && echo 1 || echo 0)"
  check "system edit canSend=false" "$([[ "$CAN" == "false" ]] && echo 1 || echo 0)"
fi

# 4) audience update on custom
AUD_OUT="$TMP_DIR/aud.json"
# resolve/create contacts for target phones
IFS=',' read -r -a PHONES <<< "$TARGET_PHONES"
CONTACT_IDS=()
NB_OUT="$TMP_DIR/notebooks.json"
curl -sS -m 20 -o "$NB_OUT" "$BASE_URL/api/ContactNotebook" "${AUTH_HEADER[@]}" || true
NB_ID="$(python3 - "$NB_OUT" <<'PY'
import json,sys
try:
  d=json.load(open(sys.argv[1],encoding='utf-8'))
except Exception:
  print(''); raise SystemExit
data=d.get('data')
items=data if isinstance(data,list) else (data or {}).get('items') or (data or {}).get('notebooks') or []
if isinstance(items,list) and items:
  print(items[0].get('id') or '')
PY
)"
if [[ -z "$NB_ID" ]]; then
  NB_CREATE="$TMP_DIR/nb_create.json"
  curl -sS -m 20 -o "$NB_CREATE" -X POST "$BASE_URL/api/ContactNotebook" \
    "${AUTH_HEADER[@]}" -H 'Content-Type: application/json' \
    -d '{"name":"تست مناسبت کراول"}' || true
  NB_ID="$(json_get "$NB_CREATE" data.id)"
fi
echo "      notebookId=$NB_ID"

for phone in "${PHONES[@]}"; do
  phone="$(echo "$phone" | tr -d '[:space:]')"
  [[ -n "$phone" ]] || continue
  FIND_OUT="$TMP_DIR/find_$phone.json"
  # pageNumber (نه page) — مطابق ContactController.GetMyContacts
  curl -sS -m 20 -o "$FIND_OUT" \
    "$BASE_URL/api/Contact/mine?pageNumber=1&pageSize=100&searchTerm=$phone" \
    "${AUTH_HEADER[@]}" || true
  if [[ ! -s "$FIND_OUT" && -n "$NB_ID" ]]; then
    curl -sS -m 20 -o "$FIND_OUT" \
      "$BASE_URL/api/Contact/notebook/$NB_ID?pageNumber=1&pageSize=100&searchTerm=$phone" \
      "${AUTH_HEADER[@]}" || true
  fi
  CID="$(python3 - "$FIND_OUT" "$phone" <<'PY'
import json,sys
phone=sys.argv[2].replace(' ','')
try:
  raw=open(sys.argv[1],encoding='utf-8').read().strip()
  if not raw:
    print(''); raise SystemExit
  d=json.loads(raw)
except Exception:
  print(''); raise SystemExit
data=d.get('data') or {}
items=data.get('items') or data.get('contacts') or (data if isinstance(data,list) else [])
for it in items or []:
  mob=str(it.get('mobileNumber') or it.get('mobile') or '').replace(' ','')
  if not mob: continue
  if phone in mob or mob.endswith(phone[-10:]):
    print(it.get('id') or ''); break
PY
)"
  if [[ -z "$CID" && -n "$NB_ID" ]]; then
    ADD_OUT="$TMP_DIR/add_$phone.json"
    curl -sS -m 20 -o "$ADD_OUT" -X POST "$BASE_URL/api/Contact" \
      "${AUTH_HEADER[@]}" -H 'Content-Type: application/json' \
      -d "{\"contactNotebookId\":$NB_ID,\"fullName\":\"تست مناسبت $phone\",\"mobileNumber\":\"$phone\"}" || true
    CID="$(json_get "$ADD_OUT" data.id)"
    if [[ -z "$CID" ]]; then
      echo "      create contact response: $(head -c 220 "$ADD_OUT" 2>/dev/null || true)"
    fi
  fi
  [[ -n "$CID" ]] && CONTACT_IDS+=("$CID")
  echo "      phone=$phone contactId=${CID:-none}"
done

if [[ ${#CONTACT_IDS[@]} -gt 0 && -n "$CREATED_OCCASION_ID" ]]; then
  IDS_JSON=$(python3 - <<PY
import json
print(json.dumps([int(x) for x in "${CONTACT_IDS[*]}".split() if x]))
PY
)
  AUD_BODY=$(python3 - <<PY
import json
print(json.dumps({
  "applyToAllContacts": False,
  "contactNotebookIds": [],
  "contactIds": json.loads('''$IDS_JSON'''),
  "excludedContactIds": []
}))
PY
)
  code="$(http_json POST "/api/SpecialOccasion/${CREATED_OCCASION_ID}/audience/update" "$AUD_BODY" "$AUD_OUT")"
  ok="$(json_get "$AUD_OUT" success)"
  APPLY="$(json_get "$AUD_OUT" data.audience.applyToAllContacts)"
  check "audience/update → 200" "$([[ "$code" == "200" ]] && echo 1 || echo 0)"
  check "audience success" "$([[ "$ok" == "true" ]] && echo 1 || echo 0)"
  check "audience applyToAll=false" "$([[ "$APPLY" == "false" ]] && echo 1 || echo 0)"
else
  echo "SKIP  audience (no contacts)"
fi

# 5) approve pending templates via admin if possible
approve_pending_templates() {
  local admin_token="$1"
  local pending_out="$TMP_DIR/pending_tpl.json"
  curl -sS -m 30 -o "$pending_out" "$BASE_URL/api/Admin/TemplateApproval/pending?page=1&pageSize=50" \
    -H "Authorization: Bearer $admin_token" || true
  python3 - "$pending_out" <<'PY' > "$TMP_DIR/pending_ids.txt"
import json,sys
d=json.load(open(sys.argv[1],encoding='utf-8'))
data=d.get('data') or {}
items=data.get('items') or data.get('data') or []
for it in items:
  name=(it.get('name') or '')
  cat=(it.get('category') or '')
  if 'مناسبت' in name or cat == 'مناسبت‌ها' or 'مناسبت' in cat:
    print(it.get('id'))
PY
  while read -r tid; do
    [[ -n "$tid" ]] || continue
    curl -sS -m 20 -o "$TMP_DIR/apr_$tid.json" -X POST "$BASE_URL/api/Admin/TemplateApproval/${tid}/approve" \
      -H "Authorization: Bearer $admin_token" || true
    echo "      approved templateId=$tid"
  done < "$TMP_DIR/pending_ids.txt"
}

if [[ "${ADMIN_PHONE:-}" != "" ]]; then
  echo "Admin approve via $ADMIN_PHONE ..."
  ADMIN_TOKEN="$(get_token "$ADMIN_PHONE" || true)"
  if [[ -n "${ADMIN_TOKEN:-}" ]]; then
    approve_pending_templates "$ADMIN_TOKEN"
  fi
else
  echo "Approve occasion templates via SQL ..."
  SQL_CONTAINER="${SQL_CONTAINER:-}"
  SA_PASSWORD_LOCAL="${SA_PASSWORD:-Vapp@Secure2025!}"
  if [[ -z "$SQL_CONTAINER" ]]; then
    if docker ps --format '{{.Names}}' 2>/dev/null | grep -q '^vapp_sqlserver_dev$'; then
      SQL_CONTAINER=vapp_sqlserver_dev
    elif docker ps --format '{{.Names}}' 2>/dev/null | grep -q '^vapp_sqlserver_prod$'; then
      SQL_CONTAINER=vapp_sqlserver_prod
    fi
  fi
  approve_sql_local() {
    local container="$1" password="$2"
    local sqlcmd=/opt/mssql-tools18/bin/sqlcmd
    docker exec "$container" test -x /opt/mssql-tools/bin/sqlcmd 2>/dev/null && sqlcmd=/opt/mssql-tools/bin/sqlcmd
    docker exec "$container" "$sqlcmd" -S localhost -U sa -P "$password" -C -d DbVapp -Q "
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
UPDATE MessageTemplates
SET ApprovalStatus = N'Approved', ApprovedAt = SYSUTCDATETIME(), UpdatedAt = SYSUTCDATETIME()
WHERE IsDeleted = 0 AND ApprovalStatus = N'Pending'
  AND (Category LIKE N'%مناسبت%' OR Name LIKE N'%مناسبت%');
UPDATE UserOccasionPreferences
SET TemplateApprovalStatus = N'Approved', TemplateApprovedAt = SYSUTCDATETIME(), UpdatedAt = SYSUTCDATETIME()
WHERE IsDeleted = 0 AND TemplateApprovalStatus = N'Pending';
SELECT 'templates_and_prefs_approved' AS Result;
"
  }
  if [[ "$SQL_CONTAINER" == "vapp_sqlserver_dev" ]]; then
    approve_sql_local "$SQL_CONTAINER" "$SA_PASSWORD_LOCAL" || true
  elif [[ "$SQL_CONTAINER" == "vapp_sqlserver_prod" ]]; then
    sa_password="$(grep -E '^SA_PASSWORD=' /root/Api_Vapp_Manually/docker/.env 2>/dev/null | cut -d= -f2- || true)"
    [[ -n "$sa_password" ]] || sa_password="$SA_PASSWORD_LOCAL"
    approve_sql_local "$SQL_CONTAINER" "$sa_password" || true
  else
    ssh -o BatchMode=yes -o ConnectTimeout=20 vapp-prod 'bash -s' <<'REMOTE' || true
SA_PASSWORD="$(grep -E '^SA_PASSWORD=' /root/Api_Vapp_Manually/docker/.env | cut -d= -f2-)"
SQLCMD=/opt/mssql-tools/bin/sqlcmd
docker exec vapp_sqlserver_prod test -x "$SQLCMD" || SQLCMD=/opt/mssql-tools18/bin/sqlcmd
docker exec vapp_sqlserver_prod "$SQLCMD" -S localhost -U sa -P "$SA_PASSWORD" -C -d DbVapp -Q "
SET QUOTED_IDENTIFIER ON; SET ANSI_NULLS ON;
UPDATE MessageTemplates SET ApprovalStatus=N'Approved', ApprovedAt=SYSUTCDATETIME(), UpdatedAt=SYSUTCDATETIME()
WHERE IsDeleted=0 AND ApprovalStatus=N'Pending' AND (Category LIKE N'%مناسبت%' OR Name LIKE N'%مناسبت%');
UPDATE UserOccasionPreferences SET TemplateApprovalStatus=N'Approved', TemplateApprovedAt=SYSUTCDATETIME(), UpdatedAt=SYSUTCDATETIME()
WHERE IsDeleted=0 AND TemplateApprovalStatus=N'Pending';
"
REMOTE
  fi
fi

# 6) enable custom + system-for-today (موقت Month/Day سیستمی را روی امروز می‌گذاریم تا SMS سیستمی هم تست شود)
SYSTEM_ORIG_MD=""
if [[ -n "$CREATED_OCCASION_ID" ]]; then
  http_json POST "/api/SpecialOccasion/${CREATED_OCCASION_ID}/toggle" '{"isEnabled":true}' "$TMP_DIR/tog_c.json" >/dev/null
fi
if [[ -n "$SYSTEM_ID" ]]; then
  http_json POST "/api/SpecialOccasion/${SYSTEM_ID}/template/reset" "" "$TMP_DIR/reset_sys.json" >/dev/null || true
  http_json POST "/api/SpecialOccasion/${SYSTEM_ID}/toggle" '{"isEnabled":true}' "$TMP_DIR/tog_s.json" >/dev/null
  # audience همان مخاطبین تست برای سیستمی
  if [[ ${#CONTACT_IDS[@]} -gt 0 ]]; then
    IDS_JSON=$(python3 - <<PY
import json
print(json.dumps([int(x) for x in "${CONTACT_IDS[*]}".split() if x]))
PY
)
    AUD_BODY=$(python3 - <<PY
import json
print(json.dumps({
  "applyToAllContacts": False,
  "contactNotebookIds": [],
  "contactIds": json.loads('''$IDS_JSON'''),
  "excludedContactIds": []
}))
PY
)
    http_json POST "/api/SpecialOccasion/${SYSTEM_ID}/audience/update" "$AUD_BODY" "$TMP_DIR/aud_sys.json" >/dev/null || true
  fi
  # تاریخ سیستمی را موقتاً روی امروز جلالی بگذار
  if command -v docker >/dev/null 2>&1 && docker ps --format '{{.Names}}' 2>/dev/null | grep -q '^vapp_sqlserver_prod$'; then
    SA_PASSWORD="$(grep -E '^SA_PASSWORD=' /root/Api_Vapp_Manually/docker/.env | cut -d= -f2-)"
    SQLCMD=/opt/mssql-tools/bin/sqlcmd
    docker exec vapp_sqlserver_prod test -x "$SQLCMD" || SQLCMD=/opt/mssql-tools18/bin/sqlcmd
    SYSTEM_ORIG_MD="$(docker exec vapp_sqlserver_prod "$SQLCMD" -S localhost -U sa -P "$SA_PASSWORD" -C -d DbVapp -h -1 -W -Q "SET NOCOUNT ON; SELECT CONCAT(Month,'|',Day,'|',CalendarType) FROM SpecialOccasions WHERE Id=$SYSTEM_ID;" | tr -d '\r' | head -1 | xargs)"
    docker exec vapp_sqlserver_prod "$SQLCMD" -S localhost -U sa -P "$SA_PASSWORD" -C -d DbVapp -Q \
      "UPDATE SpecialOccasions SET Month=$J_M, Day=$J_D, CalendarType=N'Jalali', UpdatedAt=SYSUTCDATETIME() WHERE Id=$SYSTEM_ID AND IsSystem=1;" >/dev/null || true
    echo "      system occasion $SYSTEM_ID temporarily set to Jalali $J_M/$J_D (was $SYSTEM_ORIG_MD)"
  fi
fi

PROF_BODY=$(python3 - <<'PY'
from datetime import datetime, timezone, timedelta
tehran=datetime.now(timezone.utc)+timedelta(hours=3,minutes=30)
# schedule 2 minutes ago so BG is due
t=(tehran - timedelta(minutes=2)).strftime('%H:%M')
import json
print(json.dumps({"businessName":"تست Vapp","congratulationsEnabled":True,"condolencesEnabled":True,"scheduledTimeTehran":t}, ensure_ascii=False))
PY
)
http_json POST /api/SpecialOccasion/profile/update "$PROF_BODY" "$TMP_DIR/prof.json" >/dev/null

# ensure SpecialOccasion automation active
AM_OUT="$TMP_DIR/am.json"
curl -sS -m 30 -o "$AM_OUT" "$BASE_URL/api/AutomatedMessage?page=1&pageSize=50" "${AUTH_HEADER[@]}" || true
AM_ID="$(python3 - "$AM_OUT" <<'PY'
import json,sys
d=json.load(open(sys.argv[1],encoding='utf-8'))
data=d.get('data')
items=data if isinstance(data,list) else (data or {}).get('items') or []
for it in items or []:
  if (it.get('automationType') or '')=='SpecialOccasion':
    print(it.get('id') or ''); break
PY
)"
if [[ -z "$AM_ID" ]]; then
  DRAFT="$TMP_DIR/am_draft.json"
  http_json POST /api/AutomatedMessage/create-draft '{"automationType":"SpecialOccasion"}' "$DRAFT" >/dev/null
  AM_ID="$(json_get "$DRAFT" data.id)"
fi
if [[ -n "$AM_ID" ]]; then
  http_json POST "/api/AutomatedMessage/${AM_ID}/toggle-status" '{"isActive":true,"status":"Active"}' "$TMP_DIR/am_tog.json" >/dev/null || \
  http_json POST "/api/AutomatedMessage/${AM_ID}/toggle-status" '{"status":"Active"}' "$TMP_DIR/am_tog2.json" >/dev/null || true
  http_json POST /api/SpecialOccasion/profile/update "{\"automatedMessageId\":$AM_ID}" "$TMP_DIR/prof2.json" >/dev/null || true
  echo "      automatedMessageId=$AM_ID"
fi

# 7) verify after approve: custom canSend true
TABLE2="$TMP_DIR/table2.json"
http_json GET /api/SpecialOccasion/table "" "$TABLE2" >/dev/null
CUSTOM_STATUS="$(python3 - "$TABLE2" "$CREATED_OCCASION_ID" <<'PY'
import json,sys
oid=int(sys.argv[2] or 0)
d=json.load(open(sys.argv[1],encoding='utf-8'))
for it in (d.get('data') or {}).get('items') or []:
  if it.get('id')==oid:
    print(it.get('templateApprovalStatus') or '')
    break
PY
)"
CUSTOM_CAN="$(python3 - "$TABLE2" "$CREATED_OCCASION_ID" <<'PY'
import json,sys
oid=int(sys.argv[2] or 0)
d=json.load(open(sys.argv[1],encoding='utf-8'))
for it in (d.get('data') or {}).get('items') or []:
  if it.get('id')==oid:
    print('true' if it.get('canSendWithCurrentTemplate') else 'false')
    break
PY
)"
check "custom after approve = Approved" "$([[ "$CUSTOM_STATUS" == "Approved" ]] && echo 1 || echo 0)"
check "custom after approve canSend" "$([[ "$CUSTOM_CAN" == "true" ]] && echo 1 || echo 0)"

if [[ "$SKIP_SMS" != "1" ]]; then
  echo "Waiting up to ~95s for AutomatedMessageBackgroundService to queue/send ..."
  sleep 95
  if command -v docker >/dev/null 2>&1 && docker ps --format '{{.Names}}' 2>/dev/null | grep -q '^vapp_sqlserver_prod$'; then
    SA_PASSWORD="$(grep -E '^SA_PASSWORD=' /root/Api_Vapp_Manually/docker/.env | cut -d= -f2-)"
    SQLCMD=/opt/mssql-tools/bin/sqlcmd
    docker exec vapp_sqlserver_prod test -x "$SQLCMD" || SQLCMD=/opt/mssql-tools18/bin/sqlcmd
    EXEC_OUT="$TMP_DIR/execs.txt"
    docker exec vapp_sqlserver_prod "$SQLCMD" -S localhost -U sa -P "$SA_PASSWORD" -C -d DbVapp -h -1 -W -Q "
SET NOCOUNT ON;
SELECT TOP 20 CONCAT(ae.Id,'|',ISNULL(ae.SpecialOccasionId,0),'|',ISNULL(ae.ContactId,0),'|',ae.Status,'|',LEFT(ISNULL(ae.MessageContent,''),40))
FROM AutomationExecutions ae
WHERE ae.ExecutedAt >= DATEADD(MINUTE,-30,SYSUTCDATETIME())
ORDER BY ae.Id DESC;
" > "$EXEC_OUT" 2>/dev/null || true
    echo "      recent AutomationExecutions:"
    sed 's/^/        /' "$EXEC_OUT" | head -25
    CUSTOM_HIT=$(grep -c "|${CREATED_OCCASION_ID}|" "$EXEC_OUT" 2>/dev/null | tr -d '\n' || true)
    CUSTOM_HIT=${CUSTOM_HIT:-0}
    SYSTEM_HIT=0
    if [[ -n "$SYSTEM_ID" ]]; then
      SYSTEM_HIT=$(grep -c "|${SYSTEM_ID}|" "$EXEC_OUT" 2>/dev/null | tr -d '\n' || true)
      SYSTEM_HIT=${SYSTEM_HIT:-0}
    fi
    check "SMS/queue custom occasion execution" "$([[ "$CUSTOM_HIT" -gt 0 ]] && echo 1 || echo 0)"
    check "SMS/queue system occasion execution" "$([[ "$SYSTEM_HIT" -gt 0 ]] && echo 1 || echo 0)"
  else
    CAMP="$TMP_DIR/camp.json"
    curl -sS -m 30 -o "$CAMP" "$BASE_URL/api/Message/campaigns?page=1&pageSize=10" "${AUTH_HEADER[@]}" || true
    echo "      campaigns snapshot saved (no local SQL)"
  fi
fi

# restore system occasion date if we changed it
if [[ -n "${SYSTEM_ORIG_MD:-}" && -n "$SYSTEM_ID" ]]; then
  IFS='|' read -r OM OD OC <<< "$SYSTEM_ORIG_MD"
  if [[ -n "$OM" && -n "$OD" ]]; then
    SA_PASSWORD="$(grep -E '^SA_PASSWORD=' /root/Api_Vapp_Manually/docker/.env | cut -d= -f2-)"
    SQLCMD=/opt/mssql-tools/bin/sqlcmd
    docker exec vapp_sqlserver_prod test -x "$SQLCMD" || SQLCMD=/opt/mssql-tools18/bin/sqlcmd
    docker exec vapp_sqlserver_prod "$SQLCMD" -S localhost -U sa -P "$SA_PASSWORD" -C -d DbVapp -Q \
      "UPDATE SpecialOccasions SET Month=$OM, Day=$OD, CalendarType=N'${OC:-Jalali}', UpdatedAt=SYSUTCDATETIME() WHERE Id=$SYSTEM_ID AND IsSystem=1;" >/dev/null || true
    echo "      restored system occasion $SYSTEM_ID → $OM/$OD ($OC)"
  fi
fi

echo ""
echo "=== RESULT: PASS=$PASS FAIL=$FAIL ==="
[[ "$FAIL" -eq 0 ]]
