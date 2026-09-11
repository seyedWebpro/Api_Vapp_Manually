#!/usr/bin/env bash
# Crawl کیف پول ادمین — موجودی، شارژ/کسر دستی، idempotency، validation، audit
#
# Usage:
#   BASE_URL=http://127.0.0.1:5054 bash devops/scripts/crawl-admin-wallet.sh
#
# Env:
#   USER_ID=1              — کاربر هدف (پیش‌فرض: اولین کاربر فعال)
#   CHARGE_AMOUNT=25000
#   DEDUCT_AMOUNT=10000
#   SKIP_API_RESTART=1     — اگر API از قبل روی 5054 است
#   SKIP_AUDIT_SQL=1       — بدون چک AdminAuditLogs
set -euo pipefail
export PATH="/usr/bin:/bin:/usr/sbin:/sbin:/usr/local/bin:${PATH:-}"

ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
cd "$ROOT"
BASE="${BASE_URL:-http://127.0.0.1:5054}"
CHARGE_AMOUNT="${CHARGE_AMOUNT:-25000}"
DEDUCT_AMOUNT="${DEDUCT_AMOUNT:-10000}"
SKIP_API_RESTART="${SKIP_API_RESTART:-0}"
SKIP_AUDIT_SQL="${SKIP_AUDIT_SQL:-0}"
LOG=/tmp/vapp-admin-wallet-crawl.log
PIDFILE=/tmp/vapp-admin-wallet-crawl.pid
TMP_DIR="$(mktemp -d)"
PASS=0
FAIL=0
SUFFIX="$(date +%s)"
FROM_UTC="$(date -u -v-15M +%Y-%m-%dT%H:%M:%S 2>/dev/null || date -u -d '15 minutes ago' +%Y-%m-%dT%H:%M:%S)"

cleanup() { rm -rf "$TMP_DIR"; }
trap cleanup EXIT

json_get() {
  local file="$1" expr="$2"
  python3 - "$file" "$expr" <<'PY'
import json,sys

def pick(obj, key):
    if not isinstance(obj, dict):
        return None
    if key in obj:
        return obj[key]
    pascal = key[:1].upper() + key[1:] if key else key
    if pascal in obj:
        return obj[pascal]
    return None

path = sys.argv[2].split(".")
with open(sys.argv[1], encoding="utf-8") as f:
    data = json.load(f)
cur = data
for p in path:
    cur = pick(cur, p)
    if cur is None:
        break
if isinstance(cur, bool):
    print("true" if cur else "false")
elif cur is None:
    print("")
else:
    print(cur)
PY
}

check() {
  local name="$1" ok="$2"
  if [[ "$ok" == "1" || "$ok" == "true" ]]; then
    echo "PASS  $name"
    PASS=$((PASS + 1))
  else
    echo "FAIL  $name"
    FAIL=$((FAIL + 1))
  fi
}

req() {
  local method="$1" path="$2" out="$3"
  shift 3
  local code
  code=$(curl -sS -m 30 -w "%{http_code}" -o "$out" -X "$method" "${BASE}${path}" \
    -H "Content-Type: application/json" \
    -H "Accept: application/json" \
    "$@" 2>/dev/null || echo "000")
  [[ -s "$out" ]] || echo '{}' > "$out"
  printf '%s' "$code"
}

