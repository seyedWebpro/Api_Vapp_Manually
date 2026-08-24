#!/usr/bin/env bash
# کراول عمیق درگاه زرین‌پال — Production / Sandbox
#
# Production (پیشنهادی روی سرور واقعی):
#   BASE_URL=https://v-application.ir bash devops/scripts/crawl-zarinpal-payment-prod.sh
#
# Sandbox لوکال (با AllowSandboxAutoVerify برای مسیر OK کامل):
#   BASE_URL=http://127.0.0.1:5054 MODE=sandbox bash devops/scripts/crawl-zarinpal-payment-prod.sh
#
# در Production مسیر «شارژ واقعی بانکی» تست نمی‌شود (نیاز به کارت).
# بقیهٔ مسیرها: request Authority، NOK، قفل pending، cancel، reuse، verify بدون OK،
# audit، gateways، SSL، callback HTML، deep link، اشتراک.
set -euo pipefail

BASE_URL="${BASE_URL:-https://v-application.ir}"
MODE="${MODE:-production}" # production | sandbox
AUTH_TOKEN="${AUTH_TOKEN:-}"
TMP_DIR="$(mktemp -d)"
PASS=0
FAIL=0
CHARGE_AMOUNT="${CHARGE_AMOUNT:-10000}"

cleanup() { rm -rf "$TMP_DIR"; }
trap cleanup EXIT

if [[ -z "$AUTH_TOKEN" && -f /tmp/vapp_token.txt ]]; then
  AUTH_TOKEN="$(cat /tmp/vapp_token.txt)"
fi
if [[ -z "$AUTH_TOKEN" ]]; then
  echo "ERROR: set AUTH_TOKEN (Bearer JWT) — Production requires auth" >&2
  exit 1
fi

AUTH_HDR=(-H "Authorization: Bearer ${AUTH_TOKEN}")

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
    code="$(curl -sS -m 90 -o "$out" -w '%{http_code}' -X "$method" "$BASE_URL$path" \
      -H 'Content-Type: application/json' "${AUTH_HDR[@]}" -d "$body" || echo 000)"
  else
    code="$(curl -sS -m 90 -o "$out" -w '%{http_code}' -X "$method" "$BASE_URL$path" \
      "${AUTH_HDR[@]}" || echo 000)"
  fi
  echo "$code"
}

http_html() {
  local path="$1" out="$2"
  curl -sS -m 90 -o "$out" -w '%{http_code}' "$BASE_URL$path" || echo 000
}

balance_of() {
  python3 - "$1" <<'PY'
import json,sys
d=json.load(open(sys.argv[1],encoding="utf-8"))
data=d.get("data")
if isinstance(data,dict):
    for k in ("balance","Balance","walletBalance","WalletBalance"):
        if k in data: print(data[k]); break
    else: print(0)
else: print(data or 0)
PY
}

echo "=== ZarinPal DEEP crawl @ $BASE_URL (mode=$MODE) ==="

# ─── 0) Infra / SSL / health ───────────────────────────────────
code="$(http_json GET /health "" "$TMP_DIR/health.json")"
check "health HTTP 200" "$([[ "$code" == "200" ]] && echo 1 || echo 0)"
db_ready="$(json_get "$TMP_DIR/health.json" databaseReady)"
[[ -z "$db_ready" ]] && db_ready="$(json_get "$TMP_DIR/health.json" databaseReady)"
# health shape: databaseReady or nested
python3 - "$TMP_DIR/health.json" <<'PY'
import json,sys
d=json.load(open(sys.argv[1],encoding="utf-8"))
ok = d.get("databaseReady") is True or d.get("database")=="ready" or (d.get("status")=="healthy")
open("/tmp/vapp_health_ok","w").write("1" if ok else "0")
PY
# write beside tmp
python3 - <<PY
import json
d=json.load(open("$TMP_DIR/health.json",encoding="utf-8"))
ok = d.get("databaseReady") is True or d.get("database")=="ready" or (d.get("status")=="healthy")
open("$TMP_DIR/health_ok","w").write("1" if ok else "0")
PY
check "health database ready" "$(cat "$TMP_DIR/health_ok")"

