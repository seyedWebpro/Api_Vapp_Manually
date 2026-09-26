#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
cd "$ROOT"
BASE="${BASE_URL:-http://127.0.0.1:5054}"
TMP_DIR="$(mktemp -d)"
LOG="/tmp/vapp-professional-campaign-crawl.log"
PIDFILE="/tmp/vapp-professional-campaign-crawl.pid"
PASS=0
FAIL=0
SUFFIX="$(date +%s)"

cleanup() {
  rm -rf "$TMP_DIR"
  if [[ -f "$PIDFILE" ]]; then
    kill "$(cat "$PIDFILE")" 2>/dev/null || true
    rm -f "$PIDFILE"
  fi
}
trap cleanup EXIT

json_get() {
  python3 - "$1" "$2" <<'PY'
import json,sys
data=json.load(open(sys.argv[1],encoding='utf-8'))
cur=data
for key in sys.argv[2].split('.'):
    if isinstance(cur,dict): cur=cur.get(key)
    elif isinstance(cur,list) and key.isdigit() and int(key)<len(cur): cur=cur[int(key)]
    else: cur=None; break
if isinstance(cur,bool): print('true' if cur else 'false')
elif cur is None: print('')
else: print(cur)
PY
}

assert_eq() {
  if [[ "$2" == "$3" ]]; then
    echo "PASS: $1 (got=$3)"; PASS=$((PASS+1))
  else
    echo "FAIL: $1 expected=$2 got=$3"; FAIL=$((FAIL+1))
  fi
}

request() {
  local method="$1" path="$2" output="$3" body="${4:-}"
  if [[ -n "$body" ]]; then
    curl -sS -o "$output" -w '%{http_code}' -X "$method" "${BASE}${path}" \
      -H 'Accept: application/json' -H 'Content-Type: application/json' -d "$body"
  else
    curl -sS -o "$output" -w '%{http_code}' -X "$method" "${BASE}${path}" -H 'Accept: application/json'
  fi
}

echo '===== FEATURE TESTS ====='
DOTNET_ROLL_FORWARD=Major dotnet test Tests/Api_Vapp.Tests.csproj --no-restore \
  --filter 'FullyQualifiedName~ProfessionalCampaign' --logger 'console;verbosity=minimal'

echo '===== START API ====='
for pid in $(lsof -t -iTCP:5054 -sTCP:LISTEN 2>/dev/null || true); do kill "$pid" 2>/dev/null || true; done
nohup env DOTNET_ROLL_FORWARD=Major ASPNETCORE_ENVIRONMENT=Development DatabaseProvider=LocalDocker \
  dotnet run --no-build --no-launch-profile --urls 'http://127.0.0.1:5054' >"$LOG" 2>&1 &
echo $! >"$PIDFILE"

ready=0
for _ in $(seq 1 120); do
  code="$(curl -s -o /dev/null -w '%{http_code}' --max-time 3 "$BASE/health" || true)"
  if [[ "$code" == '200' ]]; then ready=1; break; fi
  if ! kill -0 "$(cat "$PIDFILE")" 2>/dev/null; then tail -80 "$LOG"; exit 1; fi
  sleep 2
done
if [[ "$ready" != '1' ]]; then tail -80 "$LOG"; exit 1; fi

echo '===== VALIDATION ====='
HTTP="$(request POST '/api/professional-campaigns' "$TMP_DIR/invalid.json" '{}')"
assert_eq 'invalid body HTTP' '400' "$HTTP"
assert_eq 'invalid body errorCode' 'VALIDATION_FAILED' "$(json_get "$TMP_DIR/invalid.json" errorCode)"
test -n "$(json_get "$TMP_DIR/invalid.json" traceId)" && TRACE=true || TRACE=false
assert_eq 'invalid body traceId' 'true' "$TRACE"

echo '===== SEED NOTEBOOK + CONTACT ====='
HTTP="$(curl -sS -o "$TMP_DIR/notebook.json" -w '%{http_code}' -X POST "$BASE/api/ContactNotebook" \
  -H 'Accept: application/json' -F "Name=کمپین کراول ${SUFFIX}" -F 'IsActive=true')"
assert_eq 'create notebook HTTP' '201' "$HTTP"
NOTEBOOK_ID="$(json_get "$TMP_DIR/notebook.json" data.id)"

MOBILE="0912$(printf '%07d' $((SUFFIX % 10000000)))"
CONTACT_BODY="$(python3 - "$NOTEBOOK_ID" "$MOBILE" <<'PY'
import json,sys
print(json.dumps({'contactNotebookId':int(sys.argv[1]),'mobileNumber':sys.argv[2],'fullName':'مخاطب کراول'},ensure_ascii=False))
PY
)"
HTTP="$(request POST '/api/Contact' "$TMP_DIR/contact.json" "$CONTACT_BODY")"
assert_eq 'create contact HTTP' '201' "$HTTP"

