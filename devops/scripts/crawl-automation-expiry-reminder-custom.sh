#!/usr/bin/env bash
# Crawl تست سه نوع اتوماسیون: CashbackExpiry / PurchaseReminder / Custom
#
# Usage:
#   BASE_URL=http://127.0.0.1:5054 bash devops/scripts/crawl-automation-expiry-reminder-custom.sh
#   SKIP_BUILD=1 SKIP_API_RESTART=1 bash devops/scripts/crawl-automation-expiry-reminder-custom.sh
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/../.." && pwd)"
cd "$ROOT_DIR"

BASE_URL="${BASE_URL:-http://127.0.0.1:5054}"
OWNER_PHONE="${OWNER_PHONE:-09920374397}"
SKIP_BUILD="${SKIP_BUILD:-0}"
SKIP_API_RESTART="${SKIP_API_RESTART:-0}"
SKIP_UNIT="${SKIP_UNIT:-0}"
LOG=/tmp/vapp-automation-three-types-crawl.log
PIDFILE=/tmp/vapp-automation-three-types-crawl.pid
TMP_DIR="$(mktemp -d)"
PASS=0
FAIL=0
CREATED_IDS=()
TOKEN=""

cleanup() {
  for id in "${CREATED_IDS[@]:-}"; do
    if [[ -n "$TOKEN" ]]; then
      curl -sS -m 10 -o /dev/null -X POST "$BASE_URL/api/AutomatedMessage/${id}/delete" \
        -H "Authorization: Bearer $TOKEN" || true
    else
      curl -sS -m 10 -o /dev/null -X POST "$BASE_URL/api/AutomatedMessage/${id}/delete" || true
    fi
  done
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

http_ok() {
  local code="$1"
  [[ "$code" == "200" || "$code" == "201" ]]
}

http_json() {
  local method="$1" path="$2" body="${3:-}" out="$4"
  local code
  local -a cmd=(curl -sS -m 45 -o "$out" -w '%{http_code}' -X "$method" "$BASE_URL$path" -H 'Content-Type: application/json')
  if [[ -n "$TOKEN" ]]; then
    cmd+=(-H "Authorization: Bearer $TOKEN")
  fi
  if [[ -n "$body" ]]; then
    cmd+=(-d "$body")
  fi
  code="$("${cmd[@]}" || echo 000)"
  echo "$code"
}

write_json() {
  local out="$1"
  shift
  python3 - "$out" "$@" <<'PY'
import json,sys
out=sys.argv[1]
obj=json.loads(sys.argv[2])
with open(out,"w",encoding="utf-8") as f:
    json.dump(obj,f,ensure_ascii=False)
print(out)
PY
}

ensure_api() {
  local code
  code="$(curl -sS -m 5 -o /dev/null -w '%{http_code}' "$BASE_URL/health" 2>/dev/null || echo 000)"
  if [[ "$code" == "200" ]]; then
    echo "API already up at $BASE_URL"
    return 0
  fi
  if [[ "$SKIP_API_RESTART" == "1" ]]; then
    echo "ERROR: API not reachable and SKIP_API_RESTART=1" >&2
    return 1
  fi

  if [[ "$SKIP_BUILD" != "1" ]]; then
    echo "Building API..."
    DOTNET_ROLL_FORWARD=Major /usr/local/share/dotnet/dotnet build Api_Vapp.csproj --nologo -v q
  fi

  if [[ -f "$PIDFILE" ]]; then
    oldpid="$(cat "$PIDFILE" || true)"
    if [[ -n "$oldpid" ]] && kill -0 "$oldpid" 2>/dev/null; then
      kill "$oldpid" 2>/dev/null || true
      sleep 1
    fi
    rm -f "$PIDFILE"
  fi

  echo "Starting API on $BASE_URL ..."
  : > "$LOG"
  nohup env DOTNET_ROLL_FORWARD=Major ASPNETCORE_ENVIRONMENT=Development DatabaseProvider=LocalDocker \
    /usr/local/share/dotnet/dotnet exec bin/Debug/net8.0/Api_Vapp.dll --urls "$BASE_URL" \
    >"$LOG" 2>&1 &
  echo $! > "$PIDFILE"

  for i in $(seq 1 90); do
    code="$(curl -sS -m 3 -o /dev/null -w '%{http_code}' "$BASE_URL/health" 2>/dev/null || echo 000)"
    if [[ "$code" == "200" ]]; then
      echo "API ready (attempt $i)"
      return 0
    fi
    sleep 1
  done
  echo "ERROR: API failed to start. Last log:" >&2
  tail -60 "$LOG" >&2 || true
  return 1
}

get_token() {
  local phone="$1" login otp verify wait_i marker logf
  logf="$ROOT_DIR/log/log-$(date +%Y%m%d).txt"
  for wait_i in 1 2 3 4 5; do
    marker="OTP_MARK_${phone}_$(date +%s)_$wait_i"
    [[ -d "$(dirname "$logf")" ]] && echo "$marker" >> "$logf" || true
    login=$(curl -sS -m 30 -X POST "$BASE_URL/api/Auth/login" \
      -H 'Content-Type: application/json' -d "{\"phoneNumber\":\"$phone\"}" || true)
    otp=$(echo "$login" | python3 -c "import sys,json; d=json.load(sys.stdin); print(d.get('otpCode') or (d.get('data') or {}).get('otpCode') or '')" 2>/dev/null || true)
    if [[ -z "$otp" && -f "$logf" ]]; then
      otp=$(awk -v m="$marker" 'index($0,m){f=1;next} f && /OTP/{match($0,/[0-9]{4,8}/); if(RLENGTH>0){print substr($0,RSTART,RLENGTH); exit}}' "$logf" || true)
    fi
    if [[ -n "$otp" ]]; then
      verify=$(curl -sS -m 30 -X POST "$BASE_URL/api/Auth/verify-login" \
        -H 'Content-Type: application/json' \
        -d "{\"phoneNumber\":\"$phone\",\"otpCode\":\"$otp\"}" || true)
      token=$(echo "$verify" | python3 -c "import sys,json; d=json.load(sys.stdin); print(d.get('token') or (d.get('data') or {}).get('token') or (d.get('data') or {}).get('accessToken') or '')" 2>/dev/null || true)
      if [[ -n "$token" ]]; then
        echo "$token"
        return 0
      fi
    fi
    sleep 1
  done
  return 1
}

ensure_subscription() {
  local profile plans gold user_id
  profile="$TMP_DIR/profile.json"
  plans="$TMP_DIR/plans.json"
  if [[ -n "$TOKEN" ]]; then
    curl -sS -m 20 -o "$profile" -H "Authorization: Bearer $TOKEN" "$BASE_URL/api/User/profile" || true
  else
    curl -sS -m 20 -o "$profile" "$BASE_URL/api/User/profile" || true
  fi
  user_id="$(json_get "$profile" data.id)"
  curl -sS -m 20 -o "$plans" "$BASE_URL/api/Admin/SubscriptionPlan?includeInactive=true" || true
  gold="$(python3 - "$plans" <<'PY'
import json,sys
try:
  d=json.load(open(sys.argv[1],encoding='utf-8'))
except Exception:
  print(""); raise SystemExit
for p in (d.get('data') or []):
  feats=[(f.get('code') or '') for f in (p.get('features') or [])]
  if 'message_automation' in feats:
    print(p.get('id') or ''); break
PY
)"
  if [[ -n "$user_id" && -n "$gold" ]]; then
    curl -sS -m 20 -o "$TMP_DIR/assign.json" -X POST "$BASE_URL/api/Admin/UserSubscription/assign" \
      -H 'Content-Type: application/json' \
      -d "{\"userId\":$user_id,\"subscriptionPlanId\":$gold}" >/dev/null || true
    echo "      subscription plan=$gold for user=$user_id"
  fi
}

