#!/usr/bin/env bash
# Crawl GET /api/EducationalVideo — لیست ویدیوهای فعال (موبایل)
#
# Usage:
#   BASE_URL=http://127.0.0.1:5054 bash devops/scripts/crawl-educational-video.sh
#   BASE_URL=https://vapplication.ir USER_TOKEN=eyJ... bash devops/scripts/crawl-educational-video.sh
#
# سناریوها:
#   - health
#   - بدون توکن → 401 (مگر DisableAuth)
#   - با توکن → 200 + success=true + data آرایه
#   - دوبار پشت سر هم → هر دو 200 (cache با SizeLimit نباید 500 بدهد)
set -euo pipefail

BASE_URL="${BASE_URL:-http://127.0.0.1:5054}"
USER_TOKEN="${USER_TOKEN:-}"
PASS=0
FAIL=0
TMP_DIR="$(mktemp -d)"
trap 'rm -rf "$TMP_DIR"' EXIT

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
    elif isinstance(cur,list) and p.isdigit():
        cur=cur[int(p)]
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

http_get() {
  local path="$1" out="$2" token="${3:-}"
  local code
  if [[ -n "$token" ]]; then
    code="$(curl -sS -m 45 -o "$out" -w '%{http_code}' \
      "$BASE_URL$path" \
      -H 'Accept: application/json' \
      -H "Authorization: Bearer $token" || echo 000)"
  else
    code="$(curl -sS -m 45 -o "$out" -w '%{http_code}' \
      "$BASE_URL$path" \
      -H 'Accept: application/json' || echo 000)"
  fi
  if [[ ! -s "$out" ]]; then echo '{}' > "$out"; fi
  echo "$code"
}

echo "=== EducationalVideo crawl @ $BASE_URL ==="

code="$(curl -sS -m 10 -o /dev/null -w '%{http_code}' "$BASE_URL/health" || echo 000)"
check "GET /health → 200" "$([[ "$code" == "200" ]] && echo 1 || echo 0)"

UNAUTH="$TMP_DIR/unauth.json"
code="$(http_get /api/EducationalVideo "$UNAUTH")"
if [[ "$code" == "401" || "$code" == "403" ]]; then
  check "GET /api/EducationalVideo without token → 401/403" "1"
elif [[ "$code" == "200" ]]; then
  echo "WARN  DisableAuth فعال — بدون توکن 200"
  PASS=$((PASS + 1))
else
  check "GET /api/EducationalVideo without token → 401/403 (got $code)" "0"
fi

if [[ -z "$USER_TOKEN" ]]; then
  echo "SKIP authenticated tests (set USER_TOKEN for prod crawl)"
else
  OUT1="$TMP_DIR/videos1.json"
  code="$(http_get /api/EducationalVideo "$OUT1" "$USER_TOKEN")"
  SUCCESS="$(json_get "$OUT1" success)"
  ERR="$(json_get "$OUT1" errorCode)"
  DATA_TYPE="$(python3 - "$OUT1" <<'PY'
import json,sys
d=json.load(open(sys.argv[1],encoding='utf-8'))
data=d.get("data")
print("array" if isinstance(data,list) else type(data).__name__)
PY
)"
  check "GET /api/EducationalVideo → 200" "$([[ "$code" == "200" ]] && echo 1 || echo 0)"
  check "success=true" "$([[ "$SUCCESS" == "true" ]] && echo 1 || echo 0)"
  check "errorCode empty on success" "$([[ -z "$ERR" || "$ERR" == "None" ]] && echo 1 || echo 0)"
  check "data is array" "$([[ "$DATA_TYPE" == "array" ]] && echo 1 || echo 0)"
  check "not UNEXPECTED_ERROR" "$([[ "$ERR" != "UNEXPECTED_ERROR" ]] && echo 1 || echo 0)"

  OUT2="$TMP_DIR/videos2.json"
  code2="$(http_get /api/EducationalVideo "$OUT2" "$USER_TOKEN")"
  SUCCESS2="$(json_get "$OUT2" success)"
  check "second GET (cache hit) → 200" "$([[ "$code2" == "200" && "$SUCCESS2" == "true" ]] && echo 1 || echo 0)"

  if [[ "$code" != "200" ]]; then
    echo "Response body:" >&2
    cat "$OUT1" >&2
  fi
fi

echo ""
echo "=== Summary: PASS=$PASS FAIL=$FAIL ==="
if [[ "$FAIL" -gt 0 ]]; then
  exit 1
fi
