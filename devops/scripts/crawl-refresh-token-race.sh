#!/usr/bin/env bash
# Crawl: concurrent refresh-token race must NOT force logout (all 200, same replacement)
#
# Usage:
#   BASE_URL=http://127.0.0.1:5054 \
#   AUTH_PHONE=09920374397 \
#   bash devops/scripts/crawl-refresh-token-race.sh
#
# Env:
#   SKIP_UNIT=1          — فقط HTTP
#   PARALLEL=8           — تعداد refresh همزمان
#   AUTH_PHONE           — شماره کاربر تست
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "$0")/../.." && pwd)"
BASE_URL="${BASE_URL:-http://127.0.0.1:5054}"
AUTH_PHONE="${AUTH_PHONE:-09920374397}"
SKIP_UNIT="${SKIP_UNIT:-0}"
PARALLEL="${PARALLEL:-8}"
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

echo "=== Refresh-token race crawl @ $BASE_URL ==="

if [[ "$SKIP_UNIT" != "1" ]]; then
  echo
  echo "--- unit ---"
  if (
    cd "$ROOT_DIR"
    dotnet test Tests/Api_Vapp.Tests.csproj \
      --filter "FullyQualifiedName~RefreshTokenRotationTests" \
      --nologo -v q
  ); then
    check "Unit: RefreshTokenRotationTests" 1
  else
    check "Unit: RefreshTokenRotationTests" 0
  fi
fi

code="$(curl -sS -m 8 -o /dev/null -w '%{http_code}' "$BASE_URL/health" || echo 000)"
if [[ "$code" != "200" && "$code" != "204" ]]; then
  echo "FAIL  API health unreachable ($code) — start API first"
  exit 1
fi
check "GET /health → $code" 1

LOGIN_OUT="$TMP_DIR/login.json"
http="$(post_json /api/Auth/login "{\"phoneNumber\":\"$AUTH_PHONE\"}" "$LOGIN_OUT")"
ok="$(json_get "$LOGIN_OUT" success)"
otp="$(json_get "$LOGIN_OUT" otpCode)"
check "POST /api/Auth/login → http=$http success=$ok" "$([[ "$http" == "200" && "$ok" == "true" ]] && echo 1 || echo 0)"

if [[ -z "$otp" ]]; then
  echo "FAIL  login response missing otpCode (need Development OTP in body)"
  echo "      body=$(head -c 400 "$LOGIN_OUT")"
  exit 1
fi
check "login returned DEV otpCode" 1

VERIFY_OUT="$TMP_DIR/verify.json"
http="$(post_json /api/Auth/verify-login "{\"phoneNumber\":\"$AUTH_PHONE\",\"otpCode\":\"$otp\"}" "$VERIFY_OUT")"
ok="$(json_get "$VERIFY_OUT" success)"
RT="$(json_get "$VERIFY_OUT" tokens.refreshToken)"
check "POST /api/Auth/verify-login → http=$http" "$([[ "$http" == "200" && "$ok" == "true" && -n "$RT" ]] && echo 1 || echo 0)"

if [[ -z "$RT" ]]; then
  echo "FAIL  no refreshToken after verify"
  exit 1
fi

echo
echo "--- concurrent refresh x$PARALLEL with SAME refresh token ---"
RACE_DIR="$TMP_DIR/race"
mkdir -p "$RACE_DIR"

for i in $(seq 1 "$PARALLEL"); do
  (
    curl -sS -m 30 -o "$RACE_DIR/r$i.json" -w '%{http_code}' \
      -X POST "$BASE_URL/api/Auth/refresh-token" \
      -H 'Content-Type: application/json' \
      -d "{\"refreshToken\":\"$RT\"}" > "$RACE_DIR/r$i.code" || echo 000 > "$RACE_DIR/r$i.code"
  ) &
done
wait

EVAL="$(python3 - "$RACE_DIR" "$PARALLEL" <<'PY'
import json,sys
d=sys.argv[1]; n=int(sys.argv[2])
codes=[]; tokens=[]; ok_all=True; invalids=0
for i in range(1,n+1):
    code=open(f"{d}/r{i}.code",encoding="utf-8").read().strip()
    codes.append(code)
    data=json.load(open(f"{d}/r{i}.json",encoding="utf-8"))
    ok=bool(data.get("success") if "success" in data else data.get("Success"))
    if code!="200" or not ok:
        ok_all=False
    toks=data.get("tokens") or data.get("Tokens") or {}
    rt=toks.get("refreshToken") or toks.get("RefreshToken") or ""
    tokens.append(rt)
    msg=str(data.get("message") or data.get("Message") or "")
    sc=int(data.get("statusCode") or data.get("StatusCode") or (code if code.isdigit() else 0) or 0)
    if sc==401 or "نامعتبر" in msg:
        invalids+=1
unique=[t for t in set(tokens) if t]
print("1" if ok_all else "0")
print("1" if len(unique)==1 else "0")
print("1" if invalids==0 else "0")
print(unique[0] if unique else "")
print("codes=" + ",".join(codes) + f" unique_rt={len(unique)} invalids={invalids}")
PY
)"

ALL_OK="$(echo "$EVAL" | sed -n '1p')"
SAME_RT="$(echo "$EVAL" | sed -n '2p')"
NO_INVALID="$(echo "$EVAL" | sed -n '3p')"
WINNER_RT="$(echo "$EVAL" | sed -n '4p')"
echo "      $(echo "$EVAL" | sed -n '5p')"

check "all $PARALLEL concurrent refresh → HTTP 200 + success" "$ALL_OK"
check "all concurrent responses share one refresh token" "$SAME_RT"
check "zero Invalid/401 among concurrent refresh" "$NO_INVALID"

FOLLOW="$TMP_DIR/follow.json"
http="$(post_json /api/Auth/refresh-token "{\"refreshToken\":\"$WINNER_RT\"}" "$FOLLOW")"
ok="$(json_get "$FOLLOW" success)"
NEW_RT="$(json_get "$FOLLOW" tokens.refreshToken)"
check "follow-up refresh with winner token → http=$http" "$([[ "$http" == "200" && "$ok" == "true" && -n "$NEW_RT" ]] && echo 1 || echo 0)"

BAD="$TMP_DIR/bad.json"
http="$(post_json /api/Auth/refresh-token '{"refreshToken":"definitely-invalid-token"}' "$BAD")"
check "garbage refresh token still → 401" "$([[ "$http" == "401" ]] && echo 1 || echo 0)"

echo
echo "=== RESULT: PASS=$PASS FAIL=$FAIL ==="
[[ "$FAIL" -eq 0 ]]
