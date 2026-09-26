#!/usr/bin/env bash
# Crawl عمیق سازگاری قوانین تقویم: pagination، IDOR، soft-delete، قالب Pending، audience
# Usage: BASE_URL=http://127.0.0.1:5054 bash devops/scripts/crawl-occasion-compliance.sh
set -euo pipefail

BASE_URL="${BASE_URL:-http://127.0.0.1:5054}"
OWNER_PHONE="${OWNER_PHONE:-09920374397}"
SQL_CONTAINER="${SQL_CONTAINER:-vapp_sqlserver_dev}"
SA_PASSWORD="${SA_PASSWORD:-Vapp@Secure2025!}"
TMP="$(mktemp -d)"
PASS=0
FAIL=0
TOKEN=""
CREATED=""
FOREIGN_ID=""

cleanup() {
  if [[ -n "${CREATED:-}" ]]; then
    if [[ -n "${TOKEN:-}" ]]; then
      curl -sS -m 10 -o /dev/null -X POST "$BASE_URL/api/SpecialOccasion/${CREATED}/delete" \
        -H "Authorization: Bearer $TOKEN" || true
    else
      curl -sS -m 10 -o /dev/null -X POST "$BASE_URL/api/SpecialOccasion/${CREATED}/delete" || true
    fi
  fi
  if [[ -n "${FOREIGN_ID:-}" ]]; then
    sql_q "UPDATE SpecialOccasions SET IsDeleted=1, UpdatedAt=SYSUTCDATETIME() WHERE Id=${FOREIGN_ID};" >/dev/null 2>&1 || true
  fi
  rm -rf "$TMP"
}
trap cleanup EXIT

sql_q() {
  docker exec "$SQL_CONTAINER" /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$SA_PASSWORD" -C -d DbVapp -h -1 -W -Q "SET NOCOUNT ON; SET QUOTED_IDENTIFIER ON; $1" 2>/dev/null | tr -d '\r' | sed '/^$/d' | head -5
}

auth_curl() {
  local method="$1" url="$2" out="$3" body="${4:-}"
  if [[ -n "$TOKEN" ]]; then
    if [[ -n "$body" ]]; then
      curl -sS -m 25 -o "$out" -w '%{http_code}' -X "$method" "$url" \
        -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' -d "$body" || echo 000
    else
      curl -sS -m 25 -o "$out" -w '%{http_code}' -X "$method" "$url" \
        -H "Authorization: Bearer $TOKEN" || echo 000
    fi
  else
    if [[ -n "$body" ]]; then
      curl -sS -m 25 -o "$out" -w '%{http_code}' -X "$method" "$url" \
        -H 'Content-Type: application/json' -d "$body" || echo 000
    else
      curl -sS -m 25 -o "$out" -w '%{http_code}' -X "$method" "$url" || echo 000
    fi
  fi
}

check() {
  if [[ "$2" == "1" || "$2" == "true" ]]; then echo "PASS  $1"; PASS=$((PASS+1))
  else echo "FAIL  $1"; FAIL=$((FAIL+1)); fi
}

json_get() {
  python3 - "$1" "$2" <<'PY'
import json,sys
d=json.load(open(sys.argv[1],encoding='utf-8'))
cur=d
for p in sys.argv[2].split('.'):
  cur=cur.get(p) if isinstance(cur,dict) else None
  if cur is None: break
print('true' if cur is True else ('false' if cur is False else ('' if cur is None else cur)))
PY
}

echo "=== Occasion deep compliance crawl @ $BASE_URL ==="
code=$(curl -sS -m 10 -o /dev/null -w '%{http_code}' "$BASE_URL/health" || echo 000)
check "health 200" "$([[ "$code" == "200" ]] && echo 1 || echo 0)"

