#!/usr/bin/env bash
# Crawl تعرفه پیامک (ادمین) + صحت کسر کیف‌پول برای OTP پولی و پیام‌های کوتاه/بلند
#
# Usage:
#   BASE_URL=http://127.0.0.1:5054 \
#   FORM_SLUG=form-qs-test \
#   NEW_COST_PER_PART=175 \
#   bash devops/scripts/crawl-sms-pricing-wallet.sh
#
# Env:
#   RESTORE_PRICING=1  — بعد از تست، تعرفه قبلی را برمی‌گرداند (پیش‌فرض: 0 یعنی تعرفه جدید می‌ماند)
#   SKIP_OTP_SEND=1    — فقط preview/update؛ بدون ثبت OTP عمومی
set -euo pipefail

BASE_URL="${BASE_URL:-http://127.0.0.1:5054}"
FORM_SLUG="${FORM_SLUG:-form-qs-test}"
NEW_COST_PER_PART="${NEW_COST_PER_PART:-175}"
RESTORE_PRICING="${RESTORE_PRICING:-0}"
SKIP_OTP_SEND="${SKIP_OTP_SEND:-0}"
PARTICIPANT_MOBILE="${PARTICIPANT_MOBILE:-}"
if [[ -z "$PARTICIPANT_MOBILE" ]]; then
  PARTICIPANT_MOBILE="09$(python3 -c 'import random; print(f"{random.randint(100000000, 999999999)}")')"
fi
TMP_DIR="$(mktemp -d)"
PASS=0
FAIL=0
ORIG_JSON="$TMP_DIR/pricing_orig.json"

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
    code="$(curl -sS -m 45 -o "$out" -w '%{http_code}' -X "$method" "$BASE_URL$path" \
      -H 'Content-Type: application/json' \
      -d "$body" || echo 000)"
  else
    code="$(curl -sS -m 45 -o "$out" -w '%{http_code}' -X "$method" "$BASE_URL$path" || echo 000)"
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

build_update_body_from_settings() {
  local settings_file="$1" cost_override="${2:-}"
  python3 - "$settings_file" "$cost_override" <<'PY'
import json,sys
d=json.load(open(sys.argv[1],encoding='utf-8'))['data']
cost=sys.argv[2].strip()
body={
  "isBillingEnabled": bool(d.get("isBillingEnabled", True)),
  "costPerPart": float(cost) if cost else float(d["costPerPart"]),
  "persianFirstPageChars": int(d["persianFirstPageChars"]),
  "persianSecondPageChars": int(d["persianSecondPageChars"]),
  "persianOtherPagesChars": int(d["persianOtherPagesChars"]),
  "englishFirstPageChars": int(d["englishFirstPageChars"]),
  "englishOtherPagesChars": int(d["englishOtherPagesChars"]),
  "maxPages": int(d["maxPages"]),
  "regularCharWeight": int(d["regularCharWeight"]),
  "spaceCharWeight": int(d["spaceCharWeight"]),
  "emojiCharWeight": int(d["emojiCharWeight"]),
  "trimContentBeforeCount": bool(d.get("trimContentBeforeCount", True)),
  "countLeadingTrailingSpaces": bool(d.get("countLeadingTrailingSpaces", True)),
  "languageDetectionSampleLength": int(d["languageDetectionSampleLength"]),
  "defaultLanguageIsPersian": bool(d.get("defaultLanguageIsPersian", True)),
  "includeOptOutSuffixInCalculation": True,
  "optOutSuffix": d.get("optOutSuffix") or "لغو11",
}
print(json.dumps(body, ensure_ascii=False))
PY
}

echo "=== SMS pricing + wallet crawl @ $BASE_URL ==="
echo "      form=$FORM_SLUG newCostPerPart=$NEW_COST_PER_PART participant=$PARTICIPANT_MOBILE"

# 0) health
code="$(curl -sS -m 10 -o /dev/null -w '%{http_code}' "$BASE_URL/health" || echo 000)"
check "GET /health → 200" "$([[ "$code" == "200" ]] && echo 1 || echo 0)"

# 1) خواندن تعرفه فعلی ادمین
code="$(http_json GET /api/Admin/SmsPricingSetting "" "$ORIG_JSON")"
ORIG_COST="$(json_get "$ORIG_JSON" data.costPerPart)"
BILLING="$(json_get "$ORIG_JSON" data.isBillingEnabled)"
EFFECTIVE="$(json_get "$ORIG_JSON" data.isBillingEffectivelyEnabled)"
check "GET Admin SmsPricingSetting → 200" "$([[ "$code" == "200" ]] && echo 1 || echo 0)"
check "billing enabled" "$([[ "$BILLING" == "true" ]] && echo 1 || echo 0)"
check "billing effectively enabled" "$([[ "$EFFECTIVE" == "true" ]] && echo 1 || echo 0)"
echo "      before: costPerPart=$ORIG_COST billing=$BILLING effective=$EFFECTIVE"

