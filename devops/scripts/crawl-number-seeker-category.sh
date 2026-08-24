#!/usr/bin/env bash
# Crawl NumberSeeker custom-category / Combobox API behavior.
set -euo pipefail
export PATH="/usr/bin:/bin:/usr/sbin:/sbin:/usr/local/bin:${PATH:-}"

ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
cd "$ROOT"
BASE="${BASE_URL:-http://127.0.0.1:5054}"
LOG=/tmp/vapp-ns-api.log
PIDFILE=/tmp/vapp-ns-api.pid

echo "===== UNIT TESTS ====="
dotnet test Tests/Api_Vapp.Tests.csproj --nologo --filter "FullyQualifiedName~NumberSeeker" --logger "console;verbosity=minimal"
echo "UNIT_OK"

echo "===== RESTART API ====="
for p in $(lsof -t -iTCP:5054 -sTCP:LISTEN 2>/dev/null || true); do kill "$p" 2>/dev/null || true; done
sleep 2
for p in $(lsof -t -iTCP:5054 -sTCP:LISTEN 2>/dev/null || true); do kill -9 "$p" 2>/dev/null || true; done
sleep 1

nohup env ASPNETCORE_ENVIRONMENT=Development \
  dotnet exec bin/Debug/net8.0/Api_Vapp.dll --urls http://127.0.0.1:5054 \
  > "$LOG" 2>&1 &
echo $! > "$PIDFILE"
echo "API_STARTED:$(cat "$PIDFILE")"

for i in $(seq 1 90); do
  if grep -q "Migration completed successfully" "$LOG" 2>/dev/null; then
    echo "MIG_OK"
    break
  fi
  if ! kill -0 "$(cat "$PIDFILE")" 2>/dev/null; then
    echo "API_DIED_EARLY"
    tail -50 "$LOG" || true
    exit 1
  fi
  sleep 2
done

READY=0
for i in $(seq 1 40); do
  code=$(curl -s -o /tmp/ns_ready.json -w "%{http_code}" --max-time 20 "$BASE/api/NumberSeeker/categories" || echo 000)
  echo "wait_curl:$i:$code"
  if [[ "$code" == "200" ]]; then READY=1; break; fi
  sleep 2
done
if [[ "$READY" != "1" ]]; then
  echo "API_NOT_READY"
  tail -50 "$LOG" || true
  exit 1
fi
echo "API_READY"

python3 << 'PY'
import json, urllib.request, urllib.error
from urllib.request import Request, urlopen

BASE = "http://127.0.0.1:5054"
PASS = FAIL = 0
rows = []

def req(method, path, body=None, headers=None, timeout=30):
    h = {"Accept": "application/json"}
    if headers:
        h.update(headers)
    data = None
    if body is not None:
        if isinstance(body, (dict, list)):
            data = json.dumps(body, ensure_ascii=False).encode("utf-8")
            h.setdefault("Content-Type", "application/json; charset=utf-8")
        else:
            data = body.encode("utf-8") if isinstance(body, str) else body
            h.setdefault("Content-Type", "application/json; charset=utf-8")
    r = Request(BASE + path, data=data, headers=h, method=method)
    try:
        with urlopen(r, timeout=timeout) as resp:
            raw = resp.read().decode("utf-8", errors="replace")
            return resp.status, json.loads(raw) if raw else {}
    except urllib.error.HTTPError as e:
        raw = e.read().decode("utf-8", errors="replace")
        try:
            payload = json.loads(raw) if raw else {}
        except Exception:
            payload = {"raw": raw}
        return e.code, payload
    except Exception as e:
        return 0, {"error": str(e)}

def check(name, expect_http, expect_code, method, path, body=None, headers=None):
    global PASS, FAIL
    http, d = req(method, path, body=body, headers=headers)
    success = d.get("success")
    code = d.get("errorCode")
    ok = http == expect_http
    if expect_http == 200:
        ok = ok and success is True
    elif expect_http in (400, 401, 403, 404, 429, 503):
        ok = ok and success is False
    if expect_code != "any":
        ok = ok and code == expect_code
    if ok:
        PASS += 1
        rows.append(f"PASS | {name} | HTTP={http} code={code}")
    else:
        FAIL += 1
        rows.append(
            f"FAIL | {name} | expected HTTP={expect_http} code={expect_code} | "
            f"got HTTP={http} success={success} code={code} msg={d.get('message')} "
            f"errors={d.get('errors')} err={d.get('error')}"
        )

def flag(name, cond, detail=""):
    global PASS, FAIL
    if cond:
        PASS += 1
        rows.append(f"PASS | {name}")
    else:
        FAIL += 1
        rows.append(f"FAIL | {name} | {detail}")

http, c = req("GET", "/api/NumberSeeker/categories")
d = c.get("data") or {}
check("GET categories", 200, None, "GET", "/api/NumberSeeker/categories")
flag("categories.allowCustomCategory", d.get("allowCustomCategory") is True, str(d.get("allowCustomCategory")))
flag("categories.customCategoryHint", bool(d.get("customCategoryHint")))
flag("categories.placeholder", bool(d.get("placeholder")))
flag(
    "categories list",
    len(d.get("categories") or []) >= 10
    and any(x.get("name") == "رستوران" for x in (d.get("categories") or [])),
)