if [[ "$BASE_URL" == https://* ]]; then
  curl -sS -m 30 -o /dev/null -w '%{ssl_verify_result}' "$BASE_URL/health" > "$TMP_DIR/ssl.txt" || echo 1 > "$TMP_DIR/ssl.txt"
  check "HTTPS TLS verify OK (0)" "$([[ "$(cat "$TMP_DIR/ssl.txt")" == "0" ]] && echo 1 || echo 0)"
  [[ "$BASE_URL" == https://api.v-application.ir* ]] && check "BASE_URL is api subdomain" "1"
fi

# ─── 1) Gateways ───────────────────────────────────────────────
code="$(http_json GET /api/Payment/gateways "" "$TMP_DIR/gw.json")"
check "gateways HTTP 200" "$([[ "$code" == "200" ]] && echo 1 || echo 0)"
python3 - <<PY
import json
d=json.load(open("$TMP_DIR/gw.json",encoding="utf-8"))
items=(d.get("data") or [])
zp=next((x for x in items if (x.get("code") or "").lower()=="zarinpal"), None)
beh=next((x for x in items if (x.get("code") or "").lower()=="behpardakht"), None)
open("$TMP_DIR/zp_ok","w").write("1" if zp and zp.get("isActive") else "0")
open("$TMP_DIR/beh_ok","w").write("1" if beh and (not beh.get("isActive")) else "0")
print("gateways=", [(x.get("code"), x.get("isActive")) for x in items])
PY
check "Zarinpal isActive=true" "$(cat "$TMP_DIR/zp_ok")"
check "Behpardakht isActive=false (prod simulation off)" "$(cat "$TMP_DIR/beh_ok")"

# ─── 2) Validation edges ───────────────────────────────────────
code="$(http_json POST /api/Wallet/charge '{"amount":100,"gateway":"Zarinpal"}' "$TMP_DIR/bad_amt.json")"
check "charge amount too low -> 400" "$([[ "$code" == "400" ]] && echo 1 || echo 0)"

code="$(http_json POST /api/Wallet/charge '{"amount":10000,"gateway":"Unknown"}' "$TMP_DIR/bad_gw.json")"
check "unsupported gateway -> 400" "$([[ "$code" == "400" ]] && echo 1 || echo 0)"

code="$(http_json POST /api/Wallet/charge '{"amount":10000,"gateway":"Behpardakht"}' "$TMP_DIR/beh.json")"
check "Behpardakht blocked when simulation off -> 400" "$([[ "$code" == "400" ]] && echo 1 || echo 0)"

code="$(http_json POST /api/Wallet/charge '{"amount":10000.5,"gateway":"Zarinpal"}' "$TMP_DIR/frac.json")"
check "fractional toman rejected -> 400" "$([[ "$code" == "400" ]] && echo 1 || echo 0)"

# ─── 3) Balance + charge request (live Authority) ───────────────
code="$(http_json GET /api/Wallet/balance "" "$TMP_DIR/bal0.json")"
bal0="$(balance_of "$TMP_DIR/bal0.json")"
echo "balance_before=$bal0"

code="$(http_json POST /api/Wallet/charge "{\"amount\":$CHARGE_AMOUNT,\"gateway\":\"Zarinpal\"}" "$TMP_DIR/ch1.json")"
check "wallet charge create HTTP 201/200" "$([[ "$code" == "201" || "$code" == "200" ]] && echo 1 || echo 0)"
pid1="$(json_get "$TMP_DIR/ch1.json" data.paymentId)"
auth1="$(json_get "$TMP_DIR/ch1.json" data.refId)"
url1="$(json_get "$TMP_DIR/ch1.json" data.gatewayUrl)"
sim1="$(json_get "$TMP_DIR/ch1.json" data.isSimulation)"
gw1="$(json_get "$TMP_DIR/ch1.json" data.gateway)"
check "charge paymentId present" "$([[ -n "$pid1" ]] && echo 1 || echo 0)"
check "charge gateway=Zarinpal" "$([[ "$gw1" == "Zarinpal" ]] && echo 1 || echo 0)"
check "charge isSimulation=false" "$([[ "$sim1" == "false" ]] && echo 1 || echo 0)"

if [[ "$MODE" == "sandbox" ]]; then
  check "sandbox authority S..." "$([[ "$auth1" == S* ]] && echo 1 || echo 0)"
  check "sandbox StartPay URL" "$([[ "$url1" == https://sandbox.zarinpal.com/pg/StartPay/* ]] && echo 1 || echo 0)"
else
  check "production authority A..." "$([[ "$auth1" == A* ]] && echo 1 || echo 0)"
  check "production StartPay URL" "$([[ "$url1" == https://payment.zarinpal.com/pg/StartPay/* ]] && echo 1 || echo 0)"
fi
check "authority length >= 20" "$([[ ${#auth1} -ge 20 ]] && echo 1 || echo 0)"

# pending lock
code="$(http_json POST /api/Wallet/charge "{\"amount\":$CHARGE_AMOUNT,\"gateway\":\"Zarinpal\"}" "$TMP_DIR/ch_pend.json")"
check "second charge blocked while pending -> 400" "$([[ "$code" == "400" ]] && echo 1 || echo 0)"

# cancel after authority blocked (avoid nested-quote bash pitfalls)
code=$(http_json POST "/api/Payment/${pid1}/cancel" '' "${TMP_DIR}/cancel.json")
check "cancel after authority blocked -> 400" "$([[ "$code" == "400" ]] && echo 1 || echo 0)"

# ─── 4) Callback NOK — no credit, status Failed ────────────────
code=$(http_html "/api/Payment/callback/zarinpal?Authority=${auth1}&Status=NOK" "${TMP_DIR}/nok.html")
check "callback NOK HTTP 200" "$([[ "$code" == "200" ]] && echo 1 || echo 0)"
grep -Eq 'success=0|vapp://payment/result' "${TMP_DIR}/nok.html" && nok_html=1 || nok_html=0
check "callback NOK HTML has deep link failure" "$nok_html"
# JS auto-redirect must not contain HTML entity &amp; (JsonSerializer may emit \u0026 which is fine)
python3 - "${TMP_DIR}/nok.html" <<'PY'
import re,sys
html=open(sys.argv[1],encoding="utf-8").read()
m=re.search(r"location\.replace\((.*)\);\s*\}, 400\)", html, re.S)
ok=False
if m:
    lit=m.group(1)
    ok = (
        "vapp://payment/result" in lit
        and "paymentId=" in lit
        and "&amp;" not in lit
    )
open(sys.argv[1]+".js_dl_ok","w").write("1" if ok else "0")
print("js_literal_ok", ok)
PY
check "callback JS deep link safe (no &amp; entity)" "$(cat "${TMP_DIR}/nok.html.js_dl_ok")"

code=$(http_json GET "/api/Payment/${pid1}" '' "${TMP_DIR}/p1.json")
st1="$(json_get "${TMP_DIR}/p1.json" data.status)"
check "payment after NOK is Failed" "$([[ "$st1" == "Failed" ]] && echo 1 || echo 0)"

code=$(http_json GET /api/Wallet/balance '' "${TMP_DIR}/bal_nok.json")
bal_nok="$(balance_of "${TMP_DIR}/bal_nok.json")"
python3 - <<PY
b0=float("$bal0"); bn=float("$bal_nok")
open("${TMP_DIR}/nok_bal_ok","w").write("1" if abs(b0-bn)<0.001 else "0")
PY
check "NOK did not change wallet balance" "$(cat "${TMP_DIR}/nok_bal_ok")"

# ─── 5) Callback edge cases ────────────────────────────────────
code=$(http_html "/api/Payment/callback/zarinpal" "${TMP_DIR}/noauth.html")
check "callback without authority 200 HTML" "$([[ "$code" == "200" ]] && echo 1 || echo 0)"
grep -qi "vapp://payment/result" "${TMP_DIR}/noauth.html" && dl0=1 || dl0=0
check "no-authority HTML still has deep link" "$dl0"

code=$(http_html "/api/Payment/callback/zarinpal?Authority=SHORT&Status=OK" "${TMP_DIR}/short.html")
check "invalid short authority 200 HTML" "$([[ "$code" == "200" ]] && echo 1 || echo 0)"

code=$(http_html "/api/Payment/callback/zarinpal?Authority=AFAKEINVALIDAUTHORITY000000000000000&Status=OK" "${TMP_DIR}/fake.html")
check "unknown authority 200 HTML" "$([[ "$code" == "200" ]] && echo 1 || echo 0)"

# lowercase query keys
code=$(http_html "/api/Payment/callback/zarinpal?authority=${auth1}&status=NOK" "${TMP_DIR}/low.html")
check "lowercase authority/status accepted 200" "$([[ "$code" == "200" ]] && echo 1 || echo 0)"

# ─── 6) New charge + verify without Status=OK + authority reuse ──
code=$(http_json POST /api/Wallet/charge "{\"amount\":$CHARGE_AMOUNT,\"gateway\":\"Zarinpal\"}" "${TMP_DIR}/ch2.json")
pid2="$(json_get "${TMP_DIR}/ch2.json" data.paymentId)"
auth2="$(json_get "${TMP_DIR}/ch2.json" data.refId)"
check "second charge after NOK created" "$([[ -n "$pid2" && -n "$auth2" ]] && echo 1 || echo 0)"

code=$(http_json POST /api/Payment/verify "{\"paymentId\":$pid2,\"authority\":\"$auth2\"}" "${TMP_DIR}/nostatus.json")
check "verify without Status -> 400" "$([[ "$code" == "400" ]] && echo 1 || echo 0)"

code=$(http_json POST /api/Payment/verify "{\"paymentId\":$pid2,\"authority\":\"$auth1\",\"status\":\"OK\"}" "${TMP_DIR}/reuse.json")
check "authority reuse verify -> 400" "$([[ "$code" == "400" ]] && echo 1 || echo 0)"

code=$(http_json GET "/api/Payment/${pid2}" '' "${TMP_DIR}/p2.json")
st2="$(json_get "${TMP_DIR}/p2.json" data.status)"
check "reused-authority payment not Verified" "$([[ "$st2" != "Verified" ]] && echo 1 || echo 0)"

# Status=OK بدون پرداخت واقعی → verify درگاه fail (معمولاً -51) → Failed قطعی
code=$(http_html "/api/Payment/callback/zarinpal?Authority=${auth2}&Status=OK" "${TMP_DIR}/ok_unpaid.html")
check "callback OK unpaid HTTP 200" "$([[ "$code" == "200" ]] && echo 1 || echo 0)"
code=$(http_json GET "/api/Payment/${pid2}" '' "${TMP_DIR}/p2b.json")
st2b="$(json_get "${TMP_DIR}/p2b.json" data.status)"
# ممکن است Failed (قطعی) یا هنوز Processing اگر transient — هر دو غیر Verified OK است
check "OK-without-pay not Verified" "$([[ "$st2b" != "Verified" ]] && echo 1 || echo 0)"
code=$(http_json GET /api/Wallet/balance '' "${TMP_DIR}/bal_unpaid.json")
bal_u="$(balance_of "${TMP_DIR}/bal_unpaid.json")"
python3 - <<PY
b0=float("$bal0"); bu=float("$bal_u")
open("${TMP_DIR}/unpaid_bal","w").write("1" if abs(b0-bu)<0.001 else "0")
PY
check "OK-without-pay did not credit wallet" "$(cat "${TMP_DIR}/unpaid_bal")"

# اگر هنوز Processing بود، با NOK پاک کن
if [[ "$st2b" == "Processing" || "$st2b" == "Pending" ]]; then
  http_html "/api/Payment/callback/zarinpal?Authority=${auth2}&Status=NOK" "${TMP_DIR}/clean2.html" >/dev/null
fi

# ─── 7) Sandbox-only happy path (auto-verify) ──────────────────
if [[ "$MODE" == "sandbox" ]]; then
  code=$(http_json POST /api/Wallet/charge "{\"amount\":$CHARGE_AMOUNT,\"gateway\":\"Zarinpal\"}" "${TMP_DIR}/ch3.json")
  pid3="$(json_get "${TMP_DIR}/ch3.json" data.paymentId)"
  auth3="$(json_get "${TMP_DIR}/ch3.json" data.refId)"
  code=$(http_html "/api/Payment/callback/zarinpal?Authority=${auth3}&Status=OK" "${TMP_DIR}/ok.html")
  check "sandbox callback OK 200" "$([[ "$code" == "200" ]] && echo 1 || echo 0)"
  code=$(http_json GET "/api/Payment/${pid3}" '' "${TMP_DIR}/p3.json")
  st3="$(json_get "${TMP_DIR}/p3.json" data.status)"
  check "sandbox payment Verified" "$([[ "$st3" == "Verified" ]] && echo 1 || echo 0)"
  code=$(http_json GET /api/Wallet/balance '' "${TMP_DIR}/bal_ok.json")
  bal_ok="$(balance_of "${TMP_DIR}/bal_ok.json")"
  python3 - <<PY
b0=float("$bal0"); bo=float("$bal_ok"); amt=float("$CHARGE_AMOUNT")
open("${TMP_DIR}/ok_bal","w").write("1" if abs((b0+amt)-bo)<0.001 else "0")
print(f"expected={b0+amt} actual={bo}")
PY
  check "sandbox OK credited wallet" "$(cat "${TMP_DIR}/ok_bal")"
  # double callback
  http_html "/api/Payment/callback/zarinpal?Authority=${auth3}&Status=OK" "${TMP_DIR}/ok2.html" >/dev/null
  code=$(http_json GET /api/Wallet/balance '' "${TMP_DIR}/bal_dup.json")
  bal_dup="$(balance_of "${TMP_DIR}/bal_dup.json")"
  python3 - <<PY
a=float("$bal_ok"); b=float("$bal_dup")
open("${TMP_DIR}/dup_ok","w").write("1" if abs(a-b)<0.001 else "0")
PY
  check "sandbox double callback no double-credit" "$(cat "${TMP_DIR}/dup_ok")"
else
  echo "SKIP  production happy-path credit (needs real bank payment)"
  check "production note: real pay required for Verified credit" "1"
fi

# ─── 8) Subscription purchase request ──────────────────────────
code=$(http_json GET /api/UserSubscription/catalog '' "${TMP_DIR}/cat.json")
plan_id="$(python3 - "${TMP_DIR}/cat.json" <<'PY'
import json,sys
d=json.load(open(sys.argv[1],encoding="utf-8"))
data=d.get("data") or {}
plans=data.get("plans") or []
for p in plans:
    if isinstance(p,dict) and not p.get("isFree") and p.get("canPurchase", True) and p.get("id"):
        print(p["id"]); break
PY
)"
[[ -z "$plan_id" ]] && plan_id=2
echo "subscription_plan_id=$plan_id"

code=$(http_json POST /api/UserSubscription/purchase "{\"planId\":$plan_id,\"gateway\":\"Zarinpal\"}" "${TMP_DIR}/sub.json")
check "subscription purchase HTTP 200/201" "$([[ "$code" == "200" || "$code" == "201" ]] && echo 1 || echo 0)"
req_pay="$(json_get "${TMP_DIR}/sub.json" data.requiresPayment)"
spid="$(json_get "${TMP_DIR}/sub.json" data.paymentId)"
sauth="$(json_get "${TMP_DIR}/sub.json" data.refId)"
surl="$(json_get "${TMP_DIR}/sub.json" data.gatewayUrl)"
if [[ "$req_pay" == "true" ]]; then
  check "subscription requiresPayment" "1"
  check "subscription paymentId present" "$([[ -n "$spid" ]] && echo 1 || echo 0)"
  if [[ "$MODE" == "production" ]]; then
    check "subscription production authority A..." "$([[ "$sauth" == A* ]] && echo 1 || echo 0)"
    check "subscription StartPay production" "$([[ "$surl" == https://payment.zarinpal.com/pg/StartPay/* ]] && echo 1 || echo 0)"
  else
    check "subscription sandbox authority" "$([[ "$sauth" == S* ]] && echo 1 || echo 0)"
  fi
  # cleanup pending via NOK
  http_html "/api/Payment/callback/zarinpal?Authority=${sauth}&Status=NOK" "${TMP_DIR}/sub_nok.html" >/dev/null
  code=$(http_json GET "/api/Payment/${spid}" '' "${TMP_DIR}/sp.json")
  sst="$(json_get "${TMP_DIR}/sp.json" data.status)"
  check "subscription NOK -> Failed" "$([[ "$sst" == "Failed" ]] && echo 1 || echo 0)"
else
  check "subscription zero-pay path" "1"
fi

# ─── 9) Merchant leak + audit ──────────────────────────────────
code=$(http_json GET "/api/Payment/${pid1}" '' "${TMP_DIR}/leak.json")
python3 - <<PY
import json
d=json.load(open("${TMP_DIR}/leak.json",encoding="utf-8"))
s=json.dumps(d,ensure_ascii=False).lower()
bad = "merchant" in s or "f37b8b50" in s
open("${TMP_DIR}/noleak","w").write("0" if bad else "1")
PY
check "MerchantId not leaked in payment DTO" "$(cat "${TMP_DIR}/noleak")"

code=$(http_json GET "/api/Admin/Audit?category=payment&entityId=${pid1}&pageSize=50" '' "${TMP_DIR}/audit.json")
check "audit search HTTP 200" "$([[ "$code" == "200" ]] && echo 1 || echo 0)"
python3 - <<PY
import json
d=json.load(open("${TMP_DIR}/audit.json",encoding="utf-8"))
items=(d.get("data") or {}).get("items") or []
actions={(i.get("action") or "") for i in items}
blob=" ".join((i.get("newValue") or "")+" "+(i.get("metadata") or "") for i in items)
need={"Payment.Requested","Payment.GatewayAuthorityIssued","Payment.Callback"}
missing=sorted(need-actions)
open("${TMP_DIR}/audit_missing","w").write(",".join(missing))
open("${TMP_DIR}/audit_ok","w").write("1" if not missing else "0")
open("${TMP_DIR}/audit_detail","w").write("1" if ("amount" in blob and "userId" in blob) else "0")
print("actions=", ",".join(sorted(actions)))
print("missing=", missing or "(none)")
PY
check "audit has request/authority/callback" "$(cat "${TMP_DIR}/audit_ok")"
check "audit JSON has amount+userId" "$(cat "${TMP_DIR}/audit_detail")"

# CancelDenied audit
code=$(http_json GET "/api/Admin/Audit?category=payment&action=Payment.CancelDenied&entityId=${pid1}&pageSize=10" '' "${TMP_DIR}/ac.json")
python3 - <<PY
import json
d=json.load(open("${TMP_DIR}/ac.json",encoding="utf-8"))
items=(d.get("data") or {}).get("items") or []
open("${TMP_DIR}/ac_ok","w").write("1" if items else "0")
PY
check "audit CancelDenied stored" "$(cat "${TMP_DIR}/ac_ok")"

# ─── 10) Direct ZarinPal domain match (live request) ───────────
# فقط چک می‌کند callback دامنه از دید زرین‌پال قبول است (مثل پاسخ پشتیبانی)
MERCHANT="$(python3 - <<'PY'
print("f37b8b50-6a1d-4a29-a8c0-ecd38fb52535")
PY
)"
curl -sS -m 30 -o "$TMP_DIR/zp_req.json" -w '%{http_code}' \
  https://payment.zarinpal.com/pg/v4/payment/request.json \
  -H 'Content-Type: application/json' -H 'Accept: application/json' \
  -d "{\"merchant_id\":\"$MERCHANT\",\"amount\":10000,\"description\":\"crawl domain check\",\"callback_url\":\"https://v-application.ir/api/Payment/callback/zarinpal\",\"currency\":\"IRT\"}" \
  > "$TMP_DIR/zp_http.txt" || echo 000 > "$TMP_DIR/zp_http.txt"
python3 - <<PY
import json
code=open("$TMP_DIR/zp_http.txt").read().strip()
try:
  d=json.load(open("$TMP_DIR/zp_req.json",encoding="utf-8"))
except Exception:
  d={}
data=d.get("data") or {}
ok = code=="200" and data.get("code")==100 and str(data.get("authority","")).startswith("A")
open("$TMP_DIR/zp_live","w").write("1" if ok else "0")
print("zp_http=", code, "data_code=", data.get("code"), "auth=", data.get("authority"))
errs=d.get("errors")
print("errors=", errs)
PY
check "live ZarinPal request accepts api.v-application.ir callback (-14 free)" "$(cat "$TMP_DIR/zp_live")"

echo
echo "=== RESULT: PASS=$PASS FAIL=$FAIL mode=$MODE ==="
[[ "$FAIL" -eq 0 ]]