code=$(auth_curl GET "$BASE_URL/api/SpecialOccasion/table" "$TMP/table.json")
if [[ "$code" != "200" ]]; then
  login=$(curl -sS -m 20 -X POST "$BASE_URL/api/Auth/login" -H 'Content-Type: application/json' -d "{\"phoneNumber\":\"$OWNER_PHONE\"}" || true)
  otp=$(echo "$login" | python3 -c "import sys,json; d=json.load(sys.stdin); print(d.get('otpCode') or '')" 2>/dev/null || true)
  if [[ -z "$otp" ]] && command -v docker >/dev/null; then
    otp=$(docker logs --tail 40 vapp_api_prod 2>&1 | grep 'DEV OTP' | tail -1 | sed -n 's/.*>>> \([0-9]*\) <<<.*/\1/p' || true)
  fi
  if [[ -n "$otp" ]]; then
    verify=$(curl -sS -m 20 -X POST "$BASE_URL/api/Auth/verify-login" -H 'Content-Type: application/json' -d "{\"phoneNumber\":\"$OWNER_PHONE\",\"otpCode\":\"$otp\"}")
    TOKEN=$(echo "$verify" | python3 -c "import sys,json; d=json.load(sys.stdin); print((d.get('tokens') or {}).get('accessToken') or '')")
  fi
  code=$(auth_curl GET "$BASE_URL/api/SpecialOccasion/table" "$TMP/table.json")
fi

ok=$(json_get "$TMP/table.json" success)
check "GET /table success" "$([[ "$ok" == "true" ]] && echo 1 || echo 0)"
total=$(json_get "$TMP/table.json" data.totalCount)
items_n=$(python3 - "$TMP/table.json" <<'PY'
import json,sys
d=json.load(open(sys.argv[1],encoding='utf-8'))
print(len((d.get('data') or {}).get('items') or []))
PY
)
check "table has items" "$([[ "${items_n:-0}" -gt 0 ]] && echo 1 || echo 0)"
echo "      totalCount=$total itemsReturned=$items_n"

code=$(auth_curl GET "$BASE_URL/api/SpecialOccasion/table?pageNumber=1&pageSize=1" "$TMP/p1.json")
p1n=$(python3 - "$TMP/p1.json" <<'PY'
import json,sys
d=json.load(open(sys.argv[1],encoding='utf-8'))
print(len((d.get('data') or {}).get('items') or []))
PY
)
p1total=$(json_get "$TMP/p1.json" data.totalCount)
check "pagination pageSize=1 returns 1 item" "$([[ "$p1n" == "1" ]] && echo 1 || echo 0)"
check "pagination keeps totalCount" "$([[ -n "$p1total" && "$p1total" != "0" ]] && echo 1 || echo 0)"
check "default table still returns many items" "$([[ "${items_n:-0}" -ge 5 ]] && echo 1 || echo 0)"

code=$(auth_curl GET "$BASE_URL/api/SpecialOccasion/table?pageNumber=1&pageSize=500" "$TMP/p500.json")
p500n=$(python3 - "$TMP/p500.json" <<'PY'
import json,sys
d=json.load(open(sys.argv[1],encoding='utf-8'))
print(len((d.get('data') or {}).get('items') or []))
PY
)
p500ps=$(json_get "$TMP/p500.json" data.pageSize)
check "pageSize=500 clamped (<=100 items)" "$([[ "${p500n:-0}" -le 100 ]] && echo 1 || echo 0)"
check "pageSize response field <=100" "$([[ -z "$p500ps" || "$p500ps" -le 100 ]] && echo 1 || echo 0)"

code=$(auth_curl GET "$BASE_URL/api/SpecialOccasion" "$TMP/list.json")
list_n=$(python3 - "$TMP/list.json" <<'PY'
import json,sys
d=json.load(open(sys.argv[1],encoding='utf-8'))
data=d.get('data')
print(len(data) if isinstance(data,list) else 0)
PY
)
check "GET list default returns catalog" "$([[ "${list_n:-0}" -ge 5 ]] && echo 1 || echo 0)"

body=$(python3 - <<'PY'
import json
print(json.dumps({
  "name":"تست compliance deep","type":"Custom","category":"Congratulation",
  "calendarType":"Jalali","month":7,"day":4,
  "defaultMessage":"متن تست تأیید {{نام}}"
}, ensure_ascii=False))
PY
)
code=$(auth_curl POST "$BASE_URL/api/SpecialOccasion" "$TMP/create.json" "$body")
CREATED=$(json_get "$TMP/create.json" data.id)
status=$(json_get "$TMP/create.json" data.templateApprovalStatus)
msg=$(json_get "$TMP/create.json" message)
check "create HTTP 201/200" "$([[ "$code" == "201" || "$code" == "200" ]] && echo 1 || echo 0)"
check "create Pending" "$([[ "$status" == "Pending" ]] && echo 1 || echo 0)"
check "create pending message mentions admin" "$(echo "$msg" | grep -q 'تأیید ادمین' && echo 1 || echo 0)"