http, m = req("GET", "/api/NumberSeeker/form-meta")
d = m.get("data") or {}
check("GET form-meta", 200, None, "GET", "/api/NumberSeeker/form-meta")
flag("form-meta.allowCustomCategory", d.get("allowCustomCategory") is True)
flag("form-meta.customCategoryHint", bool(d.get("customCategoryHint")))
flag("form-meta.categoryPlaceholder", bool(d.get("categoryPlaceholder")))
flag("form-meta.defaultCity", d.get("defaultCity") == "تهران")
flag(
    "form-meta.phones",
    d.get("minPhones") == 1 and d.get("maxPhones") == 1000 and d.get("defaultPhones") == 50,
)
flag("form-meta.canViewPhones", isinstance(d.get("canViewPhones"), bool))
flag(
    "form-meta.lists",
    len(d.get("categories") or []) >= 10
    and len(d.get("cities") or []) >= 10
    and len(d.get("sources") or []) >= 1,
)

check("GET cities", 200, None, "GET", "/api/NumberSeeker/cities")
check("GET sources", 200, None, "GET", "/api/NumberSeeker/sources")
check("GET health", 200, None, "GET", "/api/NumberSeeker/health")
check("GET tasks", 200, None, "GET", "/api/NumberSeeker/tasks?limit=5")

check(
    "invalid bearer categories",
    401,
    "UNAUTHORIZED",
    "GET",
    "/api/NumberSeeker/categories",
    headers={"Authorization": "Bearer not-a-token"},
)
check(
    "invalid bearer scrape",
    401,
    "UNAUTHORIZED",
    "POST",
    "/api/NumberSeeker/scrape",
    body={"source": "divar", "city": "تهران", "category": "رستوران", "maxPhones": 10},
    headers={"Authorization": "Bearer not-a-token"},
)

for name, body in [
    ("missing category", {"source": "divar", "city": "تهران", "maxPhones": 10}),
    ("empty category", {"source": "divar", "city": "تهران", "category": "", "maxPhones": 10}),
    ("whitespace category", {"source": "divar", "city": "تهران", "category": "   ", "maxPhones": 10}),
    ("punctuation category", {"source": "divar", "city": "تهران", "category": "???", "maxPhones": 10}),
    ("whitespace city", {"source": "divar", "city": "   ", "category": "دندانپزشکی", "maxPhones": 10}),
    ("invalid source", {"source": "x", "city": "تهران", "category": "کافه", "maxPhones": 10}),
    ("maxPhones 0", {"source": "divar", "city": "تهران", "category": "کافه", "maxPhones": 0}),
    ("maxPhones 1001", {"source": "divar", "city": "تهران", "category": "کافه", "maxPhones": 1001}),
    ("empty body", {}),
    ("too long 201", {"source": "divar", "city": "تهران", "category": "ا" * 201, "maxPhones": 10}),
]:
    check(name, 400, "VALIDATION_FAILED", "POST", "/api/NumberSeeker/scrape", body=body)

http, d = req(
    "POST",
    "/api/NumberSeeker/scrape",
    body={"source": "divar", "city": "تهران", "category": "", "maxPhones": 10},
)
flag(
    "empty category single error",
    http == 400 and (d.get("errors") or []) == ["دسته‌بندی الزامی است"],
    str(d.get("errors")),
)
http, d = req(
    "POST",
    "/api/NumberSeeker/scrape",
    body={"source": "divar", "city": "تهران", "category": "???", "maxPhones": 10},
)
flag(
    "punctuation message",
    http == 400
    and d.get("message") == "دسته‌بندی نامعتبر است"
    and d.get("errorCode") == "VALIDATION_FAILED",
    str(d.get("message")),
)

def past(name, body):
    global PASS, FAIL
    http, d = req("POST", "/api/NumberSeeker/scrape", body=body)
    code = d.get("errorCode")
    if http == 400 or code == "VALIDATION_FAILED":
        FAIL += 1
        rows.append(f"FAIL | {name} rejected | HTTP={http} code={code} msg={d.get('message')}")
    else:
        PASS += 1
        rows.append(f"PASS | {name} past validation | HTTP={http} code={code}")

past("exact 200", {"source": "divar", "city": "تهران", "category": "ا" * 200, "maxPhones": 5})
for cat in ["دندانپزشکی", "قالیشویی", "فست‌فود", "  کافه رستوران  ", "موبایل فروشی", "رستوران"]:
    past(f"cat={cat!r}", {"source": "divar", "city": "تهران", "category": cat, "maxPhones": 5})
for source in ["divar", "sheypoor", "nshan", "balad", "googlemaps"]:
    past(f"source={source}", {"source": source, "city": "تهران", "category": "دندانپزشکی", "maxPhones": 3})

http, d = req("POST", "/api/NumberSeeker/scrape", body="{bad")
flag("invalid json -> 400", http == 400, f"HTTP={http} {d}")

print("\n".join(rows))
print("========== CRAWL SUMMARY ==========")
print(f"PASS={PASS} FAIL={FAIL} TOTAL={PASS+FAIL}")
print("CRAWL_ALL_PASSED" if FAIL == 0 else "CRAWL_HAS_FAILURES")
open("/tmp/ns_crawl_summary.txt", "w").write(f"{PASS} {FAIL} {0 if FAIL else 1}\n")
raise SystemExit(0 if FAIL == 0 else 1)
PY

echo "OVERALL_PASS"