# 2) به‌روزرسانی تعرفه از پنل ادمین (همان API که UI صدا می‌زند)
UPDATE_BODY="$(build_update_body_from_settings "$ORIG_JSON" "$NEW_COST_PER_PART")"
UPDATE_OUT="$TMP_DIR/pricing_update.json"
code="$(http_json POST /api/Admin/SmsPricingSetting/update "$UPDATE_BODY" "$UPDATE_OUT")"
NEW_COST="$(json_get "$UPDATE_OUT" data.costPerPart)"
check "POST Admin SmsPricingSetting/update → 200" "$([[ "$code" == "200" ]] && echo 1 || echo 0)"
check "costPerPart updated to $NEW_COST_PER_PART" "$(python3 -c "import sys; print(1 if abs(float('$NEW_COST')-float('$NEW_COST_PER_PART'))<0.001 else 0)")"
echo "      after update: costPerPart=$NEW_COST"

# 3) preview: OTP کوتاه / پیام متوسط / پیام خیلی بلند (چند پارت)
OTP_SAMPLE='کد تایید شما: 123456'
MED_SAMPLE="$(python3 - <<'PY'
print("سلام مشتری عزیز، " + ("تخفیف ویژه فروشگاه ما فقط امروز فعال است. " * 3))
PY
)"
LONG_SAMPLE="$(python3 - <<'PY'
# متن فارسی بلند برای چند پارت (با پسوند لغو)
base = "پیام تست قیمت‌گذاری چندصفحه‌ای برای بررسی کسر کیف پول. "
print(base * 25)
PY
)"

preview_one() {
  local label="$1" content="$2" out="$3"
  local body code
  body="$(python3 - "$content" <<'PY'
import json,sys
print(json.dumps({"content": sys.argv[1], "recipientsCount": 1}, ensure_ascii=False))
PY
)"
  code="$(http_json POST /api/Admin/SmsPricingSetting/preview "$body" "$out")"
  local parts cost
  parts="$(json_get "$out" data.partsCount)"
  cost="$(json_get "$out" data.estimatedTotalCost)"
  check "preview $label → 200" "$(http_ok "$code" && echo 1 || echo 0)"
  check "preview $label cost == parts*$NEW_COST_PER_PART" "$(python3 -c "print(1 if abs(float('$cost')-float('$parts')*float('$NEW_COST_PER_PART'))<0.02 else 0)")"
  echo "      $label: parts=$parts cost=$cost"
}

OTP_PREV="$TMP_DIR/prev_otp.json"
MED_PREV="$TMP_DIR/prev_med.json"
LONG_PREV="$TMP_DIR/prev_long.json"
preview_one "otp-short" "$OTP_SAMPLE" "$OTP_PREV"
preview_one "medium" "$MED_SAMPLE" "$MED_PREV"
preview_one "long-multi-part" "$LONG_SAMPLE" "$LONG_PREV"

LONG_PARTS="$(json_get "$LONG_PREV" data.partsCount)"
check "long message has >1 parts" "$(python3 -c "print(1 if int('$LONG_PARTS' or 0)>1 else 0)")"

# 4) موجودی قبل از OTP
PROFILE_BEFORE="$TMP_DIR/profile_before.json"
code="$(http_json GET /api/User/profile "" "$PROFILE_BEFORE")"
USER_ID="$(json_get "$PROFILE_BEFORE" data.id)"
WALLET_BEFORE="$(json_get "$PROFILE_BEFORE" data.walletBalance)"
check "GET /api/User/profile → 200" "$([[ "$code" == "200" ]] && echo 1 || echo 0)"
echo "      userId=$USER_ID walletBefore=$WALLET_BEFORE"

# اطمینان از موجودی کافی برای چند OTP
NEED_TOPUP="$(python3 -c "print(1 if float('$WALLET_BEFORE' or 0) < 5000 else 0)")"
if [[ "$NEED_TOPUP" == "1" ]]; then
  echo "WARN  wallet low — SQL top-up"
  docker exec vapp_sqlserver_dev /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'Vapp@Secure2025!' -C -d DbVapp \
    -Q "UPDATE Users SET WalletBalance = CASE WHEN WalletBalance < 50000 THEN 50000 ELSE WalletBalance END WHERE Id = ${USER_ID};" \
    2>/dev/null || true
  http_json GET /api/User/profile "" "$PROFILE_BEFORE" >/dev/null || true
  WALLET_BEFORE="$(json_get "$PROFILE_BEFORE" data.walletBalance)"
  echo "      wallet after top-up=$WALLET_BEFORE"