seed_cashback_contact() {
  python3 - <<'PY'
import json, os, subprocess, time
from datetime import datetime, timedelta, timezone

base=os.environ["BASE_URL"]
token=os.environ.get("TOKEN","")
tmp=os.environ["TMP_DIR"]

def http(method, path, body=None):
    out=f"{tmp}/seed_{int(time.time()*1000)}.json"
    cmd=["curl","-sS","-m","30","-o",out,"-w","%{http_code}","-X",method,f"{base}{path}","-H","Content-Type: application/json"]
    if token:
        cmd += ["-H", f"Authorization: Bearer {token}"]
    if body is not None:
        cmd += ["-d", json.dumps(body, ensure_ascii=False)]
    code=subprocess.check_output(cmd, text=True).strip()
    try:
        data=json.load(open(out,encoding="utf-8"))
    except Exception:
        data={}
    return code, data

code, notebooks = http("GET", "/api/ContactNotebook")
items = notebooks.get("data") or []
if isinstance(items, dict):
    items = items.get("items") or items.get("notebooks") or []
nb_id = items[0].get("id") if isinstance(items, list) and items else None
print(f"notebook={nb_id} listHttp={code}")
if not nb_id:
    raise SystemExit("no notebook")

# prefer an existing contact
code, listed = http("GET", f"/api/Contact?contactNotebookId={nb_id}&pageNumber=1&pageSize=20")
data = listed.get("data") or {}
arr = data.get("items") or data.get("contacts") or []
if not isinstance(arr, list):
    arr = []
cid = arr[0].get("id") if arr else None
print(f"existing_contact={cid} listHttp={code} count={len(arr)}")

if not cid:
    mobile=f"09{int(time.time())%1000000000:09d}"
    code, contact = http("POST", "/api/Contact", {
        "contactNotebookId": nb_id,
        "fullName": "مخاطب تست اتوماسیون",
        "mobileNumber": mobile
    })
    cid = ((contact.get("data") or {}).get("id"))
    print(f"create_contact http={code} id={cid} msg={contact.get('message')} errors={contact.get('errors')}")
    if not cid:
        raise SystemExit("no contact")

code, cb = http("POST", "/api/Cashback/contact/add-manual", {
    "contactId": cid,
    "amount": 25000,
    "validityDays": 2,
    "description": "seed automation crawl"
})
print(f"cashback_add http={code} success={cb.get('success')} msg={cb.get('message')}")
open(f"{tmp}/seed_meta.json","w",encoding="utf-8").write(json.dumps({
    "notebookId": nb_id,
    "contactId": cid,
    "cashbackSuccess": cb.get("success"),
}, ensure_ascii=False))
if cb.get("success") is not True:
    raise SystemExit("cashback seed failed")
PY
}