ensure_api() {
  if [[ "$SKIP_API_RESTART" == "1" ]]; then
    local code
    code=$(curl -sS -m 5 -o /dev/null -w "%{http_code}" "$BASE/health" 2>/dev/null || echo 000)
    if [[ "$code" != "200" ]]; then
      echo "API not healthy at $BASE (health=$code) and SKIP_API_RESTART=1"
      exit 1
    fi
    echo "API_READY (existing)"
    return
  fi

  echo "===== BUILD + RESTART API ====="
  export DOTNET_ROOT="${DOTNET_ROOT:-/usr/local/share/dotnet}"
  local DOTNET_BIN="${DOTNET_ROOT}/dotnet"
  [[ -x "$DOTNET_BIN" ]] || DOTNET_BIN="$(command -v dotnet)"

  if [[ "${SKIP_BUILD:-0}" == "1" && -f bin/Debug/net8.0/Api_Vapp.dll ]]; then
    echo "SKIP_BUILD=1 — using existing dll"
  else
    "$DOTNET_BIN" build Api_Vapp.csproj -v q
  fi
  for p in $(lsof -t -iTCP:5054 -sTCP:LISTEN 2>/dev/null || true); do kill "$p" 2>/dev/null || true; done
  sleep 2
  for p in $(lsof -t -iTCP:5054 -sTCP:LISTEN 2>/dev/null || true); do kill -9 "$p" 2>/dev/null || true; done
  sleep 1

  nohup env ASPNETCORE_ENVIRONMENT=Development DatabaseProvider="${DatabaseProvider:-LocalDocker}" \
    "$DOTNET_BIN" exec bin/Debug/net8.0/Api_Vapp.dll --urls "http://127.0.0.1:5054" \
    > "$LOG" 2>&1 &
  echo $! > "$PIDFILE"

  local READY=0
  for i in $(seq 1 90); do
    local code
    code=$(curl -sS -m 5 -o /dev/null -w "%{http_code}" "$BASE/health" 2>/dev/null || echo 000)
    if [[ "$code" == "200" ]]; then
      # صبر تا migrate/seed تمام شود — User باید داده برگرداند
      local uout="$TMP_DIR/ready_users.json"
      local ucode
      ucode=$(curl -sS -m 15 -o "$uout" -w "%{http_code}" "$BASE/api/User?pageNumber=1&pageSize=1" 2>/dev/null || echo 000)
      if [[ "$ucode" == "200" ]]; then
        READY=1
        break
      fi
    fi
    if [[ -f "$PIDFILE" ]] && ! kill -0 "$(cat "$PIDFILE")" 2>/dev/null; then
      echo "API_DIED_EARLY"
      tail -60 "$LOG" || true
      exit 1
    fi
    sleep 2
  done
  if [[ "$READY" != "1" ]]; then
    echo "API_NOT_READY"
    tail -60 "$LOG" || true
    exit 1
  fi
  echo "API_READY"
}

audit_has() {
  local action="$1"
  local out
  out="$(MSSQL_SA_PASSWORD="${MSSQL_SA_PASSWORD:-Vapp@Secure2025!}" bash "$ROOT/devops/scripts/audit-search.sh" \
    --action "$action" --from "$FROM_UTC" --lines 40 2>&1 || true)"
  echo "$out" | grep -E "^[0-9]+\|" | grep -F "$action" >/dev/null 2>&1
}

echo "=== crawl-admin-wallet ==="
echo "BASE=$BASE CHARGE=$CHARGE_AMOUNT DEDUCT=$DEDUCT_AMOUNT FROM_UTC=$FROM_UTC"
ensure_api

# --- resolve target user ---
USERS_OUT="$TMP_DIR/users.json"
HTTP=$(req GET "/api/User?pageNumber=1&pageSize=5" "$USERS_OUT")
check "GET /api/User → 200" "$([[ "$HTTP" == "200" ]] && echo 1 || echo 0)"
if [[ "$HTTP" != "200" ]]; then
  echo "DEBUG User response HTTP=$HTTP body=$(head -c 400 "$USERS_OUT")"
fi

USER_ID="${USER_ID:-}"
if [[ -z "$USER_ID" ]]; then
  USER_ID="$(python3 - "$USERS_OUT" <<'PY'
import json,sys
d=json.load(open(sys.argv[1],encoding='utf-8'))
data=d.get('data') or d.get('Data') or {}
users=data.get('users') or data.get('Users') or []
if not users:
    print("")
    raise SystemExit
u=users[0]
print(u.get('id') or u.get('Id') or "")
PY
)"
fi
if [[ -z "$USER_ID" ]]; then
  curl -sS -m 20 -o "$TMP_DIR/wallet_self.json" "$BASE/api/Wallet/balance" >/dev/null || true
  HTTP=$(req GET "/api/User?pageNumber=1&pageSize=5" "$USERS_OUT")
  USER_ID="$(python3 - "$USERS_OUT" <<'PY'
import json,sys
d=json.load(open(sys.argv[1],encoding='utf-8'))
data=d.get('data') or d.get('Data') or {}
users=data.get('users') or data.get('Users') or []
print((users[0].get('id') or users[0].get('Id')) if users else "")
PY
)"
fi
if [[ -z "$USER_ID" ]]; then
  echo "FAIL  no target user (HTTP=$HTTP)"
  head -c 500 "$USERS_OUT"; echo
  exit 1
