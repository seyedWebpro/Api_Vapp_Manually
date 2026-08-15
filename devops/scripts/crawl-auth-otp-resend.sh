#!/usr/bin/env bash
# Crawl صحت ارسال مجدد OTP احراز هویت (login / register / forgot)
#
# Usage:
#   BASE_URL=http://127.0.0.1:5054 \
#   AUTH_PHONE=09920374397 \
#   bash devops/scripts/crawl-auth-otp-resend.sh
#
# Env:
#   SKIP_UNIT=1
#   AUTH_PHONE   — شماره کاربر واقعی برای login/resend (پیش‌فرض 09920374397)
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "$0")/../.." && pwd)"
BASE_URL="${BASE_URL:-http://127.0.0.1:5054}"
AUTH_PHONE="${AUTH_PHONE:-09920374397}"
UNKNOWN_PHONE="${UNKNOWN_PHONE:-09120000000}"
SKIP_UNIT="${SKIP_UNIT:-0}"
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
        # camelCase or PascalCase
        cur=cur.get(p)
        if cur is None:
            alt=p[:1].upper()+p[1:] if p else p
            cur=data if False else None
            # retry on same object with PascalCase
            pass
    else:
        cur=None
        break
# robust getter
cur=data
for p in path:
    if not isinstance(cur,dict):
        cur=None
        break
    if p in cur:
        cur=cur[p]
        continue
    alt=p[:1].upper()+p[1:]
    if alt in cur:
        cur=cur[alt]
        continue
    # also try lower first letter
    low=p[:1].lower()+p[1:]
    cur=cur.get(low)
if isinstance(cur,bool):
    print("true" if cur else "false")
elif cur is None:
    print("")
else:
    print(cur)
PY
}

post_json() {
  local path="$1" body="$2" out="$3"
  curl -sS -m 30 -o "$out" -w '%{http_code}' \
    -X POST "$BASE_URL$path" \
    -H 'Content-Type: application/json' \
    -d "$body" || echo 000
}

echo "=== Auth OTP resend crawl @ $BASE_URL ==="

if [[ "$SKIP_UNIT" != "1" ]]; then
  echo
  echo "--- unit ---"
  if (
    cd "$ROOT_DIR"
    dotnet test Tests/Api_Vapp.Tests.csproj \
      --filter "FullyQualifiedName~AuthOtpResponseFactoryTests" \
      --nologo -v q
  ); then
    check "Unit: AuthOtpResponseFactoryTests" 1
  else
    check "Unit: AuthOtpResponseFactoryTests" 0
  fi
fi

code="$(curl -sS -m 8 -o /dev/null -w '%{http_code}' "$BASE_URL/health" || echo 000)"
if [[ "$code" != "200" && "$code" != "204" ]]; then
  echo "FAIL  API health unreachable ($code)"
  exit 1
fi
check "GET /health → $code" 1

# 1) شماره نامعتبر
INV="$TMP_DIR/inv.json"
http="$(post_json /api/Auth/resend-login-otp '{"phoneNumber":"123"}' "$INV")"
check "resend-login invalid phone → 400" "$([[ "$http" == "400" ]] && echo 1 || echo 0)"
msg="$(json_get "$INV" message)"
check "invalid phone has Persian message" "$([[ -n "$msg" ]] && echo 1 || echo 0)"
echo "      invalid: http=$http message=$msg"

# 2) کاربر ناشناس
NF="$TMP_DIR/nf.json"
http="$(post_json /api/Auth/resend-login-otp "{\"phoneNumber\":\"$UNKNOWN_PHONE\"}" "$NF")"
check "resend-login unknown user → 404" "$([[ "$http" == "404" ]] && echo 1 || echo 0)"
nf_msg="$(json_get "$NF" message)"
nf_code="$(json_get "$NF" errorCode)"
check "unknown user message guides to register" "$([[ "$nf_msg" == *ثبت*نام* ]] && echo 1 || echo 0)"
check "unknown user errorCode=NOT_FOUND" "$([[ "$nf_code" == "NOT_FOUND" ]] && echo 1 || echo 0)"
echo "      notfound: http=$http errorCode=$nf_code message=$nf_msg"