flow_type() {
  local type="$1" settings_file="$2" content="$3"
  local create select settings msg summary out id total eligible code ok

  echo ""
  echo "----- flow $type -----"
  create="$TMP_DIR/create_${type}.json"
  code="$(http_json POST /api/AutomatedMessage/create-draft "{\"automationType\":\"$type\"}" "$create")"
  id="$(json_get "$create" data.id)"
  ok="$(json_get "$create" success)"
  if http_ok "$code"; then check "$type create-draft HTTP ok" 1; else check "$type create-draft HTTP ok" 0; fi
  if [[ "$ok" == "true" && -n "$id" && "$id" != "0" ]]; then check "$type create-draft has id" 1; else check "$type create-draft has id" 0; fi
  [[ -n "$id" && "$id" != "0" ]] && CREATED_IDS+=("$id")
  echo "      id=$id http=$code"

  select="$TMP_DIR/select_${type}.json"
  code="$(http_json POST "/api/AutomatedMessage/$id/recipients/select" '{"applyToAllContacts":true}' "$select")"
  total="$(json_get "$select" data.totalCount)"
  eligible="$(json_get "$select" data.eligibleCount)"
  ok="$(json_get "$select" success)"
  if http_ok "$code"; then check "$type recipients/select before settings HTTP ok" 1; else check "$type recipients/select before settings HTTP ok" 0; fi
  if [[ "$ok" == "true" ]]; then check "$type recipients/select success" 1; else check "$type recipients/select success" 0; fi
  echo "      select total=$total eligible=$eligible"

  settings="$TMP_DIR/settings_${type}.json"
  code="$(http_json POST "/api/AutomatedMessage/$id/settings" "$(cat "$settings_file")" "$settings")"
  ok="$(json_get "$settings" success)"
  if http_ok "$code"; then check "$type settings HTTP ok" 1; else check "$type settings HTTP ok" 0; fi
  if [[ "$ok" == "true" ]]; then check "$type settings success" 1; else check "$type settings success" 0; fi
  echo "      settings http=$code msg=$(json_get "$settings" message)"

  msg="$TMP_DIR/msg_${type}.json"
  code="$(http_json POST "/api/AutomatedMessage/$id/message/content" \
    "$(python3 -c 'import json,sys; print(json.dumps({"content":sys.argv[1]},ensure_ascii=False))' "$content")" "$msg")"
  if http_ok "$code"; then check "$type message/content HTTP ok" 1; else check "$type message/content HTTP ok" 0; fi

  summary="$TMP_DIR/summary_${type}.json"
  code="$(http_json POST "/api/AutomatedMessage/$id/summary/calculate" \
    '{"preventDuplicate":true,"duplicatePreventionHours":24,"sendToSpecificTags":false}' "$summary")"
  if [[ "$code" == "200" || "$code" == "201" || "$code" == "400" ]]; then
    check "$type summary/calculate HTTP handled" 1
  else
    check "$type summary/calculate HTTP handled" 0
  fi
  echo "      summary http=$code success=$(json_get "$summary" success) eligible=$(json_get "$summary" data.eligibleRecipientsCount) msg=$(json_get "$summary" message)"

  out="$TMP_DIR/get_${type}.json"
  code="$(http_json GET "/api/AutomatedMessage/$id" "" "$out")"
  ok="$(json_get "$out" success)"
  if [[ "$code" == "200" && "$ok" == "true" ]]; then check "$type GET detail ok" 1; else check "$type GET detail ok" 0; fi
  echo "      daysBefore=$(json_get "$out" data.daysBeforeEvent) activation=$(json_get "$out" data.activationConditions)"
}