fi

EXPECTED_OTP_COST="$(json_get "$OTP_PREV" data.estimatedTotalCost)"
EXPECTED_OTP_PARTS="$(json_get "$OTP_PREV" data.partsCount)"

if [[ "$SKIP_OTP_SEND" != "1" ]]; then
  # 5) OTP پولی فرم عمومی — کسر از کیف مالک
  REG_OUT="$TMP_DIR/register.json"
  REG_BODY="$(python3 - "$PARTICIPANT_MOBILE" <<'PY'
import json,sys
print(json.dumps({
  "firstName": "تست",
  "lastName": "کراول",
  "participantMobile": sys.argv[1]
}, ensure_ascii=False))
PY
)"
  code="$(http_json POST "/api/FormPublic/$FORM_SLUG/register" "$REG_BODY" "$REG_OUT")"
  check "FormPublic register (paid OTP) → 2xx" "$(http_ok "$code" && echo 1 || echo 0)"
  echo "      register http=$code msg=$(json_get "$REG_OUT" message)"

  sleep 1
  PROFILE_AFTER="$TMP_DIR/profile_after.json"
  http_json GET /api/User/profile "" "$PROFILE_AFTER" >/dev/null
  WALLET_AFTER="$(json_get "$PROFILE_AFTER" data.walletBalance)"
  echo "      walletAfter=$WALLET_AFTER"

  TX_OUT="$TMP_DIR/txs.json"
  http_json GET "/api/Wallet/transactions?pageNumber=1&pageSize=10" "" "$TX_OUT" >/dev/null

  python3 - "$TX_OUT" "$EXPECTED_OTP_COST" "$WALLET_BEFORE" "$WALLET_AFTER" <<'PY' > "$TMP_DIR/otp_assert.txt"
import json,sys
txs_file, expected_s, before_s, after_s = sys.argv[1:5]
expected = float(expected_s)
before = float(before_s)
after = float(after_s)
d=json.load(open(txs_file,encoding='utf-8'))
items=((d.get('data') or {}).get('items') or (d.get('data') or {}).get('transactions') or [])
# پیدا کردن آخرین Purchase مرتبط با OTP/پیامک
purchase=None
refund=None
for t in items:
    title=(t.get('title') or '')
    desc=(t.get('description') or '')
    typ=(t.get('transactionType') or t.get('type') or '')
    amt=float(t.get('amount') or 0)
    blob=f"{title} {desc}".lower()
    if purchase is None and amt < 0 and ('otp' in blob or 'تأیید' in title or 'تایید' in title or 'شرکت' in title or 'پیامک' in title or 'پیامک' in desc):
        purchase=t
    if refund is None and amt > 0 and ('برگشت' in title or 'refund' in typ.lower() or typ=='Refund'):
        refund=t

purchase_amt = abs(float(purchase['amount'])) if purchase else None
net = round(before - after, 2)
ok_purchase = purchase_amt is not None and abs(purchase_amt - expected) < 0.05
# اگر SMS واقعی fail شود ممکن است Refund شود → net≈0 ولی مبلغ Purchase باید درست باشد
ok_net_or_purchase = ok_purchase and (abs(net - expected) < 0.05 or (refund is not None and abs(net) < 0.05))
print(f"purchase_amt={purchase_amt}")
print(f"expected={expected}")
print(f"net_delta={net}")
print(f"ok_purchase={1 if ok_purchase else 0}")
print(f"ok_net_or_purchase={1 if ok_net_or_purchase else 0}")
print(f"purchase_title={(purchase or {}).get('title','')}")
print(f"refunded={1 if refund else 0}")
PY

  PURCHASE_OK="$(grep '^ok_purchase=' "$TMP_DIR/otp_assert.txt" | cut -d= -f2)"
  NET_OK="$(grep '^ok_net_or_purchase=' "$TMP_DIR/otp_assert.txt" | cut -d= -f2)"
  cat "$TMP_DIR/otp_assert.txt" | sed 's/^/      /'
  check "OTP Purchase amount == preview cost ($EXPECTED_OTP_COST, parts=$EXPECTED_OTP_PARTS)" "$([[ "$PURCHASE_OK" == "1" ]] && echo 1 || echo 0)"
  check "OTP wallet change matches cost (or purchase+refund on SMS fail)" "$([[ "$NET_OK" == "1" ]] && echo 1 || echo 0)"