code=$(auth_curl GET "$BASE_URL/api/SpecialOccasion/$CREATED" "$TMP/byid.json")
check "GET own id 200" "$([[ "$code" == "200" ]] && echo 1 || echo 0)"

upd=$(python3 - <<'PY'
import json
print(json.dumps({"defaultMessage":"متن ویرایش‌شده برای تأیید {{نام}}"}, ensure_ascii=False))
PY
)
code=$(auth_curl POST "$BASE_URL/api/SpecialOccasion/$CREATED/update" "$TMP/upd.json" "$upd")
upd_status=$(json_get "$TMP/upd.json" data.templateApprovalStatus)
upd_msg=$(json_get "$TMP/upd.json" message)
check "update DefaultMessage Pending" "$([[ "$upd_status" == "Pending" ]] && echo 1 || echo 0)"
check "update pending message mentions admin" "$(echo "$upd_msg" | grep -q 'تأیید ادمین' && echo 1 || echo 0)"

sys=$(python3 - "$TMP/table.json" <<'PY'
import json,sys
d=json.load(open(sys.argv[1],encoding='utf-8'))
for it in (d.get('data') or {}).get('items') or []:
  if it.get('isSystem'):
    print(it.get('id')); break
PY
)
if [[ -n "$sys" ]]; then
  code=$(auth_curl GET "$BASE_URL/api/SpecialOccasion/$sys" "$TMP/sys.json")
  check "GET system id allowed" "$([[ "$code" == "200" ]] && echo 1 || echo 0)"

  tmpl=$(python3 - <<'PY'
import json
print(json.dumps({"customMessage":"قالب سیستمی ویرایش‌شده برای تست {{نام}}"}, ensure_ascii=False))
PY
  )
  code=$(auth_curl POST "$BASE_URL/api/SpecialOccasion/$sys/template/update" "$TMP/tmpl.json" "$tmpl")
  t_status=$(json_get "$TMP/tmpl.json" data.templateApprovalStatus)
  t_msg=$(json_get "$TMP/tmpl.json" message)
  check "system template update Pending" "$([[ "$t_status" == "Pending" ]] && echo 1 || echo 0)"
  check "system template pending message" "$(echo "$t_msg" | grep -q 'تأیید ادمین' && echo 1 || echo 0)"

  code=$(auth_curl POST "$BASE_URL/api/SpecialOccasion/$sys/delete" "$TMP/sysdel.json")
  sysdel_code=$(json_get "$TMP/sysdel.json" errorCode)
  check "delete system Forbidden" "$([[ "$code" == "403" || "$sysdel_code" == "FORBIDDEN" || "$sysdel_code" == "Forbidden" ]] && echo 1 || echo 0)"
fi

aud_bad='{"applyToAllContacts":false,"contactNotebookIds":[],"contactIds":[],"excludedContactIds":[]}'
code=$(auth_curl POST "$BASE_URL/api/SpecialOccasion/$CREATED/audience/update" "$TMP/aud_bad.json" "$aud_bad")
aud_err=$(json_get "$TMP/aud_bad.json" errorCode)
check "audience empty rejected" "$([[ "$code" == "400" || "$aud_err" == "VALIDATION_FAILED" ]] && echo 1 || echo 0)"

aud_ok='{"applyToAllContacts":true,"contactNotebookIds":[],"contactIds":[],"excludedContactIds":[]}'
code=$(auth_curl POST "$BASE_URL/api/SpecialOccasion/$CREATED/audience/update" "$TMP/aud_ok.json" "$aud_ok")
check "audience all contacts OK" "$([[ "$code" == "200" ]] && echo 1 || echo 0)"

