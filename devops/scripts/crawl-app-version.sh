#!/usr/bin/env bash
# Crawl مدیریت آپدیت اپ (AppVersion check + admin policy)
#
# Usage:
#   BASE_URL=http://127.0.0.1:5054 bash devops/scripts/crawl-app-version.sh
#
# سناریوها:
#   - health
#   - check بدون پارامتر / پلتفرم نامعتبر / ورژن نامعتبر → 400
#   - check با 1.0.0 → none (ورژن اولیه)
#   - admin list + update latest به 1.1.0 (min همان 1.0.0) → optional
#   - check بعد از آپدیت → optional
#   - بازگرداندن به 1.0.0
set -euo pipefail

BASE_URL="${BASE_URL:-http://127.0.0.1:5054}"
TMP_DIR="$(mktemp -d)"
PASS=0
FAIL=0
ORIG_ANDROID="$TMP_DIR/android_orig.json"

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
  if [[ ! -s "$out" ]]; then echo '{}' > "$out"; fi
  echo "$code"
}

http_ok() {
  local code="$1"
  [[ "$code" == "200" || "$code" == "201" ]]
}

echo "=== AppVersion crawl @ $BASE_URL ==="

# 0) health
code="$(curl -sS -m 10 -o /dev/null -w '%{http_code}' "$BASE_URL/health" || echo 000)"
check "GET /health → 200" "$([[ "$code" == "200" ]] && echo 1 || echo 0)"

# 1) validation: missing params
MISS="$TMP_DIR/miss.json"
code="$(http_json GET '/api/AppVersion/check' "" "$MISS")"
SUCCESS="$(json_get "$MISS" success)"
ERR="$(json_get "$MISS" errorCode)"
check "check without params → 400" "$([[ "$code" == "400" && "$SUCCESS" == "false" ]] && echo 1 || echo 0)"
check "check without params has errorCode" "$([[ -n "$ERR" ]] && echo 1 || echo 0)"

# 2) invalid platform
BAD_PLAT="$TMP_DIR/bad_plat.json"
code="$(http_json GET '/api/AppVersion/check?platform=web&currentVersion=1.0.0' "" "$BAD_PLAT")"
ERR="$(json_get "$BAD_PLAT" errorCode)"
MSG="$(json_get "$BAD_PLAT" message)"
check "invalid platform → 400 VALIDATION_FAILED" "$([[ "$code" == "400" && "$ERR" == "VALIDATION_FAILED" ]] && echo 1 || echo 0)"
check "invalid platform Persian message" "$([[ -n "$MSG" ]] && echo 1 || echo 0)"

# 3) invalid version format
BAD_VER="$TMP_DIR/bad_ver.json"
code="$(http_json GET '/api/AppVersion/check?platform=android&currentVersion=abc' "" "$BAD_VER")"
ERR="$(json_get "$BAD_VER" errorCode)"
check "invalid version → 400 INVALID_INPUT" "$([[ "$code" == "400" && "$ERR" == "INVALID_INPUT" ]] && echo 1 || echo 0)"

# 4) happy path none (seed 1.0.0)
NONE="$TMP_DIR/none.json"
code="$(http_json GET '/api/AppVersion/check?platform=android&currentVersion=1.0.0' "" "$NONE")"
UT="$(json_get "$NONE" data.updateType)"
LV="$(json_get "$NONE" data.latestVersion)"
MV="$(json_get "$NONE" data.minSupportedVersion)"
SUCCESS="$(json_get "$NONE" success)"
check "check 1.0.0 → 200 success" "$(http_ok "$code" && [[ "$SUCCESS" == "true" ]] && echo 1 || echo 0)"
check "check 1.0.0 → updateType=none" "$([[ "$UT" == "none" ]] && echo 1 || echo 0)"
check "seed latestVersion=1.0.0" "$([[ "$LV" == "1.0.0" ]] && echo 1 || echo 0)"
check "seed minSupportedVersion=1.0.0" "$([[ "$MV" == "1.0.0" ]] && echo 1 || echo 0)"

# ios parity
IOS="$TMP_DIR/ios.json"
code="$(http_json GET '/api/AppVersion/check?platform=ios&currentVersion=1.0.0' "" "$IOS")"
UT="$(json_get "$IOS" data.updateType)"
check "ios check 1.0.0 → none" "$(http_ok "$code" && [[ "$UT" == "none" ]] && echo 1 || echo 0)"

# 5) admin list (DisableAuth local)
ADMIN_LIST="$TMP_DIR/admin_list.json"
code="$(http_json GET /api/Admin/AppVersion "" "$ADMIN_LIST")"
check "GET Admin/AppVersion → 200" "$(http_ok "$code" && echo 1 || echo 0)"

# 6) snapshot android + bump latest to 1.1.0 (keep min 1.0.0 → optional)
code="$(http_json GET /api/Admin/AppVersion/android "" "$ORIG_ANDROID")"
check "GET Admin/AppVersion/android → 200" "$(http_ok "$code" && echo 1 || echo 0)"