# 3) login موفق
LOGIN="$TMP_DIR/login.json"
http="$(post_json /api/Auth/login "{\"phoneNumber\":\"$AUTH_PHONE\"}" "$LOGIN")"
check "login → 200" "$([[ "$http" == "200" ]] && echo 1 || echo 0)"
login_ok="$(json_get "$LOGIN" success)"
login_msg="$(json_get "$LOGIN" message)"
expires="$(json_get "$LOGIN" expiresInSeconds)"
retry="$(json_get "$LOGIN" retryAfterSeconds)"
check "login success=true" "$([[ "$login_ok" == "true" ]] && echo 1 || echo 0)"
check "login message mentions کد تایید" "$([[ "$login_msg" == *کد*تایید* ]] && echo 1 || echo 0)"
check "login expiresInSeconds=300" "$([[ "$expires" == "300" ]] && echo 1 || echo 0)"
check "login retryAfterSeconds>0 (for mobile timer)" "$(python3 -c "print(1 if int('${retry:-0}' or 0)>0 else 0)")"
echo "      login: http=$http expires=$expires retry=$retry message=$login_msg"

# 4) ارسال مجدد فوری → باید 429 باشد (همان rate limit لاگین)
RESEND="$TMP_DIR/resend.json"
http="$(post_json /api/Auth/resend-login-otp "{\"phoneNumber\":\"$AUTH_PHONE\"}" "$RESEND")"
check "immediate resend-login → 429" "$([[ "$http" == "429" ]] && echo 1 || echo 0)"
r_ok="$(json_get "$RESEND" success)"
r_msg="$(json_get "$RESEND" message)"
r_code="$(json_get "$RESEND" errorCode)"
r_retry="$(json_get "$RESEND" retryAfterSeconds)"
check "rate-limit success=false" "$([[ "$r_ok" == "false" ]] && echo 1 || echo 0)"
check "rate-limit errorCode=OTP_RATE_LIMITED" "$([[ "$r_code" == "OTP_RATE_LIMITED" ]] && echo 1 || echo 0)"
check "rate-limit retryAfterSeconds>0" "$(python3 -c "print(1 if int('${r_retry:-0}' or 0)>0 else 0)")"
check "rate-limit message human-friendly" "$([[ "$r_msg" == *صبر* ]] && echo 1 || echo 0)"
echo "      resend429: http=$http errorCode=$r_code retry=$r_retry message=$r_msg"

# 5) ثبت‌نام بدون session → 404 واضح
REG="$TMP_DIR/reg.json"
http="$(post_json /api/Auth/resend-registration-otp "{\"phoneNumber\":\"$UNKNOWN_PHONE\"}" "$REG")"
check "resend-registration without session → 404" "$([[ "$http" == "404" ]] && echo 1 || echo 0)"
reg_msg="$(json_get "$REG" message)"
reg_code="$(json_get "$REG" errorCode)"
check "resend-registration errorCode=NOT_FOUND" "$([[ "$reg_code" == "NOT_FOUND" ]] && echo 1 || echo 0)"
check "resend-registration message mentions ثبت‌نام" "$([[ "$reg_msg" == *ثبت*نام* ]] && echo 1 || echo 0)"
echo "      reg404: http=$http errorCode=$reg_code message=$reg_msg"

# 6) شکل قرارداد موفقیت (از پاسخ login) برای موبایل
python3 - "$LOGIN" <<'PY' > "$TMP_DIR/contract.txt"
import json,sys
with open(sys.argv[1],encoding="utf-8") as f:
    d=json.load(f)
required=["statusCode","success","message","expiresInSeconds","retryAfterSeconds"]
missing=[k for k in required if k not in d]
print("missing=" + ",".join(missing))
print("ok" if not missing and d.get("success") is True else "bad")
PY
contract="$(tail -n1 "$TMP_DIR/contract.txt")"
check "login response contract for mobile" "$([[ "$contract" == "ok" ]] && echo 1 || echo 0)"
cat "$TMP_DIR/contract.txt" | sed 's/^/      /'

echo
echo "=== RESULT: PASS=$PASS FAIL=$FAIL ==="
[[ "$FAIL" -eq 0 ]]