# IDOR: مناسبت سفارشی متعلق به کاربر دیگر (در صورت نیاز کاربر stub می‌سازیم)
OWNER_UID=$(sql_q "SELECT TOP 1 CAST(UserId AS NVARCHAR(20)) FROM SpecialOccasions WHERE Id=${CREATED};" | head -1 | tr -d ' ')
FOREIGN_UID=$(sql_q "SELECT TOP 1 CAST(Id AS NVARCHAR(20)) FROM Users WHERE IsDeleted=0 AND Id<>ISNULL(${OWNER_UID:-0},0) ORDER BY Id;" | head -1 | tr -d ' ')
if [[ -z "$FOREIGN_UID" || ! "$FOREIGN_UID" =~ ^[0-9]+$ ]]; then
  FOREIGN_UID=$(sql_q "
IF NOT EXISTS (SELECT 1 FROM Users WHERE PhoneNumber=N'09000000001')
BEGIN
  INSERT INTO Users (PhoneNumber, PasswordHash, FullName, IsActive, IsPhoneVerified, IsDeleted, CreatedAt, WalletBalance, CanViewNumberSeekerPhones)
  VALUES (N'09000000001', N'x', N'compliance-idor', 1, 1, 0, SYSUTCDATETIME(), 0, 0);
END
SELECT CAST(Id AS NVARCHAR(20)) FROM Users WHERE PhoneNumber=N'09000000001';
" | head -1 | tr -d ' ')
fi
if [[ -n "$OWNER_UID" && "$OWNER_UID" =~ ^[0-9]+$ && -n "$FOREIGN_UID" && "$FOREIGN_UID" =~ ^[0-9]+$ && "$FOREIGN_UID" != "$OWNER_UID" ]]; then
  FOREIGN_ID=$(sql_q "
DECLARE @id INT;
INSERT INTO SpecialOccasions (UserId, Name, Type, Category, CalendarType, Month, Day, OccasionDate, IsSystem, IsActive, IsDeleted, SortOrder, CreatedAt)
VALUES (${FOREIGN_UID}, N'foreign-compliance', N'Custom', N'Congratulation', N'Jalali', 1, 1, '2020-01-01', 0, 1, 0, 9999, SYSUTCDATETIME());
SET @id = SCOPE_IDENTITY();
SELECT CAST(@id AS NVARCHAR(20));
" | head -1 | tr -d ' ')
  if [[ -n "$FOREIGN_ID" && "$FOREIGN_ID" =~ ^[0-9]+$ ]]; then
    code=$(auth_curl GET "$BASE_URL/api/SpecialOccasion/$FOREIGN_ID" "$TMP/idor.json")
    idor_err=$(json_get "$TMP/idor.json" errorCode)
    check "IDOR foreign custom Forbidden/404" "$([[ "$code" == "403" || "$code" == "404" || "$idor_err" == "FORBIDDEN" || "$idor_err" == "Forbidden" || "$idor_err" == "NOT_FOUND" ]] && echo 1 || echo 0)"
    code=$(auth_curl POST "$BASE_URL/api/SpecialOccasion/$FOREIGN_ID/delete" "$TMP/idor_del.json")
    idor_del=$(json_get "$TMP/idor_del.json" errorCode)
    check "IDOR delete foreign Forbidden/404" "$([[ "$code" == "403" || "$code" == "404" || "$idor_del" == "FORBIDDEN" || "$idor_del" == "Forbidden" || "$idor_del" == "NOT_FOUND" ]] && echo 1 || echo 0)"
  else
    check "IDOR seed foreign occasion" "0"
  fi
else
  echo "SKIP  IDOR SQL (could not resolve distinct owner/foreign UserId)"
fi

# soft-delete preference همراه حذف
code=$(auth_curl POST "$BASE_URL/api/SpecialOccasion/$CREATED/delete" "$TMP/del.json")
check "delete own HTTP 200" "$([[ "$code" == "200" ]] && echo 1 || echo 0)"
pref_left=$(sql_q "SELECT CAST(COUNT(1) AS NVARCHAR(20)) FROM UserOccasionPreferences WHERE SpecialOccasionId=${CREATED} AND IsDeleted=0;" | head -1 | tr -d ' ')
check "prefs soft-deleted with occasion" "$([[ "${pref_left:-1}" == "0" ]] && echo 1 || echo 0)"
code=$(auth_curl GET "$BASE_URL/api/SpecialOccasion/$CREATED" "$TMP/gone.json")
gone_err=$(json_get "$TMP/gone.json" errorCode)
check "GET deleted returns NotFound" "$([[ "$code" == "404" || "$gone_err" == "NOT_FOUND" ]] && echo 1 || echo 0)"
CREATED=""  # already deleted

code=$(curl -sS -m 10 -o /dev/null -w '%{http_code}' "$BASE_URL/api/SpecialOccasion/table" -H 'Authorization: Bearer bad.token' || echo 000)
check "bad token handled (401 or DisableAuth 200)" "$([[ "$code" == "401" || "$code" == "200" ]] && echo 1 || echo 0)"

echo "=== RESULT PASS=$PASS FAIL=$FAIL ==="
[[ "$FAIL" -eq 0 ]]
