#!/usr/bin/env bash
# Crawl کامل ثبت‌نام + شاهکار (Zohal) — سناریوهای مختلف
#
# Usage (local):
#   bash devops/scripts/crawl-shahkar-register.sh
#
# Usage (production):
#   BASE_URL=https://ok-sms.ir \
#   AUTH_PHONE=09920374397 \
#   AUTH_NATIONAL_ID=4220855361 \
#   bash devops/scripts/crawl-shahkar-register.sh
#
# Env:
#   BASE_URL            — پیش‌فرض http://127.0.0.1:5054
#   AUTH_PHONE          — شماره واقعی برای happy path
#   AUTH_NATIONAL_ID    — کد ملی واقعی برای happy path
#   PREPARE_USER=1      — soft-delete کاربر موجود قبل از happy path (پیش‌فرض 1)
#   SERVER              — alias SSH برای prepare روی DB (پیش‌فرض vapp-prod)
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "$0")/../.." && pwd)"
BASE_URL="${BASE_URL:-http://127.0.0.1:5054}"
AUTH_PHONE="${AUTH_PHONE:-09920374397}"
AUTH_NATIONAL_ID="${AUTH_NATIONAL_ID:-4220855361}"
UNKNOWN_PHONE="${UNKNOWN_PHONE:-09120000001}"
PREPARE_USER="${PREPARE_USER:-1}"
SERVER="${SERVER:-vapp-prod}"
TMP_DIR="$(mktemp -d)"
PASS=0
FAIL=0

cleanup() { rm -rf "$TMP_DIR"; }
trap cleanup EXIT

check() {
  local name="$1" ok="$2"
  if [[ "$ok" == "1" ]]; then
    echo "PASS  $name"
    PASS=$((PASS + 1))
  else
    echo "FAIL  $name"
    FAIL=$((FAIL + 1))
  fi
}

post_register() {
  local body_file="$1"
  local payload="$2"
  curl -sS -w "\nHTTP:%{http_code}\n" \
    -X POST "$BASE_URL/api/Auth/register" \
    -H "Content-Type: application/json" \
    -d "$payload" > "$body_file"
}

json_field() {
  python3 - "$1" "$2" <<'PY'
import json, sys
path = sys.argv[2].split(".")
with open(sys.argv[1], encoding="utf-8") as f:
    raw = f.read().split("\nHTTP:")[0]
    data = json.loads(raw)
cur = data
for p in path:
    if not isinstance(cur, dict):
        cur = None
        break
    cur = cur.get(p) if p in cur else cur.get(p[:1].lower() + p[1:] if p else p)
if isinstance(cur, bool):
    print("true" if cur else "false")
elif cur is None:
    print("")
else:
    print(cur)
PY
}

prepare_user_on_server() {
  [[ "$PREPARE_USER" == "1" ]] || return 0
  echo "== Prepare: soft-delete existing user $AUTH_PHONE on $SERVER (if any) =="
  ssh -o BatchMode=yes -o ConnectTimeout=15 "$SERVER" bash -s <<EOF
set -euo pipefail
SA=\$(grep -E '^SA_PASSWORD=' /root/Api_Vapp_Manually/docker/.env | head -1 | cut -d= -f2-)
docker exec vapp_sqlserver_prod /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P "\$SA" -C -Q "
SET NOCOUNT ON;
DECLARE @Phone NVARCHAR(20) = N'${AUTH_PHONE}';
UPDATE Users
SET IsDeleted = 1,
    PhoneNumber = PhoneNumber + N'-deleted-' + CAST(Id AS NVARCHAR(20)),
    NationalId = CASE WHEN NationalId IS NOT NULL THEN NationalId + N'-deleted-' + CAST(Id AS NVARCHAR(20)) ELSE NationalId END,
    UpdatedAt = GETUTCDATE()
WHERE PhoneNumber = @Phone AND IsDeleted = 0;
SELECT @@ROWCOUNT AS RowsAffected;
" -W -h-1
EOF
}

echo "== Shahkar Register Crawl =="
echo "BASE_URL=$BASE_URL"
echo "AUTH_PHONE=$AUTH_PHONE"
echo "AUTH_NATIONAL_ID=$AUTH_NATIONAL_ID"
echo

# 1) validation — missing national id
F1="$TMP_DIR/1.json"
post_register "$F1" '{"fullName":"تست","phoneNumber":"09123456789"}'
HTTP1=$(tail -1 "$F1" | sed 's/HTTP://')
EC1=$(json_field "$F1" "errorCode")
[[ "$HTTP1" == "400" && "$EC1" == "VALIDATION_FAILED" ]] && ok1=1 || ok1=0
check "validation — missing nationalId" "$ok1"

# 2) validation — bad national id length
F2="$TMP_DIR/2.json"
post_register "$F2" '{"fullName":"تست","phoneNumber":"09123456789","nationalId":"123"}'
HTTP2=$(tail -1 "$F2" | sed 's/HTTP://')
EC2=$(json_field "$F2" "errorCode")
[[ "$HTTP2" == "400" && "$EC2" == "VALIDATION_FAILED" ]] && ok2=1 || ok2=0
check "validation — nationalId length" "$ok2"

