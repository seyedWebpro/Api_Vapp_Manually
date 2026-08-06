#!/usr/bin/env bash
# Crawl/smoke تست چرخه عمر پیام خودکار: create → toggle → cancel → delete
# Usage:
#   BASE_URL=http://127.0.0.1:5054 bash devops/scripts/crawl-automated-message-lifecycle.sh
set -euo pipefail

BASE_URL="${BASE_URL:-http://127.0.0.1:5054}"
TMP_DIR="$(mktemp -d)"
PASS=0
FAIL=0
CREATED_IDS=()

cleanup() {
  # بهترین‌تلاش: حذف هر draft باقی‌مانده از این اجرا
  for id in "${CREATED_IDS[@]:-}"; do
    curl -sS -m 10 -o /dev/null -X POST "$BASE_URL/api/AutomatedMessage/${id}/delete" || true
  done
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
  if [[ -n "$body" ]]; then
    code="$(curl -sS -m 30 -o "$out" -w '%{http_code}' -X "$method" "$BASE_URL$path" \
      -H 'Content-Type: application/json' \
      -d "$body" || echo 000)"
  else
    code="$(curl -sS -m 30 -o "$out" -w '%{http_code}' -X "$method" "$BASE_URL$path" || echo 000)"
  fi
  echo "$code"
}

echo "=== AutomatedMessage lifecycle crawl @ $BASE_URL ==="

# 0) health
code="$(curl -sS -m 10 -o /dev/null -w '%{http_code}' "$BASE_URL/health" || echo 000)"
check "GET /health → 200" "$([[ "$code" == "200" ]] && echo 1 || echo 0)"

# 0.1) ensure default user has message_automation (gold plan) for DisableAuth tests
PLANS_OUT="$TMP_DIR/plans.json"
curl -sS -m 20 -o "$PLANS_OUT" "$BASE_URL/api/Admin/SubscriptionPlan?includeInactive=true" || true
GOLD_ID="$(python3 - "$PLANS_OUT" <<'PY'
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
PROFILE_OUT="$TMP_DIR/profile.json"
curl -sS -m 15 -o "$PROFILE_OUT" "$BASE_URL/api/User/profile" || true
USER_ID="$(json_get "$PROFILE_OUT" data.id)"
if [[ -n "$USER_ID" && -n "$GOLD_ID" ]]; then
  ASSIGN_OUT="$TMP_DIR/assign.json"
  curl -sS -m 20 -o "$ASSIGN_OUT" -X POST "$BASE_URL/api/Admin/UserSubscription/assign" \
    -H 'Content-Type: application/json' \
    -d "{\"userId\":$USER_ID,\"subscriptionPlanId\":$GOLD_ID}" >/dev/null || true
  echo "      ensured subscription plan=$GOLD_ID for user=$USER_ID"
fi

http_ok() {
  local code="$1"
  [[ "$code" == "200" || "$code" == "201" ]]
}

# 1) create draft Birthday
CREATE_OUT="$TMP_DIR/create.json"
code="$(http_json POST /api/AutomatedMessage/create-draft '{"automationType":"Birthday"}' "$CREATE_OUT")"
ok="$(json_get "$CREATE_OUT" success)"
AM_ID="$(json_get "$CREATE_OUT" data.id)"
check "POST create-draft → 200/201" "$(http_ok "$code" && echo 1 || echo 0)"
check "create-draft success=true" "$([[ "$ok" == "true" ]] && echo 1 || echo 0)"
check "create-draft returns id" "$([[ -n "$AM_ID" && "$AM_ID" != "0" ]] && echo 1 || echo 0)"
if [[ -n "$AM_ID" && "$AM_ID" != "0" ]]; then
  CREATED_IDS+=("$AM_ID")
fi
echo "      draft id=$AM_ID"

# 2) get by id
GET_OUT="$TMP_DIR/get.json"
code="$(http_json GET "/api/AutomatedMessage/$AM_ID" "" "$GET_OUT")"
ok="$(json_get "$GET_OUT" success)"
active="$(json_get "$GET_OUT" data.isActive)"
check "GET by id → 200" "$([[ "$code" == "200" ]] && echo 1 || echo 0)"
check "GET success + isActive=true" "$([[ "$ok" == "true" && "$active" == "true" ]] && echo 1 || echo 0)"

# 3) toggle off
TOGGLE_OFF="$TMP_DIR/toggle_off.json"
code="$(http_json POST "/api/AutomatedMessage/$AM_ID/toggle-status" '{"isActive":false}' "$TOGGLE_OFF")"
ok="$(json_get "$TOGGLE_OFF" success)"
active="$(json_get "$TOGGLE_OFF" data.isActive)"
status="$(json_get "$TOGGLE_OFF" data.status)"
check "POST toggle-status false → 200/201" "$(http_ok "$code" && echo 1 || echo 0)"
check "toggle off: success + isActive=false + status=Paused" \
  "$([[ "$ok" == "true" && "$active" == "false" && "$status" == "Paused" ]] && echo 1 || echo 0)"

# 4) toggle on
TOGGLE_ON="$TMP_DIR/toggle_on.json"
code="$(http_json POST "/api/AutomatedMessage/$AM_ID/toggle-status" '{"isActive":true}' "$TOGGLE_ON")"
ok="$(json_get "$TOGGLE_ON" success)"
active="$(json_get "$TOGGLE_ON" data.isActive)"
status="$(json_get "$TOGGLE_ON" data.status)"
check "POST toggle-status true → 200/201" "$(http_ok "$code" && echo 1 || echo 0)"
check "toggle on: success + isActive=true + status=Active" \
  "$([[ "$ok" == "true" && "$active" == "true" && "$status" == "Active" ]] && echo 1 || echo 0)"

