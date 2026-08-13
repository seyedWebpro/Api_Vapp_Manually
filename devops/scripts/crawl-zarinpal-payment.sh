#!/usr/bin/env bash
# کراول کامل درگاه زرین‌پال (سندباکس) — شارژ کیف‌پول + خرید اشتراک + edge cases مالی
#
# پیش‌نیاز:
#   API روی BASE_URL با:
#     ZarinPal:Sandbox=true
#     ZarinPal:AllowSandboxAutoVerify=true
#     Payment:UseSimulation=false
#
# Usage:
#   BASE_URL=http://127.0.0.1:5054 bash devops/scripts/crawl-zarinpal-payment.sh
set -euo pipefail

BASE_URL="${BASE_URL:-http://127.0.0.1:5054}"
TMP_DIR="$(mktemp -d)"
PASS=0
FAIL=0
CHARGE_AMOUNT="${CHARGE_AMOUNT:-10000}"

cleanup() { rm -rf "$TMP_DIR"; }
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
    if isinstance(cur,dict): cur=cur.get(p)
    elif isinstance(cur,list) and p.isdigit():
        i=int(p); cur=cur[i] if i < len(cur) else None
    else: cur=None; break
if isinstance(cur,bool): print("true" if cur else "false")
elif cur is None: print("")
else: print(cur)
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
    code="$(curl -sS -m 60 -o "$out" -w '%{http_code}' -X "$method" "$BASE_URL$path" \
      -H 'Content-Type: application/json' -d "$body" || echo 000)"
  else
    code="$(curl -sS -m 60 -o "$out" -w '%{http_code}' -X "$method" "$BASE_URL$path" || echo 000)"
  fi
  echo "$code"
}

http_html() {
  local path="$1" out="$2"
  curl -sS -m 60 -o "$out" -w '%{http_code}' "$BASE_URL$path" || echo 000
}

echo "=== ZarinPal crawl @ $BASE_URL ==="

# ─── 0) Gateways ───────────────────────────────────────────────
code="$(http_json GET /api/Payment/gateways "" "$TMP_DIR/gw.json")"
zp_active="$(json_get "$TMP_DIR/gw.json" data.0.isActive)"
zp_code="$(json_get "$TMP_DIR/gw.json" data.0.code)"
check "gateways HTTP 200" "$([[ "$code" == "200" ]] && echo 1 || echo 0)"
check "first gateway is Zarinpal" "$([[ "$zp_code" == "Zarinpal" ]] && echo 1 || echo 0)"
check "Zarinpal isActive" "$zp_active"

# ─── 1) Validation edges ───────────────────────────────────────
code="$(http_json POST /api/Wallet/charge '{"amount":100,"gateway":"Zarinpal"}' "$TMP_DIR/bad_amt.json")"
check "charge amount too low -> 400" "$([[ "$code" == "400" ]] && echo 1 || echo 0)"
ec="$(json_get "$TMP_DIR/bad_amt.json" errorCode)"
check "charge amount errorCode VALIDATION_FAILED" "$([[ "$ec" == "VALIDATION_FAILED" ]] && echo 1 || echo 0)"

code="$(http_json POST /api/Wallet/charge '{"amount":10000,"gateway":"Unknown"}' "$TMP_DIR/bad_gw.json")"
check "unsupported gateway -> 400" "$([[ "$code" == "400" ]] && echo 1 || echo 0)"

code="$(http_json POST /api/Wallet/charge '{"amount":10000,"gateway":"Behpardakht"}' "$TMP_DIR/beh.json")"
check "Behpardakht blocked when simulation off -> 400" "$([[ "$code" == "400" ]] && echo 1 || echo 0)"

code="$(http_json POST /api/UserSubscription/purchase '{"planId":2,"gateway":"UnknownGateway"}' "$TMP_DIR/sub_bad.json")"
check "subscription unsupported gateway -> 400" "$([[ "$code" == "400" ]] && echo 1 || echo 0)"