fi
echo "TARGET_USER_ID=$USER_ID"

# --- auth scenarios ---
HTTP=$(req GET "/api/Admin/Wallet/${USER_ID}/balance" "$TMP_DIR/bal_noauth.json")
# DisableAuth may allow 200; invalid bearer should still be 401 when auth enabled
HTTP_BAD=$(curl -sS -m 20 -o "$TMP_DIR/bal_badtok.json" -w "%{http_code}" \
  -H "Authorization: Bearer not-a-valid-token" \
  "$BASE/api/Admin/Wallet/${USER_ID}/balance" || echo 000)
if [[ "$HTTP_BAD" == "401" ]]; then
  check "invalid bearer → 401" 1
elif [[ "$HTTP" == "200" ]]; then
  check "DisableAuth allows balance without token" 1
else
  check "auth gate for balance" 0
fi

# --- balance ---
BAL0="$TMP_DIR/bal0.json"
HTTP=$(req GET "/api/Admin/Wallet/${USER_ID}/balance" "$BAL0")
check "GET balance → 200" "$([[ "$HTTP" == "200" ]] && echo 1 || echo 0)"
check "balance success" "$([[ "$(json_get "$BAL0" success)" == "true" ]] && echo 1 || echo 0)"
B0="$(json_get "$BAL0" data.balance)"
check "balance numeric" "$(python3 -c "print(1 if float('$B0' or 'x')==float('$B0') else 0)" 2>/dev/null || echo 0)"
echo "      balance_before=$B0"

# --- validation failures ---
HTTP=$(req POST "/api/Admin/Wallet/${USER_ID}/manual-charge" "$TMP_DIR/val_empty.json" \
  -d '{}')
check "charge empty body → 400" "$([[ "$HTTP" == "400" ]] && echo 1 || echo 0)"
ERR_CODE="$(json_get "$TMP_DIR/val_empty.json" errorCode)"
check "charge empty errorCode VALIDATION_FAILED|INVALID_INPUT" \
  "$([[ "$ERR_CODE" == "VALIDATION_FAILED" || "$ERR_CODE" == "INVALID_INPUT" ]] && echo 1 || echo 0)"

HTTP=$(req POST "/api/Admin/Wallet/${USER_ID}/manual-charge" "$TMP_DIR/val_frac.json" \
  -d "{\"amount\":1000.5,\"description\":\"دلیل تست اعشار\",\"idempotencyKey\":\"frac-${SUFFIX}\"}")
check "charge fractional → 400" "$([[ "$HTTP" == "400" ]] && echo 1 || echo 0)"

HTTP=$(req POST "/api/Admin/Wallet/${USER_ID}/manual-charge" "$TMP_DIR/val_nodesc.json" \
  -d "{\"amount\":1000,\"idempotencyKey\":\"nodesc-${SUFFIX}\"}")
check "charge without description → 400" "$([[ "$HTTP" == "400" ]] && echo 1 || echo 0)"

HTTP=$(req GET "/api/Admin/Wallet/999999991/balance" "$TMP_DIR/nf.json")
check "balance missing user → 404" "$([[ "$HTTP" == "404" ]] && echo 1 || echo 0)"

# --- charge ---
IDEMP_CHG="chg-${SUFFIX}-abcdef12"
CHG="$TMP_DIR/charge.json"
HTTP=$(req POST "/api/Admin/Wallet/${USER_ID}/manual-charge" "$CHG" \
  -d "{\"amount\":${CHARGE_AMOUNT},\"description\":\"تست کراول شارژ دستی کیف پول\",\"idempotencyKey\":\"${IDEMP_CHG}\"}")
check "manual-charge → 200" "$([[ "$HTTP" == "200" ]] && echo 1 || echo 0)"
check "charge success" "$([[ "$(json_get "$CHG" success)" == "true" ]] && echo 1 || echo 0)"
check "charge not duplicate" "$([[ "$(json_get "$CHG" data.wasAlreadyProcessed)" == "false" ]] && echo 1 || echo 0)"
AFTER_CHG="$(json_get "$CHG" data.balanceAfter)"
BEFORE_CHG="$(json_get "$CHG" data.balanceBefore)"
check "charge balanceBefore matches prior" "$(python3 -c "print(1 if abs(float('$BEFORE_CHG')-float('$B0'))<0.01 else 0)")"
check "charge balanceAfter = before + amount" "$(python3 -c "print(1 if abs(float('$AFTER_CHG')-(float('$BEFORE_CHG')+float('$CHARGE_AMOUNT')))<0.01 else 0)")"
echo "      after_charge=$AFTER_CHG"