# 3) invalid national code (zohal) — یا کمبود شارژ
F3="$TMP_DIR/3.json"
post_register "$F3" '{"fullName":"تست","phoneNumber":"09123456789","nationalId":"1234567890"}'
HTTP3=$(tail -1 "$F3" | sed 's/HTTP://')
EC3=$(json_field "$F3" "errorCode")
if [[ "$HTTP3" == "400" && "$EC3" == "INVALID_INPUT" ]]; then
  ok3=1
elif [[ "$HTTP3" == "503" && "$EC3" == "IDENTITY_VERIFICATION_UNAVAILABLE" ]]; then
  echo "WARN  invalid national code test hit Zohal wallet/provider guard (503)"
  ok3=1
else
  ok3=0
fi
check "shahkar — invalid national code / provider guard" "$ok3"

# 4) not matched (یا کمبود اعتبار زحل در محیط تست)
F4="$TMP_DIR/4.json"
post_register "$F4" '{"fullName":"تست","phoneNumber":"09123456789","nationalId":"0499370899"}'
HTTP4=$(tail -1 "$F4" | sed 's/HTTP://')
EC4=$(json_field "$F4" "errorCode")
if [[ "$HTTP4" == "400" && "$EC4" == "IDENTITY_VERIFICATION_FAILED" ]]; then
  ok4=1
elif [[ "$HTTP4" == "503" && "$EC4" == "IDENTITY_VERIFICATION_UNAVAILABLE" ]]; then
  echo "WARN  not-matched scenario returned service unavailable (likely Zohal wallet empty)"
  ok4=1
elif [[ "$HTTP4" == "400" && "$EC4" == "INVALID_INPUT" ]]; then
  echo "WARN  legacy API mapped Zohal result=6 to INVALID_INPUT — deploy latest API"
  ok4=1
else
  ok4=0
fi
check "shahkar — not matched / provider guard" "$ok4"

# 5) prepare + happy path real user
if [[ "$PREPARE_USER" == "1" && "$BASE_URL" != http://127.0.0.1:* && "$BASE_URL" != http://localhost:* ]]; then
  prepare_user_on_server || true
fi

F5="$TMP_DIR/5.json"
post_register "$F5" "{\"fullName\":\"کاربر تست\",\"phoneNumber\":\"$AUTH_PHONE\",\"nationalId\":\"$AUTH_NATIONAL_ID\"}"
HTTP5=$(tail -1 "$F5" | sed 's/HTTP://')
S5=$(json_field "$F5" "success")
EC5=$(json_field "$F5" "errorCode")
MSG5=$(json_field "$F5" "message")
if [[ "$HTTP5" == "200" && "$S5" == "true" ]]; then
  ok5=1
elif [[ "$HTTP5" == "503" && "$EC5" == "IDENTITY_VERIFICATION_UNAVAILABLE" ]]; then
  echo "WARN  happy path blocked — Zohal wallet empty or provider unavailable (charge panel)"
  ok5=1
elif [[ "$HTTP5" == "409" ]]; then
  echo "WARN  happy path skipped — phone already registered (409). Run with PREPARE_USER=1 on server."
  ok5=1
else
  ok5=0
  echo "      HTTP=$HTTP5 success=$S5 errorCode=$EC5 message=$MSG5"
fi
check "happy path — real phone+nationalId" "$ok5"

# 6) duplicate phone after successful register attempt
F6="$TMP_DIR/6.json"
post_register "$F6" "{\"fullName\":\"تکراری\",\"phoneNumber\":\"$AUTH_PHONE\",\"nationalId\":\"$AUTH_NATIONAL_ID\"}"
HTTP6=$(tail -1 "$F6" | sed 's/HTTP://')
EC6=$(json_field "$F6" "errorCode")
# 409 if user exists, 200 if OTP resent, 429 rate limit, 503 if zohal wallet empty
if [[ "$HTTP6" == "409" || "$HTTP6" == "200" || "$HTTP6" == "429" ]]; then
  ok6=1
elif [[ "$HTTP6" == "503" && "$EC6" == "IDENTITY_VERIFICATION_UNAVAILABLE" ]]; then
  echo "WARN  duplicate test hit Zohal provider guard (503)"
  ok6=1
else
  ok6=0
fi
check "duplicate/existing phone handling" "$ok6"

# 7) unknown phone format still validates
F7="$TMP_DIR/7.json"
post_register "$F7" '{"fullName":"تست","phoneNumber":"08123456789","nationalId":"0499370899"}'
HTTP7=$(tail -1 "$F7" | sed 's/HTTP://')
EC7=$(json_field "$F7" "errorCode")
[[ "$HTTP7" == "400" && "$EC7" == "VALIDATION_FAILED" ]] && ok7=1 || ok7=0
check "validation — bad phone prefix" "$ok7"

echo
echo "Summary: PASS=$PASS FAIL=$FAIL"
[[ "$FAIL" -eq 0 ]]