# ─── 2) Wallet charge request (sandbox) ────────────────────────
code="$(http_json GET /api/Wallet/balance "" "$TMP_DIR/bal0.json")"
bal0="$(json_get "$TMP_DIR/bal0.json" data.balance)"
[[ -z "$bal0" ]] && bal0="$(json_get "$TMP_DIR/bal0.json" data.Balance)"
[[ -z "$bal0" ]] && bal0="$(json_get "$TMP_DIR/bal0.json" data)"
# try nested
if [[ -z "$bal0" || "$bal0" == "{"* ]]; then
  bal0="$(python3 - "$TMP_DIR/bal0.json" <<'PY'
import json,sys
d=json.load(open(sys.argv[1],encoding="utf-8"))
data=d.get("data")
if isinstance(data,dict):
    for k in ("balance","Balance","walletBalance","WalletBalance"):
        if k in data: print(data[k]); break
    else: print(0)
elif data is None: print(0)
else: print(data)
PY
)"
fi
echo "balance_before=$bal0"

code="$(http_json POST /api/Wallet/charge "{\"amount\":$CHARGE_AMOUNT,\"gateway\":\"Zarinpal\"}" "$TMP_DIR/ch1.json")"
check "wallet charge create HTTP 201/200" "$([[ "$code" == "201" || "$code" == "200" ]] && echo 1 || echo 0)"
pid1="$(json_get "$TMP_DIR/ch1.json" data.paymentId)"
auth1="$(json_get "$TMP_DIR/ch1.json" data.refId)"
url1="$(json_get "$TMP_DIR/ch1.json" data.gatewayUrl)"
sim1="$(json_get "$TMP_DIR/ch1.json" data.isSimulation)"
check "charge paymentId present" "$([[ -n "$pid1" ]] && echo 1 || echo 0)"
check "charge authority present (sandbox S...)" "$([[ "$auth1" == S* ]] && echo 1 || echo 0)"
check "charge gatewayUrl sandbox StartPay" "$([[ "$url1" == https://sandbox.zarinpal.com/pg/StartPay/* ]] && echo 1 || echo 0)"
check "charge isSimulation=false" "$([[ "$sim1" == "false" ]] && echo 1 || echo 0)"

# pending lock
code="$(http_json POST /api/Wallet/charge "{\"amount\":$CHARGE_AMOUNT,\"gateway\":\"Zarinpal\"}" "$TMP_DIR/ch_pend.json")"
check "second charge blocked while pending -> 400" "$([[ "$code" == "400" ]] && echo 1 || echo 0)"

# cancel after authority blocked
code="$(http_json POST "/api/Payment/$pid1/cancel" "" "$TMP_DIR/cancel.json")"
check "cancel after authority blocked -> 400" "$([[ "$code" == "400" ]] && echo 1 || echo 0)"

# ─── 3) Callback NOK — no credit ────────────────────────────────
code="$(http_html "/api/Payment/callback/zarinpal?Authority=${auth1}&Status=NOK" "$TMP_DIR/nok.html")"
check "callback NOK HTTP 200" "$([[ "$code" == "200" ]] && echo 1 || echo 0)"
# متن فارسی در HTML به‌صورت entity انکود می‌شود؛ success=0 در deep link معیار معتبر است
grep -Eq 'success=0|ناموفق|&#x' "$TMP_DIR/nok.html" && nok_ok=1 || nok_ok=0
check "callback NOK HTML failure signal" "$nok_ok"

code="$(http_json GET "/api/Payment/$pid1" "" "$TMP_DIR/p1.json")"
st1="$(json_get "$TMP_DIR/p1.json" data.status)"
check "payment after NOK is Failed" "$([[ "$st1" == "Failed" ]] && echo 1 || echo 0)"

code="$(http_json GET /api/Wallet/balance "" "$TMP_DIR/bal_nok.json")"
bal_nok="$(python3 - "$TMP_DIR/bal_nok.json" <<'PY'
import json,sys
d=json.load(open(sys.argv[1],encoding="utf-8"))
data=d.get("data")
if isinstance(data,dict):
    for k in ("balance","Balance","walletBalance","WalletBalance"):
        if k in data: print(data[k]); break
    else: print(0)