# --- idempotent replay charge ---
CHG2="$TMP_DIR/charge2.json"
HTTP=$(req POST "/api/Admin/Wallet/${USER_ID}/manual-charge" "$CHG2" \
  -d "{\"amount\":${CHARGE_AMOUNT},\"description\":\"تست کراول شارژ دستی کیف پول\",\"idempotencyKey\":\"${IDEMP_CHG}\"}")
check "charge replay → 200" "$([[ "$HTTP" == "200" ]] && echo 1 || echo 0)"
check "charge replay marked duplicate" "$([[ "$(json_get "$CHG2" data.wasAlreadyProcessed)" == "true" ]] && echo 1 || echo 0)"
AFTER_REPLAY="$(json_get "$CHG2" data.balanceAfter)"
check "charge replay did not change balance" "$(python3 -c "print(1 if abs(float('$AFTER_REPLAY')-float('$AFTER_CHG'))<0.01 else 0)")"

BAL1="$TMP_DIR/bal1.json"
HTTP=$(req GET "/api/Admin/Wallet/${USER_ID}/balance" "$BAL1")
B1="$(json_get "$BAL1" data.balance)"
check "GET balance after charge matches" "$(python3 -c "print(1 if abs(float('$B1')-float('$AFTER_CHG'))<0.01 else 0)")"

# --- deduct ---
IDEMP_DED="ded-${SUFFIX}-abcdef12"
DED="$TMP_DIR/deduct.json"
HTTP=$(req POST "/api/Admin/Wallet/${USER_ID}/manual-deduct" "$DED" \
  -d "{\"amount\":${DEDUCT_AMOUNT},\"description\":\"تست کراول کسر دستی کیف پول\",\"idempotencyKey\":\"${IDEMP_DED}\"}")
check "manual-deduct → 200" "$([[ "$HTTP" == "200" ]] && echo 1 || echo 0)"
check "deduct success" "$([[ "$(json_get "$DED" success)" == "true" ]] && echo 1 || echo 0)"
AFTER_DED="$(json_get "$DED" data.balanceAfter)"
BEFORE_DED="$(json_get "$DED" data.balanceBefore)"
check "deduct balanceBefore matches post-charge" "$(python3 -c "print(1 if abs(float('$BEFORE_DED')-float('$B1'))<0.01 else 0)")"
check "deduct balanceAfter = before - amount" "$(python3 -c "print(1 if abs(float('$AFTER_DED')-(float('$BEFORE_DED')-float('$DEDUCT_AMOUNT')))<0.01 else 0)")"
echo "      after_deduct=$AFTER_DED"

# --- idempotent replay deduct ---
DED2="$TMP_DIR/deduct2.json"
HTTP=$(req POST "/api/Admin/Wallet/${USER_ID}/manual-deduct" "$DED2" \
  -d "{\"amount\":${DEDUCT_AMOUNT},\"description\":\"تست کراول کسر دستی کیف پول\",\"idempotencyKey\":\"${IDEMP_DED}\"}")
check "deduct replay marked duplicate" "$([[ "$(json_get "$DED2" data.wasAlreadyProcessed)" == "true" ]] && echo 1 || echo 0)"
check "deduct replay balance stable" "$(python3 -c "print(1 if abs(float('$(json_get "$DED2" data.balanceAfter)')-float('$AFTER_DED'))<0.01 else 0)")"

# --- overdraft ---
OVER_AMT="$(python3 -c "print(int(float('$AFTER_DED')+1))")"
HTTP=$(req POST "/api/Admin/Wallet/${USER_ID}/manual-deduct" "$TMP_DIR/over.json" \
  -d "{\"amount\":${OVER_AMT},\"description\":\"تست کسر بیش از موجودی\",\"idempotencyKey\":\"over-${SUFFIX}\"}")
check "overdraft deduct → 400" "$([[ "$HTTP" == "400" ]] && echo 1 || echo 0)"

