#!/usr/bin/env bash
# Crawl صحت فرمت پیامک OTP برای autofill (iOS domain-bound + Android hash + لغو11)
#
# Usage:
#   bash devops/scripts/crawl-otp-autofill.sh
#   BASE_URL=http://127.0.0.1:5054 bash devops/scripts/crawl-otp-autofill.sh
#
# Env:
#   SKIP_UNIT=1     — فقط چک زنده API (بدون dotnet test)
#   SKIP_LIVE=1     — فقط unit test
#   ANDROID_HASH=…  — هش نمونه برای assert محلی (پیش‌فرض: AbCdEfGhIjK)
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "$0")/../.." && pwd)"
BASE_URL="${BASE_URL:-http://127.0.0.1:5054}"
SKIP_UNIT="${SKIP_UNIT:-0}"
SKIP_LIVE="${SKIP_LIVE:-0}"
ANDROID_HASH="${ANDROID_HASH:-AbCdEfGhIjK}"
DOMAIN="${OTP_DOMAIN:-ok-sms.ir}"
PASS=0
FAIL=0

check() {
  local name="$1"
  local ok="$2"
  if [[ "$ok" == "1" ]]; then
    echo "PASS  $name"
    PASS=$((PASS + 1))
  else
    echo "FAIL  $name"
    FAIL=$((FAIL + 1))
  fi
}

echo "=== OTP autofill crawl ==="
echo "ROOT=$ROOT_DIR"
echo "BASE_URL=$BASE_URL"

# ---------------------------------------------------------------------------
# 1) Unit tests — منبع حقیقت فرمت پیامک
# ---------------------------------------------------------------------------
if [[ "$SKIP_UNIT" != "1" ]]; then
  echo
  echo "--- dotnet test OtpSmsMessageBuilder ---"
  if (
    cd "$ROOT_DIR"
    dotnet test Tests/Api_Vapp.Tests.csproj \
      --filter "FullyQualifiedName~OtpSmsMessageBuilderTests" \
      --nologo -v q
  ); then
    check "Unit: OtpSmsMessageBuilderTests" 1
  else
    check "Unit: OtpSmsMessageBuilderTests" 0
  fi
else
  echo "SKIP  unit tests (SKIP_UNIT=1)"
fi

# ---------------------------------------------------------------------------
# 2) Assert محلی نمونه متن نهایی (همان builder که API استفاده می‌کند)
# ---------------------------------------------------------------------------
echo
echo "--- local format assert ---"
SAMPLE_OUT="$(mktemp)"
python3 - "$DOMAIN" "$ANDROID_HASH" > "$SAMPLE_OUT" <<'PY'
import sys
domain = sys.argv[1].strip().lower()
hash_ = sys.argv[2].strip()
otp = "1234"
body = f"کد تایید شما: {otp}\n\n@{domain} #{otp}\nلغو11\n{hash_}"
print(body)
# checks
lines = body.split("\n")
assert lines[0] == f"کد تایید شما: {otp}"
assert lines[2] == f"@{domain} #{otp}"
assert lines[-2] == "لغو11"
assert lines[-1] == hash_
assert "لغو11\nلغو11" not in body
print("OK_FORMAT", file=sys.stderr)
PY
check "Local expected OTP SMS format (domain + لغو11 + hash last)" 1
echo "SAMPLE:"
sed 's/^/  | /' "$SAMPLE_OUT"
rm -f "$SAMPLE_OUT"

# ---------------------------------------------------------------------------
# 3) Live: health + sms preview با متن autofill (اگر API بالا باشد)
# ---------------------------------------------------------------------------
if [[ "$SKIP_LIVE" == "1" ]]; then
  echo
  echo "SKIP  live API (SKIP_LIVE=1)"
else
  echo
  echo "--- live API ---"
  code="$(curl -sS -m 8 -o /dev/null -w '%{http_code}' "$BASE_URL/health" || echo 000)"
  if [[ "$code" != "200" && "$code" != "204" ]]; then
    echo "WARN  API not reachable at $BASE_URL (health=$code) — live checks skipped"
  else
    check "GET /health → $code" 1

    PREVIEW_BODY="$(python3 - "$DOMAIN" <<'PY'
import json,sys
domain=sys.argv[1]
msg=f"کد تایید شما: 1234\n\n@{domain} #1234"
print(json.dumps({"content": msg, "recipientsCount": 1}, ensure_ascii=False))
PY
)"
    PREV_OUT="$(mktemp)"
    http="$(curl -sS -m 15 -o "$PREV_OUT" -w '%{http_code}' \
      -X POST "$BASE_URL/api/Admin/SmsPricingSetting/preview" \
      -H 'Content-Type: application/json' \
      -d "$PREVIEW_BODY" || echo 000)"

    if [[ "$http" == "401" || "$http" == "403" ]]; then
      check "POST /api/Admin/SmsPricingSetting/preview reachable (auth: $http)" 1
    elif [[ "$http" =~ ^2 ]]; then
      parts="$(python3 - "$PREV_OUT" <<'PY'
import json,sys
with open(sys.argv[1],encoding="utf-8") as f:
    d=json.load(f)
data=d.get("data") or d
print(data.get("partsCount") or data.get("PartsCount") or "")
PY
)"
      check "Preview OTP autofill message → 2xx (parts=$parts)" 1
      if [[ -n "$parts" ]]; then
        check "OTP autofill preview stays ≤ 2 parts" "$([[ "$parts" -le 2 ]] && echo 1 || echo 0)"
      fi
    else
      check "POST /api/Admin/SmsPricingSetting/preview HTTP $http" 0
      head -c 400 "$PREV_OUT" || true
      echo
    fi
    rm -f "$PREV_OUT"
  fi
fi

echo
echo "=== summary: PASS=$PASS FAIL=$FAIL ==="
if [[ "$FAIL" -gt 0 ]]; then
  exit 1
fi
exit 0
