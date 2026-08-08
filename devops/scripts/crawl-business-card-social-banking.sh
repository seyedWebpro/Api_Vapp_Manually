#!/usr/bin/env bash
# Crawl local BusinessCard API: social links + banking + public schema
set -euo pipefail

BASE_URL="${BASE_URL:-http://localhost:5054}"
API="${BASE_URL}/api"
SLUG="bc-social-bank-$(date +%s)"
PASS=0
FAIL=0

red() { printf '\033[31m%s\033[0m\n' "$*"; }
green() { printf '\033[32m%s\033[0m\n' "$*"; }
info() { printf '\033[36m%s\033[0m\n' "$*"; }

assert_json() {
  local name="$1"
  local body="$2"
  local expect_success="$3"
  local expect_http="$4"
  local http_code="$5"

  if [[ "$http_code" != "$expect_http" ]]; then
    red "FAIL [$name] HTTP=$http_code expected=$expect_http"
    echo "$body" | head -c 800
    echo
    FAIL=$((FAIL + 1))
    return 1
  fi

  local success
  success=$(echo "$body" | python3 -c "import sys,json; d=json.load(sys.stdin); print(str(d.get('success')).lower())" 2>/dev/null || echo "parse_error")
  if [[ "$success" != "$expect_success" ]]; then
    red "FAIL [$name] success=$success expected=$expect_success"
    echo "$body" | head -c 800
    echo
    FAIL=$((FAIL + 1))
    return 1
  fi

  green "PASS [$name] HTTP=$http_code success=$success"
  PASS=$((PASS + 1))
  return 0
}

curl_json() {
  local method="$1"
  local path="$2"
  local data="${3:-}"
  if [[ -n "$data" ]]; then
    curl -sS -w '\n%{http_code}' -X "$method" "${API}${path}" \
      -H 'Content-Type: application/json' \
      -d "$data"
  else
    curl -sS -w '\n%{http_code}' -X "$method" "${API}${path}"
  fi
}

info "=== BusinessCard social+banking crawl @ ${BASE_URL} ==="

# Health / reachability
if ! curl -sS -o /dev/null -w '%{http_code}' "${BASE_URL}/swagger/index.html" | grep -Eq '200|301|302'; then
  # fallback: try any endpoint
  if ! curl -sS -o /dev/null -w '%{http_code}' "${API}/BusinessCard?pageNumber=1&pageSize=1" | grep -Eq '200|401'; then
    red "API not reachable at ${BASE_URL}"
    exit 1
  fi
fi

CREATE_BODY=$(cat <<EOF
{
  "templateKey": "creative",
  "title": "کارت تست شبکه و بانک",
  "slug": "${SLUG}",
  "descriptionEnabled": true,
  "descriptionText": "توضیحات تست کراول",
  "contactEnabled": true,
  "contactPhone": "09121234567",
  "contactEmail": "card@example.com",
  "bankingEnabled": true,
  "bankAccountNumber": "1234567890",
  "bankCardNumber": "6037991234567890",
  "bankShebaNumber": "IR120170000000123456789001",
  "socialLinks": [
    {"networkType":"instagram","label":"اینستاگرام کاری","value":"work_ig","displayOrder":0},
    {"networkType":"instagram","label":"اینستاگرام شخصی","value":"personal_ig","displayOrder":1},
    {"networkType":"whatsapp","label":"واتساپ پشتیبانی","value":"09121234567","displayOrder":2},
    {"networkType":"telegram","value":"tg_channel","displayOrder":3},
    {"networkType":"eitaa","value":"eitaa_ch","displayOrder":4},
    {"networkType":"rubika","value":"rubika_ch","displayOrder":5},
    {"networkType":"bale","value":"bale_ch","displayOrder":6}
  ]
}
EOF
)

RESP=$(curl_json POST /BusinessCard "$CREATE_BODY")
HTTP=$(echo "$RESP" | tail -n1)
BODY=$(echo "$RESP" | sed '$d')
assert_json "POST /BusinessCard create" "$BODY" "true" "201" "$HTTP" || true

CARD_ID=$(echo "$BODY" | python3 -c "import sys,json; print(json.load(sys.stdin)['data']['id'])" 2>/dev/null || echo "")
if [[ -z "$CARD_ID" ]]; then
  red "Could not parse card id — abort"
  exit 1
fi
info "CardId=${CARD_ID} Slug=${SLUG}"

# invalid network type
BAD_BODY='{"socialLinks":[{"networkType":"not_a_network","value":"x","displayOrder":0}]}'
RESP=$(curl_json POST "/BusinessCard/${CARD_ID}/update-sections" "$BAD_BODY")
HTTP=$(echo "$RESP" | tail -n1)
BODY=$(echo "$RESP" | sed '$d')
assert_json "POST update-sections invalid network" "$BODY" "false" "400" "$HTTP" || true

# invalid card number
BAD_CARD='{"bankingEnabled":true,"bankCardNumber":"1234"}'
RESP=$(curl_json POST "/BusinessCard/${CARD_ID}/update-sections" "$BAD_CARD")
HTTP=$(echo "$RESP" | tail -n1)
BODY=$(echo "$RESP" | sed '$d')
assert_json "POST update-sections invalid card" "$BODY" "false" "400" "$HTTP" || true

# publish
RESP=$(curl_json POST "/BusinessCard/${CARD_ID}/publish" '{}')
HTTP=$(echo "$RESP" | tail -n1)
BODY=$(echo "$RESP" | sed '$d')
assert_json "POST publish" "$BODY" "true" "200" "$HTTP" || true

PUBLIC_URL=$(echo "$BODY" | python3 -c "import sys,json; print(json.load(sys.stdin)['data'].get('publicUrl') or '')" 2>/dev/null || echo "")

# public get
RESP=$(curl_json GET "/BusinessCardPublic/${SLUG}")
HTTP=$(echo "$RESP" | tail -n1)
BODY=$(echo "$RESP" | sed '$d')
assert_json "GET BusinessCardPublic/{slug}" "$BODY" "true" "200" "$HTTP" || true

# validate public payload fields
if echo "$BODY" | python3 -c "
import sys, json
d=json.load(sys.stdin)['data']
links=d.get('socialLinks') or []
assert len(links)>=7, f'socialLinks count={len(links)}'
labels=[l.get('label') for l in links]
assert any('کاری' in (x or '') for x in labels), labels
assert d.get('bankingEnabled') is True
assert d.get('bankCardNumber')=='6037991234567890'
assert d.get('bankShebaNumber')=='IR120170000000123456789001'
assert d.get('contactInstagram')=='work_ig'
"; then
  green "PASS [public payload fields]"
  PASS=$((PASS+1))
else
  red "FAIL [public payload fields]"
  FAIL=$((FAIL+1))
fi

# GET details owner
RESP=$(curl_json GET "/BusinessCard/${CARD_ID}")
HTTP=$(echo "$RESP" | tail -n1)
BODY=$(echo "$RESP" | sed '$d')
assert_json "GET /BusinessCard/{id}" "$BODY" "true" "200" "$HTTP" || true

# cleanup soft-delete
RESP=$(curl_json POST "/BusinessCard/${CARD_ID}/delete")
HTTP=$(echo "$RESP" | tail -n1)
BODY=$(echo "$RESP" | sed '$d')
assert_json "POST delete" "$BODY" "true" "200" "$HTTP" || true

echo
info "=== Summary: PASS=${PASS} FAIL=${FAIL} publicUrl=${PUBLIC_URL} ==="
[[ "$FAIL" -eq 0 ]]