echo '===== CREATE CAMPAIGN WITH EXPLICIT IRAN OFFSET ====='
START_IRAN="$(python3 - <<'PY'
from datetime import datetime,timedelta,timezone
tz=timezone(timedelta(hours=3,minutes=30))
print((datetime.now(timezone.utc)+timedelta(minutes=10)).astimezone(tz).isoformat(timespec='seconds'))
PY
)"
BODY="$(python3 - "$NOTEBOOK_ID" "$START_IRAN" "$SUFFIX" <<'PY'
import json,sys
print(json.dumps({
 'title':f'کمپین کراول {sys.argv[3]}','targetType':'Notebooks','targetIds':[int(sys.argv[1])],
 'startAt':sys.argv[2],
 'steps':[{'content':f'پیام اول کراول {sys.argv[3]}','delayDays':0,'delayHours':0,'delayMinutes':0},
          {'content':f'پیام دوم کراول {sys.argv[3]}','delayDays':0,'delayHours':0,'delayMinutes':1},
          {'content':f'پیام سوم کراول {sys.argv[3]}','delayDays':0,'delayHours':0,'delayMinutes':2}]
},ensure_ascii=False))
PY
)"
HTTP="$(request POST '/api/professional-campaigns' "$TMP_DIR/create.json" "$BODY")"
assert_eq 'create campaign HTTP' '201' "$HTTP"
assert_eq 'create campaign success' 'true' "$(json_get "$TMP_DIR/create.json" success)"
assert_eq 'recipient snapshot count' '1' "$(json_get "$TMP_DIR/create.json" data.recipientsCount)"
assert_eq 'campaign awaits approvals' 'PendingApproval' "$(json_get "$TMP_DIR/create.json" data.status)"
CAMPAIGN_ID="$(json_get "$TMP_DIR/create.json" data.id)"

HTTP="$(request POST "/api/professional-campaigns/${CAMPAIGN_ID}/activate" "$TMP_DIR/activate_early.json")"
assert_eq 'activation before approvals HTTP' '400' "$HTTP"

echo '===== ADMIN APPROVALS ====='
HTTP="$(request GET "/api/Admin/MessageApproval?search=${SUFFIX}&page=1&pageSize=20" "$TMP_DIR/approvals.json")"
assert_eq 'approval list HTTP' '200' "$HTTP"
python3 - "$TMP_DIR/approvals.json" >"$TMP_DIR/approval_ids" <<'PY'
import json,sys
d=json.load(open(sys.argv[1],encoding='utf-8'))
items=(d.get('data') or {}).get('items') or []
ids=[str(x['id']) for x in items if x.get('requestType')=='ProfessionalCampaignStep']
print('\n'.join(ids))
if len(ids)!=3: raise SystemExit(f'expected 3 campaign approvals, got {len(ids)}')
PY
while read -r approval_id; do
  HTTP="$(request POST "/api/Admin/MessageApproval/${approval_id}/approve" "$TMP_DIR/approve-${approval_id}.json")"
  assert_eq "approve text ${approval_id} HTTP" '200' "$HTTP"
done <"$TMP_DIR/approval_ids"

HTTP="$(request POST "/api/professional-campaigns/${CAMPAIGN_ID}/activate" "$TMP_DIR/activate.json")"
assert_eq 'activate campaign HTTP' '200' "$HTTP"
assert_eq 'campaign active' 'Active' "$(json_get "$TMP_DIR/activate.json" data.status)"
python3 - "$TMP_DIR/activate.json" <<'PY'
import json,sys
from datetime import datetime,timezone
d=json.load(open(sys.argv[1],encoding='utf-8'))['data']
times=[datetime.fromisoformat(x['scheduledAtUtc'].replace('Z','+00:00')) for x in d['steps']]
assert all(t.utcoffset().total_seconds()==0 for t in times), times
assert int((times[1]-times[0]).total_seconds())==60, times
assert int((times[2]-times[1]).total_seconds())==120, times
print('PASS: UTC schedule and chained delays are exact')
PY
PASS=$((PASS+1))

HTTP="$(request POST "/api/professional-campaigns/${CAMPAIGN_ID}/pause" "$TMP_DIR/pause.json")"
assert_eq 'pause HTTP' '200' "$HTTP"
HTTP="$(request POST "/api/professional-campaigns/${CAMPAIGN_ID}/resume" "$TMP_DIR/resume.json")"
assert_eq 'resume HTTP' '200' "$HTTP"

echo '===== PAGINATION + OWNERSHIP ====='
HTTP="$(request GET '/api/professional-campaigns?pageNumber=1&pageSize=500' "$TMP_DIR/list_clamp.json")"
assert_eq 'list pageSize=500 HTTP' '200' "$HTTP"
assert_eq 'list pageSize clamped to 100' '100' "$(json_get "$TMP_DIR/list_clamp.json" data.pageSize)"