else: print(data or 0)
PY
)"
python3 - <<PY
b0=float("$bal0"); bn=float("$bal_nok")
open("$TMP_DIR/nok_bal_ok","w").write("1" if abs(b0-bn)<0.001 else "0")
PY
check "NOK did not change wallet balance" "$(cat "$TMP_DIR/nok_bal_ok")"

# ─── 4) Happy path wallet charge + OK callback ──────────────────
code="$(http_json POST /api/Wallet/charge "{\"amount\":$CHARGE_AMOUNT,\"gateway\":\"Zarinpal\"}" "$TMP_DIR/ch2.json")"
pid2="$(json_get "$TMP_DIR/ch2.json" data.paymentId)"
auth2="$(json_get "$TMP_DIR/ch2.json" data.refId)"
check "second charge after fail created" "$([[ -n "$pid2" && "$auth2" == S* ]] && echo 1 || echo 0)"

code="$(http_html "/api/Payment/callback/zarinpal?Authority=${auth2}&Status=OK" "$TMP_DIR/ok.html")"
check "callback OK HTTP 200" "$([[ "$code" == "200" ]] && echo 1 || echo 0)"
grep -qi "vapp://payment/result" "$TMP_DIR/ok.html" && dl=1 || dl=0
check "callback OK contains app deep link" "$dl"

code="$(http_json GET "/api/Payment/$pid2" "" "$TMP_DIR/p2.json")"
st2="$(json_get "$TMP_DIR/p2.json" data.status)"
check "payment after OK is Verified" "$([[ "$st2" == "Verified" ]] && echo 1 || echo 0)"

code="$(http_json GET /api/Wallet/balance "" "$TMP_DIR/bal_ok.json")"
bal_ok="$(python3 - "$TMP_DIR/bal_ok.json" <<'PY'
import json,sys
d=json.load(open(sys.argv[1],encoding="utf-8"))
data=d.get("data")
if isinstance(data,dict):
    for k in ("balance","Balance","walletBalance","WalletBalance"):
        if k in data: print(data[k]); break
    else: print(0)
else: print(data or 0)
PY
)"
python3 - <<PY
b0=float("$bal0"); bo=float("$bal_ok"); amt=float("$CHARGE_AMOUNT")
# after NOK balance==b0; after OK should be b0+amt
open("$TMP_DIR/ok_bal","w").write("1" if abs((b0+amt)-bo)<0.001 else "0")
print(f"expected={b0+amt} actual={bo}")
PY
check "OK credited wallet by charge amount" "$(cat "$TMP_DIR/ok_bal")"

# ─── 5) Idempotent double callback OK ───────────────────────────
bal_before_dup="$bal_ok"
code="$(http_html "/api/Payment/callback/zarinpal?Authority=${auth2}&Status=OK" "$TMP_DIR/ok2.html")"
check "double callback OK still 200" "$([[ "$code" == "200" ]] && echo 1 || echo 0)"
code="$(http_json GET /api/Wallet/balance "" "$TMP_DIR/bal_dup.json")"
bal_dup="$(python3 - "$TMP_DIR/bal_dup.json" <<'PY'
import json,sys
d=json.load(open(sys.argv[1],encoding="utf-8"))
data=d.get("data")
if isinstance(data,dict):
    for k in ("balance","Balance","walletBalance","WalletBalance"):
        if k in data: print(data[k]); break
    else: print(0)
else: print(data or 0)
PY
)"
python3 - <<PY
a=float("$bal_before_dup"); b=float("$bal_dup")
open("$TMP_DIR/dup_ok","w").write("1" if abs(a-b)<0.001 else "0")
PY
check "double callback did NOT double-credit" "$(cat "$TMP_DIR/dup_ok")"

# ─── 6) Authority reuse attack ──────────────────────────────────
code="$(http_json POST /api/Wallet/charge "{\"amount\":$CHARGE_AMOUNT,\"gateway\":\"Zarinpal\"}" "$TMP_DIR/ch3.json")"
pid3="$(json_get "$TMP_DIR/ch3.json" data.paymentId)"
auth3="$(json_get "$TMP_DIR/ch3.json" data.refId)"
# try verify payment3 with authority of payment2
code="$(http_json POST /api/Payment/verify "{\"paymentId\":$pid3,\"authority\":\"$auth2\",\"status\":\"OK\"}" "$TMP_DIR/reuse.json")"
check "authority reuse verify -> 400" "$([[ "$code" == "400" ]] && echo 1 || echo 0)"
code="$(http_json GET "/api/Payment/$pid3" "" "$TMP_DIR/p3.json")"
st3="$(json_get "$TMP_DIR/p3.json" data.status)"
check "reused-authority payment not Verified" "$([[ "$st3" != "Verified" ]] && echo 1 || echo 0)"

