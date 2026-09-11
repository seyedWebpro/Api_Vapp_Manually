#!/usr/bin/env bash
# Crawl هاب دارایی کاربر (Admin UserInventory) — خلاصه، قالب، محتوا، view-link همهٔ حالت‌ها
#
# Usage:
#   BASE_URL=http://127.0.0.1:5054 bash devops/scripts/crawl-admin-user-inventory.sh
#   SKIP_API_RESTART=1 USER_ID=1 bash devops/scripts/crawl-admin-user-inventory.sh
#
set -euo pipefail
export PATH="/usr/bin:/bin:/usr/sbin:/sbin:/usr/local/bin:${PATH:-}"

ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
cd "$ROOT"
BASE="${BASE_URL:-http://127.0.0.1:5054}"
SKIP_API_RESTART="${SKIP_API_RESTART:-0}"
LOG=/tmp/vapp-admin-user-inventory-crawl.log
PIDFILE=/tmp/vapp-admin-user-inventory-crawl.pid
TMP_DIR="$(mktemp -d)"
PASS=0
FAIL=0

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
  code=$(curl -sS -m 45 -w "%{http_code}" -o "$out" -X "$method" "${BASE}${path}" \
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

  "$DOTNET_BIN" build Api_Vapp.csproj -v q
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
      READY=1
      break
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

echo "=== crawl-admin-user-inventory ==="
echo "BASE=$BASE"
ensure_api

USERS_OUT="$TMP_DIR/users.json"
HTTP=$(req GET "/api/User?pageNumber=1&pageSize=5" "$USERS_OUT")
check "GET /api/User → 200" "$([[ "$HTTP" == "200" ]] && echo 1 || echo 0)"

USER_ID="${USER_ID:-}"
if [[ -z "$USER_ID" ]]; then
  USER_ID="$(python3 - "$USERS_OUT" <<'PY'
import json,sys
d=json.load(open(sys.argv[1],encoding='utf-8'))
data=d.get('data') or d.get('Data') or {}
users=data.get('users') or data.get('Users') or []
print(users[0].get('id') or users[0].get('Id') if users else "")
PY
)"
fi
if [[ -z "$USER_ID" ]]; then
  echo "FAIL  no target user"
  exit 1
fi
echo "USER_ID=$USER_ID"

# --- summary ---
HTTP=$(req GET "/api/Admin/UserInventory/${USER_ID}/summary" "$TMP_DIR/sum.json")
check "summary → 200" "$([[ "$HTTP" == "200" && "$(json_get "$TMP_DIR/sum.json" success)" == "true" ]] && echo 1 || echo 0)"
TPL_COUNT="$(json_get "$TMP_DIR/sum.json" data.templatesCount)"
CNT_COUNT="$(json_get "$TMP_DIR/sum.json" data.contentsCount)"
WHEEL_COUNT="$(json_get "$TMP_DIR/sum.json" data.luckyWheelsCount)"
echo "  templates=$TPL_COUNT contents=$CNT_COUNT wheels=$WHEEL_COUNT"
check "summary has phone" "$([[ -n "$(json_get "$TMP_DIR/sum.json" data.phoneNumber)" ]] && echo 1 || echo 0)"

HTTP=$(req GET "/api/Admin/UserInventory/0/summary" "$TMP_DIR/bad0.json")
check "summary userId=0 → 400 INVALID_USER_ID" "$([[ "$HTTP" == "400" && "$(json_get "$TMP_DIR/bad0.json" errorCode)" == "INVALID_USER_ID" ]] && echo 1 || echo 0)"

HTTP=$(req GET "/api/Admin/UserInventory/999999/summary" "$TMP_DIR/nf.json")
check "summary missing → 404" "$([[ "$HTTP" == "404" ]] && echo 1 || echo 0)"

# --- templates (no content field) ---
HTTP=$(req GET "/api/Admin/UserInventory/${USER_ID}/templates?page=1&pageSize=20" "$TMP_DIR/tpl.json")
check "templates → 200" "$([[ "$HTTP" == "200" && "$(json_get "$TMP_DIR/tpl.json" success)" == "true" ]] && echo 1 || echo 0)"
python3 - "$TMP_DIR/tpl.json" <<'PY'
import json,sys
d=json.load(open(sys.argv[1],encoding='utf-8'))
items=(d.get('data') or {}).get('items') or []
open(sys.argv[1]+".ok","w").write("1" if all("content" not in i and "Content" not in i for i in items) else "0")
open(sys.argv[1]+".path","w").write("1" if all(str(i.get("adminViewPath") or "").startswith("/admin/template-approvals") for i in items) or not items else "0")
print("tpl_items", len(items))
PY
check "templates omit content body" "$(cat "$TMP_DIR/tpl.json.ok")"
check "templates adminViewPath set" "$(cat "$TMP_DIR/tpl.json.path")"