# 5) cancel (pause without delete)
CANCEL_OUT="$TMP_DIR/cancel.json"
code="$(http_json POST "/api/AutomatedMessage/$AM_ID/cancel" "" "$CANCEL_OUT")"
ok="$(json_get "$CANCEL_OUT" success)"
active="$(json_get "$CANCEL_OUT" data.isActive)"
deleted="$(json_get "$CANCEL_OUT" data.isDeleted)"
status="$(json_get "$CANCEL_OUT" data.status)"
check "POST cancel → 200/201" "$(http_ok "$code" && echo 1 || echo 0)"
check "cancel: success + inactive + not deleted + Paused" \
  "$([[ "$ok" == "true" && "$active" == "false" && "$deleted" == "false" && "$status" == "Paused" ]] && echo 1 || echo 0)"

# 6) still gettable after cancel
GET2_OUT="$TMP_DIR/get2.json"
code="$(http_json GET "/api/AutomatedMessage/$AM_ID" "" "$GET2_OUT")"
ok="$(json_get "$GET2_OUT" success)"
check "GET after cancel still works" "$([[ "$code" == "200" && "$ok" == "true" ]] && echo 1 || echo 0)"

# 7) delete
DELETE_OUT="$TMP_DIR/delete.json"
code="$(http_json POST "/api/AutomatedMessage/$AM_ID/delete" "" "$DELETE_OUT")"
ok="$(json_get "$DELETE_OUT" success)"
deleted="$(json_get "$DELETE_OUT" data.isDeleted)"
active="$(json_get "$DELETE_OUT" data.isActive)"
check "POST delete → 200/201" "$(http_ok "$code" && echo 1 || echo 0)"
check "delete: success + isDeleted=true + isActive=false" \
  "$([[ "$ok" == "true" && "$deleted" == "true" && "$active" == "false" ]] && echo 1 || echo 0)"
# از cleanup حذف نکن — قبلاً حذف شده
CREATED_IDS=()

# 8) get after delete → 404
GET3_OUT="$TMP_DIR/get3.json"
code="$(http_json GET "/api/AutomatedMessage/$AM_ID" "" "$GET3_OUT")"
ok="$(json_get "$GET3_OUT" success)"
err="$(json_get "$GET3_OUT" errorCode)"
check "GET after delete → 404 NOT_FOUND" \
  "$([[ "$code" == "404" && "$ok" == "false" && "$err" == "NOT_FOUND" ]] && echo 1 || echo 0)"

# 9) delete again → 404
DEL2_OUT="$TMP_DIR/delete2.json"
code="$(http_json POST "/api/AutomatedMessage/$AM_ID/delete" "" "$DEL2_OUT")"
ok="$(json_get "$DEL2_OUT" success)"
err="$(json_get "$DEL2_OUT" errorCode)"
check "POST delete again → 404" \
  "$([[ "$code" == "404" && "$ok" == "false" && "$err" == "NOT_FOUND" ]] && echo 1 || echo 0)"

# 10) cancel missing → 404
CAN2_OUT="$TMP_DIR/cancel2.json"
code="$(http_json POST "/api/AutomatedMessage/999999999/cancel" "" "$CAN2_OUT")"
ok="$(json_get "$CAN2_OUT" success)"
err="$(json_get "$CAN2_OUT" errorCode)"
check "POST cancel missing id → 404" \
  "$([[ "$code" == "404" && "$ok" == "false" && "$err" == "NOT_FOUND" ]] && echo 1 || echo 0)"

# 11) create second draft then delete
CREATE2_OUT="$TMP_DIR/create2.json"
code="$(http_json POST /api/AutomatedMessage/create-draft '{"automationType":"Welcome"}' "$CREATE2_OUT")"
AM2="$(json_get "$CREATE2_OUT" data.id)"
ok="$(json_get "$CREATE2_OUT" success)"
check "create second draft Welcome" "$(http_ok "$code" && [[ "$ok" == "true" && -n "$AM2" ]] && echo 1 || echo 0)"
CREATED_IDS+=("$AM2")

DEL3_OUT="$TMP_DIR/delete3.json"
code="$(http_json POST "/api/AutomatedMessage/$AM2/delete" "" "$DEL3_OUT")"
ok="$(json_get "$DEL3_OUT" success)"
deleted="$(json_get "$DEL3_OUT" data.isDeleted)"
check "delete second draft succeeds" \
  "$(http_ok "$code" && [[ "$ok" == "true" && "$deleted" == "true" ]] && echo 1 || echo 0)"
CREATED_IDS=()

# 12) create for empty-body toggle handling
CREATE3_OUT="$TMP_DIR/create3.json"
http_json POST /api/AutomatedMessage/create-draft '{"automationType":"Custom"}' "$CREATE3_OUT" >/dev/null
AM3="$(json_get "$CREATE3_OUT" data.id)"
CREATED_IDS+=("$AM3")
VAL2_OUT="$TMP_DIR/val2.json"
code="$(curl -sS -m 20 -o "$VAL2_OUT" -w '%{http_code}' -X POST "$BASE_URL/api/AutomatedMessage/$AM3/toggle-status" \
  -H 'Content-Type: application/json' -d '{}' || echo 000)"
check "toggle with empty object handled (200/400)" \
  "$([[ "$code" == "200" || "$code" == "400" ]] && echo 1 || echo 0)"

# cleanup AM3
http_json POST "/api/AutomatedMessage/$AM3/delete" "" "$TMP_DIR/cleanup3.json" >/dev/null || true
CREATED_IDS=()

echo "=== Result: PASS=$PASS FAIL=$FAIL ==="
[[ "$FAIL" -eq 0 ]]