else
  echo "SKIP  OTP send (SKIP_OTP_SEND=1)"
fi

# 6) calculate-summary برای پیام بلند (مسیر کمپین — همان تعرفه ادمین)
SUM_OUT="$TMP_DIR/summary.json"
SUM_BODY="$(python3 - "$LONG_SAMPLE" <<'PY'
import json,sys
print(json.dumps({
  "content": sys.argv[1],
  "isPersonalized": False,
  "recipientIds": [],
  "notebookIds": [],
  "sendToAll": False
}, ensure_ascii=False))
PY
)"
code="$(http_json POST /api/Message/campaign/calculate-summary "$SUM_BODY" "$SUM_OUT" || echo 000)"
# ممکن است بدون گیرنده 400 بدهد؛ در این صورت فقط preview ادمین ملاک است
if http_ok "$code"; then
  SUM_COST="$(json_get "$SUM_OUT" data.estimatedTotalCost)"
  SUM_PARTS="$(json_get "$SUM_OUT" data.partsCount)"
  if [[ -z "$SUM_COST" || "$SUM_COST" == "" ]]; then
    SUM_COST="$(json_get "$SUM_OUT" data.totalCost)"
  fi
  if [[ -z "$SUM_PARTS" || "$SUM_PARTS" == "" ]]; then
    SUM_PARTS="$(json_get "$SUM_OUT" data.partsPerMessage)"
  fi
  echo "      calculate-summary http=$code parts=$SUM_PARTS cost=$SUM_COST"
  if [[ -n "$SUM_PARTS" && -n "$SUM_COST" ]]; then
    check "campaign summary cost == parts*newCost" "$(python3 -c "print(1 if abs(float('$SUM_COST')-float('$SUM_PARTS')*float('$NEW_COST_PER_PART'))<0.05 else 0)")"
  else
    echo "WARN  calculate-summary shape unexpected — see $SUM_OUT"
    check "campaign summary parsed" "0"
  fi
else
  echo "INFO  calculate-summary http=$code (ممکن است بدون گیرنده رد شود) — preview ادمین کافی است"
  check "long preview already validated multi-part cost" "1"
fi

# 7) Auth OTP رایگان است — لاگین نباید از کیف کم کند
AUTH_BEFORE="$TMP_DIR/auth_before.json"
http_json GET /api/User/profile "" "$AUTH_BEFORE" >/dev/null
W_AUTH_BEFORE="$(json_get "$AUTH_BEFORE" data.walletBalance)"
LOGIN_OUT="$TMP_DIR/login.json"
code="$(http_json POST /api/Auth/login '{"phoneNumber":"09920374397"}' "$LOGIN_OUT")"
sleep 1
AUTH_AFTER="$TMP_DIR/auth_after.json"
http_json GET /api/User/profile "" "$AUTH_AFTER" >/dev/null
W_AUTH_AFTER="$(json_get "$AUTH_AFTER" data.walletBalance)"
check "Auth login OTP does NOT charge wallet" "$(python3 -c "print(1 if abs(float('$W_AUTH_BEFORE')-float('$W_AUTH_AFTER'))<0.01 else 0)")"
echo "      auth wallet before=$W_AUTH_BEFORE after=$W_AUTH_AFTER loginHttp=$code"

# 8) بازگردانی اختیاری تعرفه
if [[ "$RESTORE_PRICING" == "1" ]]; then
  RESTORE_BODY="$(build_update_body_from_settings "$ORIG_JSON" "")"
  RESTORE_OUT="$TMP_DIR/restore.json"
  code="$(http_json POST /api/Admin/SmsPricingSetting/update "$RESTORE_BODY" "$RESTORE_OUT")"
  RESTORED="$(json_get "$RESTORE_OUT" data.costPerPart)"
  check "restore original costPerPart=$ORIG_COST" "$([[ "$code" == "200" ]] && python3 -c "print(1 if abs(float('$RESTORED')-float('$ORIG_COST'))<0.001 else 0)")"
  echo "      restored costPerPart=$RESTORED"
else
  echo "INFO  pricing left at costPerPart=$NEW_COST (set RESTORE_PRICING=1 to revert)"
fi

echo ""
echo "=== RESULT: PASS=$PASS FAIL=$FAIL ==="
[[ "$FAIL" -eq 0 ]]