HTTP="$(request GET "/api/professional-campaigns/${CAMPAIGN_ID}" "$TMP_DIR/own.json")"
assert_eq 'get own campaign HTTP' '200' "$HTTP"

SQL_CONTAINER="${SQL_CONTAINER:-vapp_sqlserver_dev}"
SA_PASSWORD="${SA_PASSWORD:-Vapp@Secure2025!}"
OWNER_UID="$(docker exec "$SQL_CONTAINER" /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$SA_PASSWORD" -C -d DbVapp -h -1 -W -Q "SET NOCOUNT ON; SET QUOTED_IDENTIFIER ON; SELECT TOP 1 CAST(UserId AS NVARCHAR(20)) FROM ProfessionalCampaigns WHERE Id=${CAMPAIGN_ID};" 2>/dev/null | tr -d '\r' | sed '/^$/d' | head -1 | tr -d ' ')"
OTHER_UID="$(docker exec "$SQL_CONTAINER" /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$SA_PASSWORD" -C -d DbVapp -h -1 -W -Q "SET NOCOUNT ON; SET QUOTED_IDENTIFIER ON; SELECT TOP 1 CAST(Id AS NVARCHAR(20)) FROM Users WHERE IsDeleted=0 AND Id<>${OWNER_UID:-0} ORDER BY Id;" 2>/dev/null | tr -d '\r' | sed '/^$/d' | head -1 | tr -d ' ')"
if [[ -z "$OTHER_UID" || ! "$OTHER_UID" =~ ^[0-9]+$ ]]; then
  OTHER_UID="$(docker exec "$SQL_CONTAINER" /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$SA_PASSWORD" -C -d DbVapp -h -1 -W -Q "
SET NOCOUNT ON; SET QUOTED_IDENTIFIER ON;
IF NOT EXISTS (SELECT 1 FROM Users WHERE PhoneNumber=N'09000000002')
BEGIN
  INSERT INTO Users (PhoneNumber, PasswordHash, FullName, IsActive, IsPhoneVerified, IsDeleted, CreatedAt, WalletBalance, CanViewNumberSeekerPhones)
  VALUES (N'09000000002', N'x', N'pc-idor', 1, 1, 0, SYSUTCDATETIME(), 0, 0);
END
SELECT CAST(Id AS NVARCHAR(20)) FROM Users WHERE PhoneNumber=N'09000000002';
" 2>/dev/null | tr -d '\r' | sed '/^$/d' | head -1 | tr -d ' ')"
fi
FOREIGN_CAMPAIGN=""
if [[ -n "$OTHER_UID" && "$OTHER_UID" =~ ^[0-9]+$ && "$OTHER_UID" != "$OWNER_UID" ]]; then
  FOREIGN_CAMPAIGN="$(docker exec "$SQL_CONTAINER" /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$SA_PASSWORD" -C -d DbVapp -h -1 -W -Q "
SET NOCOUNT ON; SET QUOTED_IDENTIFIER ON;
DECLARE @id INT;
INSERT INTO ProfessionalCampaigns (UserId, Title, TargetType, TargetIdsJson, Status, RecipientsCount, IsActive, IsDeleted, CreatedAt)
VALUES (${OTHER_UID}, N'foreign-campaign-crawl', N'Notebooks', N'[0]', N'Cancelled', 0, 0, 0, SYSUTCDATETIME());
SET @id = SCOPE_IDENTITY();
SELECT CAST(@id AS NVARCHAR(20));
" 2>/dev/null | tr -d '\r' | sed '/^$/d' | head -1 | tr -d ' ')"
  if [[ -n "$FOREIGN_CAMPAIGN" && "$FOREIGN_CAMPAIGN" =~ ^[0-9]+$ ]]; then
    HTTP="$(request GET "/api/professional-campaigns/${FOREIGN_CAMPAIGN}" "$TMP_DIR/foreign.json")"
    assert_eq 'IDOR foreign campaign NotFound' '404' "$HTTP"
    docker exec "$SQL_CONTAINER" /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$SA_PASSWORD" -C -d DbVapp -Q "SET QUOTED_IDENTIFIER ON; UPDATE ProfessionalCampaigns SET IsDeleted=1 WHERE Id=${FOREIGN_CAMPAIGN};" >/dev/null 2>&1 || true
  else
    echo "FAIL: seed foreign professional campaign"; FAIL=$((FAIL+1))
  fi
else
  echo "SKIP: no second user for professional IDOR (owner=$OWNER_UID other=$OTHER_UID)"
fi

HTTP="$(request POST "/api/professional-campaigns/${CAMPAIGN_ID}/cancel" "$TMP_DIR/cancel.json")"
assert_eq 'cancel HTTP' '200' "$HTTP"

echo "PASS=$PASS FAIL=$FAIL"
if [[ "$FAIL" -ne 0 ]]; then exit 1; fi
echo 'PROFESSIONAL_CAMPAIGN_CRAWL_OK'