UPDATE_OUT="$TMP_DIR/update.json"
UPDATE_BODY="$(python3 - "$ORIG_ANDROID" <<'PY'
import json,sys
d=json.load(open(sys.argv[1],encoding='utf-8'))['data']
body={
  "latestVersion": "1.1.0",
  "minSupportedVersion": "1.0.0",
  "storeUrl": d.get("storeUrl") or "https://example.com/store/android",
  "title": "نسخه جدید آماده است",
  "message": "می‌توانید اپ را به‌روزرسانی کنید.",
  "changelog": ["بهبود عملکرد", "رفع چند باگ"],
  "isActive": True,
}
print(json.dumps(body, ensure_ascii=False))
PY
)"
code="$(http_json POST /api/Admin/AppVersion/android/update "$UPDATE_BODY" "$UPDATE_OUT")"
NEW_LV="$(json_get "$UPDATE_OUT" data.latestVersion)"
NEW_MV="$(json_get "$UPDATE_OUT" data.minSupportedVersion)"
check "POST Admin update latest=1.1.0 → 200" "$(http_ok "$code" && [[ "$NEW_LV" == "1.1.0" ]] && echo 1 || echo 0)"
check "min stays 1.0.0 (optional mode)" "$([[ "$NEW_MV" == "1.0.0" ]] && echo 1 || echo 0)"

# 7) check after bump → optional
OPT="$TMP_DIR/optional.json"
code="$(http_json GET '/api/AppVersion/check?platform=android&currentVersion=1.0.0' "" "$OPT")"
UT="$(json_get "$OPT" data.updateType)"
STORE="$(json_get "$OPT" data.storeUrl)"
TITLE="$(json_get "$OPT" data.title)"
check "check after bump → optional" "$(http_ok "$code" && [[ "$UT" == "optional" ]] && echo 1 || echo 0)"
check "optional has storeUrl" "$([[ -n "$STORE" ]] && echo 1 || echo 0)"
check "optional has title" "$([[ -n "$TITLE" ]] && echo 1 || echo 0)"

# 8) still on latest → none
UPTODATE="$TMP_DIR/uptodate.json"
code="$(http_json GET '/api/AppVersion/check?platform=android&currentVersion=1.1.0' "" "$UPTODATE")"
UT="$(json_get "$UPTODATE" data.updateType)"
check "check current=1.1.0 → none" "$(http_ok "$code" && [[ "$UT" == "none" ]] && echo 1 || echo 0)"

# 9) forced path (raise min above current) then restore
FORCE_BODY='{"latestVersion":"1.2.0","minSupportedVersion":"1.2.0","storeUrl":"https://example.com/store/android","title":"به‌روزرسانی الزامی","message":"لطفاً آپدیت کنید","changelog":["امنیتی"],"isActive":true}'
FORCE_OUT="$TMP_DIR/force.json"
code="$(http_json POST /api/Admin/AppVersion/android/update "$FORCE_BODY" "$FORCE_OUT")"
check "admin set forced policy → 200" "$(http_ok "$code" && echo 1 || echo 0)"

FORCED="$TMP_DIR/forced_check.json"
code="$(http_json GET '/api/AppVersion/check?platform=android&currentVersion=1.0.0' "" "$FORCED")"
UT="$(json_get "$FORCED" data.updateType)"
check "check forced → forced" "$(http_ok "$code" && [[ "$UT" == "forced" ]] && echo 1 || echo 0)"

# 10) restore seed (1.0.0 / 1.0.0 optional-ready)
RESTORE_BODY='{"latestVersion":"1.0.0","minSupportedVersion":"1.0.0","storeUrl":null,"title":"نسخه جدید آماده است","message":"نسخه جدید اپ در دسترس است. می‌توانید هر زمان به‌روزرسانی کنید.","changelog":[],"isActive":true}'
RESTORE_OUT="$TMP_DIR/restore.json"
code="$(http_json POST /api/Admin/AppVersion/android/update "$RESTORE_BODY" "$RESTORE_OUT")"
UT_RESTORE="$(json_get "$RESTORE_OUT" data.latestVersion)"
check "restore android to 1.0.0" "$(http_ok "$code" && [[ "$UT_RESTORE" == "1.0.0" ]] && echo 1 || echo 0)"

FINAL="$TMP_DIR/final.json"
code="$(http_json GET '/api/AppVersion/check?platform=android&currentVersion=1.0.0' "" "$FINAL")"
UT="$(json_get "$FINAL" data.updateType)"
check "final check → none" "$(http_ok "$code" && [[ "$UT" == "none" ]] && echo 1 || echo 0)"

# 11) admin update validation (min > latest)
BAD_UPDATE="$TMP_DIR/bad_update.json"
code="$(http_json POST /api/Admin/AppVersion/android/update \
  '{"latestVersion":"1.0.0","minSupportedVersion":"2.0.0","isActive":true}' \
  "$BAD_UPDATE")"
SUCCESS="$(json_get "$BAD_UPDATE" success)"
check "admin min>latest → 400" "$([[ "$code" == "400" && "$SUCCESS" == "false" ]] && echo 1 || echo 0)"

echo ""
echo "=== Summary: PASS=$PASS FAIL=$FAIL ==="
if [[ "$FAIL" -gt 0 ]]; then
  exit 1
fi