echo "=== crawl-automation-expiry-reminder-custom ==="
echo "BASE=$BASE_URL"

if [[ "$SKIP_UNIT" != "1" ]]; then
  echo "--- unit tests ---"
  if DOTNET_ROLL_FORWARD=Major /usr/local/share/dotnet/dotnet test Tests/Api_Vapp.Tests.csproj \
      --filter 'FullyQualifiedName~AutomationRecipientEvaluatorTests' \
      --nologo --logger 'console;verbosity=minimal'; then
    check "unit AutomationRecipientEvaluatorTests" 1
  else
    check "unit AutomationRecipientEvaluatorTests" 0
  fi
fi

ensure_api

TOKEN=""
if tok="$(get_token "$OWNER_PHONE" 2>/dev/null || true)"; then
  TOKEN="$tok"
fi
if [[ -n "$TOKEN" ]]; then
  echo "Auth token acquired"
else
  echo "INFO: continuing without token (DisableAuth may be enabled)"
fi
export TOKEN BASE_URL TMP_DIR
ensure_subscription

unauth="$TMP_DIR/unauth.json"
code="$(curl -sS -m 15 -o "$unauth" -w '%{http_code}' -X POST "$BASE_URL/api/AutomatedMessage/create-draft" \
  -H 'Content-Type: application/json' -d '{"automationType":"CashbackExpiry"}' || echo 000)"
if [[ "$code" == "401" || "$code" == "200" || "$code" == "201" ]]; then
  check "create-draft without token → 401 or DisableAuth 200/201" 1
else
  check "create-draft without token → 401 or DisableAuth 200/201" 0
fi

seed_cashback_contact || echo "WARN seed cashback failed (continuing)"

CB_SETTINGS="$TMP_DIR/cb_settings.json"
PR_SETTINGS="$TMP_DIR/pr_settings.json"
CU_SETTINGS="$TMP_DIR/cu_settings.json"
ALIAS_SETTINGS="$TMP_DIR/alias_settings.json"
INV_SETTINGS="$TMP_DIR/inv_settings.json"
BAD_SETTINGS="$TMP_DIR/bad_settings.json"