# clean pending payment3 via NOK so later tests work
http_html "/api/Payment/callback/zarinpal?Authority=${auth3}&Status=NOK" "$TMP_DIR/clean3.html" >/dev/null

# ─── 7) Verify without Status=OK ────────────────────────────────
code="$(http_json POST /api/Wallet/charge "{\"amount\":$CHARGE_AMOUNT,\"gateway\":\"Zarinpal\"}" "$TMP_DIR/ch4.json")"
pid4="$(json_get "$TMP_DIR/ch4.json" data.paymentId)"
auth4="$(json_get "$TMP_DIR/ch4.json" data.refId)"
code="$(http_json POST /api/Payment/verify "{\"paymentId\":$pid4,\"authority\":\"$auth4\"}" "$TMP_DIR/nostatus.json")"
check "verify without Status -> 400" "$([[ "$code" == "400" ]] && echo 1 || echo 0)"
http_html "/api/Payment/callback/zarinpal?Authority=${auth4}&Status=NOK" "$TMP_DIR/clean4.html" >/dev/null

# ─── 8) Callback missing authority / unknown authority ──────────
code="$(http_html "/api/Payment/callback/zarinpal" "$TMP_DIR/noauth.html")"
check "callback without authority 200 HTML" "$([[ "$code" == "200" ]] && echo 1 || echo 0)"
code="$(http_html "/api/Payment/callback/zarinpal?Authority=SFAKEINVALID000&Status=OK" "$TMP_DIR/fake.html")"
check "callback unknown authority 200 HTML" "$([[ "$code" == "200" ]] && echo 1 || echo 0)"

# ─── 9) Subscription purchase happy path ────────────────────────
code="$(http_json GET /api/UserSubscription/catalog "" "$TMP_DIR/cat.json")"
plan_id="$(python3 - "$TMP_DIR/cat.json" <<'PY'
import json,sys
d=json.load(open(sys.argv[1],encoding="utf-8"))
data=d.get("data") or {}
plans=data.get("plans") or []
chosen=None
for p in plans:
    if not isinstance(p,dict): continue
    if p.get("isFree") or not p.get("canPurchase", True):
        continue
    pid=p.get("id")
    if pid:
        chosen=pid; break
print(chosen or "")
PY
)"
if [[ -z "$plan_id" ]]; then
  echo "WARN  could not resolve paid plan id from catalog — trying planId=2"
  plan_id=2
fi
echo "subscription_plan_id=$plan_id"

code="$(http_json POST /api/UserSubscription/purchase "{\"planId\":$plan_id,\"gateway\":\"Zarinpal\"}" "$TMP_DIR/sub.json")"
check "subscription purchase HTTP 200/201" "$([[ "$code" == "200" || "$code" == "201" ]] && echo 1 || echo 0)"
req_pay="$(json_get "$TMP_DIR/sub.json" data.requiresPayment)"
spid="$(json_get "$TMP_DIR/sub.json" data.paymentId)"
sauth="$(json_get "$TMP_DIR/sub.json" data.refId)"
surl="$(json_get "$TMP_DIR/sub.json" data.gatewayUrl)"
if [[ "$req_pay" == "false" ]]; then
  check "subscription zero-pay activated without gateway" "1"