# --- contents all + matrix ---
HTTP=$(req GET "/api/Admin/UserInventory/${USER_ID}/contents?page=1&pageSize=100" "$TMP_DIR/all.json")
check "contents → 200" "$([[ "$HTTP" == "200" && "$(json_get "$TMP_DIR/all.json" success)" == "true" ]] && echo 1 || echo 0)"

python3 - "$TMP_DIR/all.json" "$TMP_DIR" <<'PY'
import json,sys
d=json.load(open(sys.argv[1],encoding='utf-8'))
items=(d.get('data') or {}).get('items') or []
out=sys.argv[2]

def write(name, val):
    open(f"{out}/{name}","w").write(str(val))

# drafts cannot view
drafts=[i for i in items if i.get("publishStatus")=="Draft"]
write("draft_ok", "1" if all(not i.get("canView") for i in drafts) or not drafts else "0")
write("draft_n", len(drafts))

# public = approved+active absolute url
publics=[i for i in items if i.get("viewMode")=="Public"]
write("public_ok", "1" if all(
    i.get("canView") and i.get("approvalStatus")=="Approved" and i.get("isActive")
    and str(i.get("publicUrl") or "").startswith("http")
    for i in publics) or not publics else "0")
write("public_n", len(publics))

# admin preview = published visual not (approved+active)
previews=[i for i in items if i.get("viewMode")=="AdminPreview"]
write("preview_ok", "1" if all(i.get("canView") and i.get("publishStatus")=="Published" for i in previews) or not previews else "0")
write("preview_n", len(previews))

# pick samples for view-link
for mode, fname in [("Public","sample_public"),("AdminPreview","sample_preview"),("External","sample_external"),("AdminPage","sample_admin")]:
    hit=next((i for i in items if i.get("viewMode")==mode and i.get("canView")), None)
    if hit:
        open(f"{out}/{fname}","w").write(f"{hit['itemType']} {hit['id']}")
    else:
        open(f"{out}/{fname}","w").write("")

# bank/quickaction should have contentPreview
smsish=[i for i in items if i.get("itemType") in ("BankAccount","QuickAction")]
write("sms_preview_ok", "1" if all((i.get("contentPreview") or "").strip() for i in smsish) or not smsish else "0")
write("sms_n", len(smsish))

# types present map
types=sorted({i.get("itemType") for i in items})
open(f"{out}/types","w").write(",".join(types))
print("types", types)
print("counts draft=%s public=%s preview=%s sms=%s" % (len(drafts), len(publics), len(previews), len(smsish)))
PY

check "drafts cannot view" "$(cat "$TMP_DIR/draft_ok")"
check "Public mode is approved+active+http" "$(cat "$TMP_DIR/public_ok")"
check "AdminPreview only published" "$(cat "$TMP_DIR/preview_ok")"
check "Bank/QuickAction have contentPreview" "$(cat "$TMP_DIR/sms_preview_ok")"
echo "  types=$(cat "$TMP_DIR/types")"
echo "  draft=$(cat "$TMP_DIR/draft_n") public=$(cat "$TMP_DIR/public_n") preview=$(cat "$TMP_DIR/preview_n") sms=$(cat "$TMP_DIR/sms_n")"

# invalid itemType
HTTP=$(req GET "/api/Admin/UserInventory/${USER_ID}/contents?itemType=Nope" "$TMP_DIR/badtype.json")
check "bad itemType → 400" "$([[ "$HTTP" == "400" && "$(json_get "$TMP_DIR/badtype.json" errorCode)" == "INVALID_INPUT" ]] && echo 1 || echo 0)"

# filter each visual type
for T in LuckyWheel BusinessCard UserForm BookingSystem SocialMediaLink QuickAction BankAccount; do
  HTTP=$(req GET "/api/Admin/UserInventory/${USER_ID}/contents?itemType=${T}&page=1&pageSize=50" "$TMP_DIR/f_${T}.json")
  ok="$(python3 - "$TMP_DIR/f_${T}.json" "$T" <<'PY'
import json,sys
d=json.load(open(sys.argv[1],encoding='utf-8'))
want=sys.argv[2]
items=(d.get('data') or {}).get('items') or []
print("1" if d.get("success") and all(i.get("itemType")==want for i in items) else "0")
PY
)"
  check "filter $T" "$([[ "$HTTP" == "200" && "$ok" == "1" ]] && echo 1 || echo 0)"
done