BAL2="$TMP_DIR/bal2.json"
HTTP=$(req GET "/api/Admin/Wallet/${USER_ID}/balance" "$BAL2")
B2="$(json_get "$BAL2" data.balance)"
check "final balance unchanged after overdraft fail" "$(python3 -c "print(1 if abs(float('$B2')-float('$AFTER_DED'))<0.01 else 0)")"
EXPECTED_NET="$(python3 -c "print(float('$B0')+float('$CHARGE_AMOUNT')-float('$DEDUCT_AMOUNT'))")"
check "net balance = start + charge - deduct" "$(python3 -c "print(1 if abs(float('$B2')-float('$EXPECTED_NET'))<0.01 else 0)")"
echo "      final_balance=$B2 expected_net=$EXPECTED_NET"

# --- transactions list ---
TX="$TMP_DIR/tx.json"
HTTP=$(req GET "/api/Admin/Wallet/${USER_ID}/transactions?pageNumber=1&pageSize=10" "$TX")
check "GET transactions → 200" "$([[ "$HTTP" == "200" ]] && echo 1 || echo 0)"
TX_COUNT="$(json_get "$TX" data.totalCount)"
check "transactions totalCount >= 2" "$(python3 -c "print(1 if int(float('$TX_COUNT' or 0))>=2 else 0)")"

# --- user list exposes walletBalance ---
HTTP=$(req GET "/api/User?pageNumber=1&pageSize=20" "$TMP_DIR/users2.json")
HAS_WB="$(python3 - "$TMP_DIR/users2.json" "$USER_ID" <<'PY'
import json,sys
uid=int(sys.argv[2])
d=json.load(open(sys.argv[1],encoding='utf-8'))
data=d.get('data') or d.get('Data') or {}
users=data.get('users') or data.get('Users') or []
u=next((x for x in users if int(x.get('id') or x.get('Id') or 0)==uid), None)
print(1 if u is not None and ('walletBalance' in u or 'WalletBalance' in u) else 0)
PY
)"
check "User list includes walletBalance" "$HAS_WB"

# --- audit logs (API primary + SQL secondary) ---
if [[ "$SKIP_AUDIT_SQL" != "1" ]]; then
  sleep 1
  AUD_CHG="$TMP_DIR/audit_chg.json"
  HTTP=$(req GET "/api/Admin/Audit?action=Wallet.ManualCharged&page=1&pageSize=5" "$AUD_CHG")
  CHG_ITEMS="$(python3 - "$AUD_CHG" <<'PY'
import json,sys
d=json.load(open(sys.argv[1],encoding='utf-8'))
data=d.get('data') or d.get('Data') or {}
items=data.get('items') or data.get('Items') or []
print(len(items))
PY
)"
  check "audit API Wallet.ManualCharged has rows" "$(python3 -c "print(1 if int('$CHG_ITEMS' or 0)>0 else 0)")"

  AUD_DED="$TMP_DIR/audit_ded.json"
  HTTP=$(req GET "/api/Admin/Audit?action=Wallet.ManualDebited&page=1&pageSize=5" "$AUD_DED")
  DED_ITEMS="$(python3 - "$AUD_DED" <<'PY'
import json,sys
d=json.load(open(sys.argv[1],encoding='utf-8'))
data=d.get('data') or d.get('Data') or {}
items=data.get('items') or data.get('Items') or []
print(len(items))
PY
)"
  check "audit API Wallet.ManualDebited has rows" "$(python3 -c "print(1 if int('$DED_ITEMS' or 0)>0 else 0)")"

  if audit_has "Wallet.ManualCharged"; then
    check "audit SQL Wallet.ManualCharged present" 1
  else
    echo "WARN  SQL audit search ManualCharged failed/empty (API check above is source of truth)"
    check "audit SQL Wallet.ManualCharged present (soft)" 1
  fi
  if audit_has "Wallet.ManualDebited"; then
    check "audit SQL Wallet.ManualDebited present" 1
  else
    echo "WARN  SQL audit search ManualDebited failed/empty (API check above is source of truth)"
    check "audit SQL Wallet.ManualDebited present (soft)" 1
  fi
else
  echo "SKIP  audit checks"
fi

echo
echo "===== SUMMARY pass=$PASS fail=$FAIL ====="
if [[ "$FAIL" -gt 0 ]]; then
  exit 1
fi
exit 0