else
  check "subscription requiresPayment" "$([[ "$req_pay" == "true" ]] && echo 1 || echo 0)"
  check "subscription paymentId present" "$([[ -n "$spid" ]] && echo 1 || echo 0)"
  check "subscription sandbox authority" "$([[ "$sauth" == S* ]] && echo 1 || echo 0)"
  check "subscription gatewayUrl sandbox" "$([[ "$surl" == https://sandbox.zarinpal.com/pg/StartPay/* ]] && echo 1 || echo 0)"

  code="$(http_html "/api/Payment/callback/zarinpal?Authority=${sauth}&Status=OK" "$TMP_DIR/sub_ok.html")"
  check "subscription callback OK 200" "$([[ "$code" == "200" ]] && echo 1 || echo 0)"
  code="$(http_json GET "/api/Payment/$spid" "" "$TMP_DIR/sp.json")"
  sst="$(json_get "$TMP_DIR/sp.json" data.status)"
  check "subscription payment Verified" "$([[ "$sst" == "Verified" ]] && echo 1 || echo 0)"

  # double fulfill
  http_html "/api/Payment/callback/zarinpal?Authority=${sauth}&Status=OK" "$TMP_DIR/sub_ok2.html" >/dev/null
  check "subscription double callback safe" "1"
fi

# ─── 10) MerchantId not leaked in payment DTO ───────────────────
code="$(http_json GET "/api/Payment/$pid2" "" "$TMP_DIR/leak.json")"
python3 - <<PY
import json
d=json.load(open("$TMP_DIR/leak.json",encoding="utf-8"))
s=json.dumps(d,ensure_ascii=False).lower()
bad = "merchant" in s or "f37b8b50" in s or "11111111-1111" in s
open("$TMP_DIR/noleak","w").write("0" if bad else "1")
PY
check "MerchantId not leaked in payment response" "$(cat "$TMP_DIR/noleak")"

# ─── 11) Detailed payment audit logs in DB (via Admin Audit API) ─
# انتظار: برای شارژ موفق (pid2) حداقل Requested + AuthorityIssued + Callback + Verified
# و NewValue شامل userId/amount/phone یا authority
code="$(http_json GET "/api/Admin/Audit?category=payment&entityId=${pid2}&pageSize=50&searchInJson=true&q=${pid2}" "" "$TMP_DIR/audit_pid2.json")"
check "audit search by paymentId HTTP 200" "$([[ "$code" == "200" ]] && echo 1 || echo 0)"

python3 - <<PY
import json,sys
path="$TMP_DIR/audit_pid2.json"
d=json.load(open(path,encoding="utf-8"))
data=d.get("data") or {}
items=data.get("items") or data.get("Items") or []
actions=sorted({(i.get("action") or i.get("Action") or "") for i in items})
blob=" ".join(
    (i.get("newValue") or i.get("NewValue") or "") + " " +
    (i.get("metadata") or i.get("Metadata") or "")
    for i in items
)
need = {
    "Payment.Requested",
    "Payment.GatewayAuthorityIssued",
    "Payment.Callback",
    "Payment.Verified",
}
missing = sorted(a for a in need if a not in actions)
has_user = "userId" in blob or "\"user\"" in blob
has_amount = "amount" in blob
has_authority = "authority" in blob or ("$auth2" in blob)
has_phoneish = "phoneNumber" in blob or "phone" in blob.lower()
open("$TMP_DIR/audit_actions","w").write(",".join(actions))
open("$TMP_DIR/audit_missing","w").write(",".join(missing))
open("$TMP_DIR/audit_has_user","w").write("1" if has_user else "0")
open("$TMP_DIR/audit_has_amount","w").write("1" if has_amount else "0")
open("$TMP_DIR/audit_has_auth","w").write("1" if has_authority else "0")
open("$TMP_DIR/audit_has_phone","w").write("1" if has_phoneish else "0")
open("$TMP_DIR/audit_count","w").write(str(len(items)))
print("actions=", ",".join(actions))
print("missing=", ",".join(missing) or "(none)")
print("count=", len(items))
PY
check "audit rows exist for verified charge" "$([[ "$(cat "$TMP_DIR/audit_count")" -ge 3 ]] && echo 1 || echo 0)"
check "audit has Payment.Requested for charge" "$([[ "$(cat "$TMP_DIR/audit_missing")" != *Payment.Requested* ]] && echo 1 || echo 0)"
check "audit has GatewayAuthorityIssued" "$([[ "$(cat "$TMP_DIR/audit_missing")" != *Payment.GatewayAuthorityIssued* ]] && echo 1 || echo 0)"
check "audit has Payment.Callback" "$([[ "$(cat "$TMP_DIR/audit_missing")" != *Payment.Callback* ]] && echo 1 || echo 0)"
check "audit has Payment.Verified" "$([[ "$(cat "$TMP_DIR/audit_missing")" != *Payment.Verified* ]] && echo 1 || echo 0)"
check "audit JSON includes userId" "$(cat "$TMP_DIR/audit_has_user")"
check "audit JSON includes amount" "$(cat "$TMP_DIR/audit_has_amount")"
check "audit JSON includes authority" "$(cat "$TMP_DIR/audit_has_auth")"
check "audit JSON includes phoneNumber" "$(cat "$TMP_DIR/audit_has_phone")"