# --- view-link matrix ---
test_view_link() {
  local label="$1" sample_file="$2" expect_mode="$3"
  local sample
  sample="$(cat "$sample_file" 2>/dev/null || true)"
  if [[ -z "$sample" ]]; then
    echo "SKIP  view-link $label (no sample)"
    check "view-link $label skipped" 1
    return
  fi
  local itype iid
  read -r itype iid <<< "$sample"
  HTTP=$(req POST "/api/Admin/UserInventory/${USER_ID}/contents/${itype}/${iid}/view-link" "$TMP_DIR/vl_${label}.json")
  local mode url path admin
  mode="$(json_get "$TMP_DIR/vl_${label}.json" data.viewMode)"
  url="$(json_get "$TMP_DIR/vl_${label}.json" data.url)"
  path="$(json_get "$TMP_DIR/vl_${label}.json" data.previewPath)"
  admin="$(json_get "$TMP_DIR/vl_${label}.json" data.adminPath)"
  echo "  view-link $label → $itype/$iid mode=$mode"
  check "view-link $label → 200" "$([[ "$HTTP" == "200" && "$(json_get "$TMP_DIR/vl_${label}.json" success)" == "true" ]] && echo 1 || echo 0)"
  check "view-link $label mode=$expect_mode" "$([[ "$mode" == "$expect_mode" ]] && echo 1 || echo 0)"
  case "$expect_mode" in
    Public|External)
      check "view-link $label has http url" "$([[ "$url" == http* ]] && echo 1 || echo 0)"
      ;;
    AdminPreview)
      check "view-link $label has previewPath" "$([[ "$path" == /preview/* ]] && echo 1 || echo 0)"
      # fetch public preview API
      local token="${path#/preview/}"
      HTTP=$(req GET "/api/Public/QuickSendPreview/${token}" "$TMP_DIR/prev_${label}.json")
      check "view-link $label preview API 200" "$([[ "$HTTP" == "200" && "$(json_get "$TMP_DIR/prev_${label}.json" success)" == "true" ]] && echo 1 || echo 0)"
      check "view-link $label isAdminPreview" "$([[ "$(json_get "$TMP_DIR/prev_${label}.json" data.isAdminPreview)" == "true" ]] && echo 1 || echo 0)"
      ;;
    AdminPage)
      check "view-link $label adminPath" "$([[ "$admin" == /admin/quick-send-approvals* ]] && echo 1 || echo 0)"
      ;;
  esac
}

test_view_link "Public" "$TMP_DIR/sample_public" "Public"
test_view_link "AdminPreview" "$TMP_DIR/sample_preview" "AdminPreview"
test_view_link "External" "$TMP_DIR/sample_external" "External"
test_view_link "AdminPage" "$TMP_DIR/sample_admin" "AdminPage"

# missing item
HTTP=$(req POST "/api/Admin/UserInventory/${USER_ID}/contents/LuckyWheel/999999/view-link" "$TMP_DIR/vl_nf.json")
check "view-link missing → 404" "$([[ "$HTTP" == "404" ]] && echo 1 || echo 0)"

# invalid bearer
HTTP=$(curl -sS -m 20 -w "%{http_code}" -o "$TMP_DIR/bad_auth.json" \
  -H "Authorization: Bearer not-a-valid-token" \
  "$BASE/api/Admin/UserInventory/${USER_ID}/summary" 2>/dev/null || echo 000)
check "invalid bearer → 401" "$([[ "$HTTP" == "401" ]] && echo 1 || echo 0)"

# ensure AdminPreview covers all 4 visual types when present in full list
python3 - "$TMP_DIR/all.json" "$TMP_DIR" <<'PY'
import json,sys
d=json.load(open(sys.argv[1],encoding='utf-8'))
items=(d.get('data') or {}).get('items') or []
out=sys.argv[2]
visuals={"BusinessCard","UserForm","LuckyWheel","BookingSystem"}
found={}
for i in items:
    t=i.get("itemType")
    if t in visuals and i.get("viewMode")=="AdminPreview" and i.get("canView"):
        found.setdefault(t, (t, i["id"]))
for t, pair in found.items():
    open(f"{out}/vp_{t}","w").write(f"{pair[0]} {pair[1]}")
open(f"{out}/vp_types","w").write(",".join(sorted(found)))
print("adminPreview_types", sorted(found))
PY
for T in BusinessCard UserForm LuckyWheel BookingSystem; do
  if [[ -f "$TMP_DIR/vp_$T" && -s "$TMP_DIR/vp_$T" ]]; then
    test_view_link "AP-$T" "$TMP_DIR/vp_$T" "AdminPreview"
  else
    echo "SKIP  AdminPreview sample for $T"
    check "AdminPreview $T skipped" 1
  fi
done

echo "==== TOTAL pass=$PASS fail=$FAIL ===="
[[ "$FAIL" -eq 0 ]]
