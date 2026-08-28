#!/usr/bin/env bash
# Crawl تست فیلتر و صفحه‌بندی تأیید قالب / تأیید پیام
# Usage: BASE_URL=http://127.0.0.1:5054 bash devops/scripts/crawl-admin-approval-filters.sh
set -euo pipefail
export PATH="/usr/bin:/bin:/usr/sbin:/sbin:/usr/local/bin:${PATH:-}"

ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
cd "$ROOT"
BASE="${BASE_URL:-http://127.0.0.1:5054}"
LOG=/tmp/vapp-admin-approval-crawl.log
PIDFILE=/tmp/vapp-admin-approval-crawl.pid
TMP_DIR="$(mktemp -d)"
PASS=0
FAIL=0
SUFFIX="$(date +%s)"

cleanup() { rm -rf "$TMP_DIR"; }
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

assert_eq() {
  local name="$1" expected="$2" actual="$3"
  if [[ "$expected" == "$actual" ]]; then
    echo "PASS: $name (got=$actual)"
    PASS=$((PASS+1))
  else
    echo "FAIL: $name expected=$expected got=$actual"
    FAIL=$((FAIL+1))
  fi
}

req() {
  local method="$1" path="$2" out="$3"
  shift 3
  curl -s -w "%{http_code}" -o "$out" -X "$method" "${BASE}${path}" \
    -H "Content-Type: application/json" \
    -H "Accept: application/json" \
    "$@" || true
}

echo "===== UNIT TESTS ====="
dotnet test Tests/Api_Vapp.Tests.csproj --nologo \
  --filter "FullyQualifiedName~AdminApprovalListFilterTests" \
  --logger "console;verbosity=minimal"
echo "UNIT_OK"

echo "===== RESTART API ====="
for p in $(lsof -t -iTCP:5054 -sTCP:LISTEN 2>/dev/null || true); do kill "$p" 2>/dev/null || true; done
sleep +2
for p in $(lsof -t -iTCP:5054 -sTCP:LISTEN 2>/dev/null || true); do kill -9 "$p" 2>/dev/null || true; done
sleep 1

nohup env ASPNETCORE_ENVIRONMENT=Development \
  dotnet exec bin/Debug/net8.0/Api_Vapp.dll --urls "http://127.0.0.1:5054" \
  > "$LOG" 2>&1 &
echo $! > "$PIDFILE"

READY=0
for i in $(seq 1 60); do
  code=$(curl -s -o /dev/null -w "%{http_code}" --max-time 10 "$BASE/api/Admin/TemplateApproval?page=1&pageSize=1" || echo 000)
  if [[ "$code" == "200" ]]; then READY=1; break; fi
  if ! kill -0 "$(cat "$PIDFILE")" 2>/dev/null; then
    echo "API_DIED_EARLY"
    tail -40 "$LOG" || true
    exit 1
  fi
  sleep 2
done
if [[ "$READY" != "1" ]]; then
  echo "API_NOT_READY"
  tail -40 "$LOG" || true
  exit 1
fi
echo "API_READY"

echo "===== SEED TEMPLATE ====="
HTTP=$(curl -s -w "%{http_code}" -o "$TMP_DIR/template.json" -X POST "$BASE/api/Template" \
  -F "Name=قالب کراول ${SUFFIX}" \
  -F "Content=سلام هلدینگ کراول ${SUFFIX} برای تست فیلتر" \
  -F "Description=توضیح کراول")
SC=$(json_get "$TMP_DIR/template.json" statusCode)
TEMPLATE_ID=$(json_get "$TMP_DIR/template.json" data.id)
assert_eq "create template status" "201" "$SC"
echo "TEMPLATE_ID=$TEMPLATE_ID"