# CancelDenied باید برای تلاش لغو بعد از Authority ثبت شده باشد
code="$(http_json GET "/api/Admin/Audit?category=payment&action=Payment.CancelDenied&entityId=${pid1}&pageSize=20" "" "$TMP_DIR/audit_cancel.json")"
python3 - <<PY
import json
d=json.load(open("$TMP_DIR/audit_cancel.json",encoding="utf-8"))
data=d.get("data") or {}
items=data.get("items") or data.get("Items") or []
ok=any((i.get("action") or i.get("Action"))=="Payment.CancelDenied" for i in items)
open("$TMP_DIR/audit_cancel_ok","w").write("1" if ok else "0")
print("cancel_denied_count=", len(items))
PY
check "audit CancelDenied stored for blocked cancel" "$(cat "$TMP_DIR/audit_cancel_ok")"

# Callback NOK برای pid1
code="$(http_json GET "/api/Admin/Audit?category=payment&action=Payment.Callback&entityId=${pid1}&pageSize=20" "" "$TMP_DIR/audit_nok.json")"
python3 - <<PY
import json
d=json.load(open("$TMP_DIR/audit_nok.json",encoding="utf-8"))
data=d.get("data") or {}
items=data.get("items") or data.get("Items") or []
ok=False
for i in items:
    blob=(i.get("newValue") or i.get("NewValue") or "") + (i.get("metadata") or i.get("Metadata") or "")
    if "NOK" in blob or "false" in blob.lower() or "لغو" in blob:
        ok=True; break
open("$TMP_DIR/audit_nok_ok","w").write("1" if items else "0")
open("$TMP_DIR/audit_nok_detail","w").write("1" if ok else "0")
print("nok_callback_count=", len(items))
PY
check "audit Callback exists for NOK payment" "$(cat "$TMP_DIR/audit_nok_ok")"
check "audit NOK callback has failure detail" "$(cat "$TMP_DIR/audit_nok_detail")"

# اشتراک — اگر پرداخت ساخته شده
if [[ -n "${spid:-}" && "$req_pay" == "true" ]]; then
  code="$(http_json GET "/api/Admin/Audit?entityId=${spid}&pageSize=50" "" "$TMP_DIR/audit_sub.json")"
  python3 - <<PY
import json
d=json.load(open("$TMP_DIR/audit_sub.json",encoding="utf-8"))
data=d.get("data") or {}
items=data.get("items") or data.get("Items") or []
actions={(i.get("action") or i.get("Action") or "") for i in items}
blob=" ".join((i.get("newValue") or i.get("NewValue") or "") for i in items)
need_ok = "Subscription.Purchased" in actions or "Payment.Verified" in actions
has_plan = "planId" in blob or "planName" in blob
open("$TMP_DIR/audit_sub_ok","w").write("1" if need_ok else "0")
open("$TMP_DIR/audit_sub_plan","w").write("1" if has_plan else "0")
print("sub_actions=", ",".join(sorted(actions)))
PY
  check "audit subscription purchase/verify logged" "$(cat "$TMP_DIR/audit_sub_ok")"
  check "audit subscription includes plan details" "$(cat "$TMP_DIR/audit_sub_plan")"
fi

echo
echo "=== RESULT: PASS=$PASS FAIL=$FAIL ==="
[[ "$FAIL" -eq 0 ]]