python3 - <<PY
import json
from pathlib import Path
tmp=Path("$TMP_DIR")
(tmp/"cb_settings.json").write_text(json.dumps({
  "type":"CashbackExpiry",
  "cashbackExpirySettings":{"daysBeforeExpiry":2,"executionMode":"Once"}
}, ensure_ascii=False), encoding="utf-8")
(tmp/"pr_settings.json").write_text(json.dumps({
  "type":"PurchaseReminder",
  "purchaseReminderSettings":{"daysWithoutPurchase":30}
}, ensure_ascii=False), encoding="utf-8")
(tmp/"cu_settings.json").write_text(json.dumps({
  "type":"Custom",
  "customAutomationSettings":{"activationConditions":json.dumps({"daysWithoutPurchase":30,"executionMode":"Once"})}
}, ensure_ascii=False), encoding="utf-8")
(tmp/"alias_settings.json").write_text(json.dumps({
  "type":"Custom",
  "customSettings":{"activationConditions":json.dumps({"hasCashback":True,"executionMode":"Once"})}
}, ensure_ascii=False), encoding="utf-8")
(tmp/"inv_settings.json").write_text(json.dumps({
  "type":"Custom",
  "customAutomationSettings":{"activationConditions":json.dumps({"condition":"value"})}
}, ensure_ascii=False), encoding="utf-8")
(tmp/"bad_settings.json").write_text(json.dumps({
  "type":"Custom",
  "customAutomationSettings":{"activationConditions":"{bad"}
}, ensure_ascii=False), encoding="utf-8")
PY

flow_type "CashbackExpiry" "$CB_SETTINGS" "{{نام}} عزیز، کش بک شما به زودی منقضی می شود"
flow_type "PurchaseReminder" "$PR_SETTINGS" "{{نام}} عزیز، دلمان برایتان تنگ شده"
flow_type "Custom" "$CU_SETTINGS" "{{نام}} عزیز، پیام سفارشی تست"

echo ""
echo "----- Custom mobile alias customSettings -----"
alias_create="$TMP_DIR/alias_create.json"
code="$(http_json POST /api/AutomatedMessage/create-draft '{"automationType":"Custom"}' "$alias_create")"
alias_id="$(json_get "$alias_create" data.id)"
CREATED_IDS+=("$alias_id")
http_json POST "/api/AutomatedMessage/$alias_id/recipients/select" '{"applyToAllContacts":true}' "$TMP_DIR/alias_sel.json" >/dev/null
alias_set="$TMP_DIR/alias_set.json"
code="$(http_json POST "/api/AutomatedMessage/$alias_id/settings" "$(cat "$ALIAS_SETTINGS")" "$alias_set")"
ok="$(json_get "$alias_set" success)"
if http_ok "$code" && [[ "$ok" == "true" ]]; then check "Custom mobile alias customSettings → 200" 1; else check "Custom mobile alias customSettings → 200" 0; fi
echo "      msg=$(json_get "$alias_set" message)"

echo ""
echo "----- Custom invalid conditions -----"
inv_create="$TMP_DIR/inv_create.json"
code="$(http_json POST /api/AutomatedMessage/create-draft '{"automationType":"Custom"}' "$inv_create")"
inv_id="$(json_get "$inv_create" data.id)"
CREATED_IDS+=("$inv_id")
http_json POST "/api/AutomatedMessage/$inv_id/recipients/select" '{"applyToAllContacts":true}' "$TMP_DIR/inv_sel.json" >/dev/null
inv_set="$TMP_DIR/inv_set.json"
code="$(http_json POST "/api/AutomatedMessage/$inv_id/settings" "$(cat "$INV_SETTINGS")" "$inv_set")"
ok="$(json_get "$inv_set" success)"
if [[ "$code" == "400" && "$ok" == "false" ]]; then check "Custom invalid keys → 400" 1; else check "Custom invalid keys → 400" 0; fi
echo "      msg=$(json_get "$inv_set" message)"

inv2="$TMP_DIR/inv2.json"
code="$(http_json POST "/api/AutomatedMessage/$inv_id/settings" "$(cat "$BAD_SETTINGS")" "$inv2")"
ok="$(json_get "$inv2" success)"
if [[ "$code" == "400" && "$ok" == "false" ]]; then check "Custom malformed JSON → 400" 1; else check "Custom malformed JSON → 400" 0; fi

own="$TMP_DIR/own.json"
code="$(http_json GET "/api/AutomatedMessage/999999999" "" "$own")"
if [[ "$code" == "404" ]]; then check "GET missing automation → 404" 1; else check "GET missing automation → 404" 0; fi

echo ""
echo "=== Result: PASS=$PASS FAIL=$FAIL ==="
[[ "$FAIL" -eq 0 ]]