echo "===== TEMPLATE APPROVAL API ====="
HTTP=$(req GET "/api/Admin/TemplateApproval?page=1&pageSize=1" "$TMP_DIR/tpl_page1.json")
SC=$(json_get "$TMP_DIR/tpl_page1.json" statusCode)
TC=$(json_get "$TMP_DIR/tpl_page1.json" data.totalCount)
TP=$(json_get "$TMP_DIR/tpl_page1.json" data.totalPages)
PS=$(json_get "$TMP_DIR/tpl_page1.json" data.pageSize)
IC=$(python3 - "$TMP_DIR/tpl_page1.json" <<'PY'
import json,sys
d=json.load(open(sys.argv[1],encoding='utf-8'))
print(len(((d.get('data') or {}).get('items') or [])))
PY
)
assert_eq "template list status" "200" "$SC"
assert_eq "template pageSize" "1" "$PS"
assert_eq "template page1 item count" "1" "$IC"
if [[ "$TC" -gt 1 ]]; then
  assert_eq "template totalPages gt 1" "true" "$([[ "$TP" -gt 1 ]] && echo true || echo false)"
fi

HTTP=$(req GET "/api/Admin/TemplateApproval?search=هلدینگ%20کراول%20${SUFFIX}&page=1&pageSize=20" "$TMP_DIR/tpl_search.json")
SC=$(json_get "$TMP_DIR/tpl_search.json" statusCode)
FOUND=$(python3 - "$TMP_DIR/tpl_search.json" "$TEMPLATE_ID" <<'PY'
import json,sys
d=json.load(open(sys.argv[1],encoding='utf-8'))
tid=int(sys.argv[2])
items=((d.get('data') or {}).get('items') or [])
print('yes' if any(i.get('id')==tid for i in items) else 'no')
PY
)
assert_eq "template search status" "200" "$SC"
assert_eq "template search finds seeded item" "yes" "$FOUND"

USER_ID=$(json_get "$TMP_DIR/tpl_search.json" data.items.0.userId)
if [[ -n "$USER_ID" ]]; then
  HTTP=$(req GET "/api/Admin/TemplateApproval?userSearch=${USER_ID}&page=1&pageSize=20" "$TMP_DIR/tpl_user.json")
  SC=$(json_get "$TMP_DIR/tpl_user.json" statusCode)
  assert_eq "template userSearch status" "200" "$SC"
fi

HTTP=$(req GET "/api/Admin/TemplateApproval?search=__no_match_${SUFFIX}__&page=1&pageSize=20" "$TMP_DIR/tpl_empty.json")
TC=$(json_get "$TMP_DIR/tpl_empty.json" data.totalCount)
assert_eq "template empty search totalCount" "0" "$TC"

echo "===== MESSAGE APPROVAL API ====="
HTTP=$(req GET "/api/Admin/MessageApproval?page=1&pageSize=1" "$TMP_DIR/msg_page1.json")
SC=$(json_get "$TMP_DIR/msg_page1.json" statusCode)
PS=$(json_get "$TMP_DIR/msg_page1.json" data.pageSize)
assert_eq "message list status" "200" "$SC"
assert_eq "message pageSize" "1" "$PS"

HTTP=$(req GET "/api/Admin/MessageApproval?search=__no_match_${SUFFIX}__&page=1&pageSize=20" "$TMP_DIR/msg_empty.json")
SC=$(json_get "$TMP_DIR/msg_empty.json" statusCode)
TC=$(json_get "$TMP_DIR/msg_empty.json" data.totalCount)
assert_eq "message empty search status" "200" "$SC"
assert_eq "message empty search totalCount" "0" "$TC"

HTTP=$(req GET "/api/Admin/MessageApproval/pending?page=1&pageSize=5&search=&userSearch=" "$TMP_DIR/msg_pending.json")
SC=$(json_get "$TMP_DIR/msg_pending.json" statusCode)
assert_eq "message pending status" "200" "$SC"

echo "===== SUMMARY ====="
echo "PASS=$PASS FAIL=$FAIL"
if [[ "$FAIL" -gt 0 ]]; then
  exit 1
fi
echo "CRAWL_OK"
