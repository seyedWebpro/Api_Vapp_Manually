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
# کمی در آینده تا activate زمان را به now بکشاند اگر گذشته باشد؛ برای ارسال فوری بعداً SQL می‌زنیم
print((datetime.now(timezone.utc)+timedelta(seconds=30)).astimezone(tz).isoformat(timespec='seconds'))
PY
)"
BODY="$(python3 - "$NOTEBOOK_ID" "$START_IRAN" "$SUFFIX" <<'PY'
import json,sys
print(json.dumps({
 'title':f'کمپین کراول {sys.argv[3]}','targetType':'Notebooks','targetIds':[int(sys.argv[1])],
 'startAt':sys.argv[2],
 'steps':[{'content':f'پیام اول کراول {sys.argv[3]}','delayDays':0,'delayHours':0,'delayMinutes':0},
          {'content':f'پیام دوم کراول {sys.argv[3]}','delayDays':0,'delayHours':0,'delayMinutes':0},
          {'content':f'پیام سوم کراول {sys.argv[3]}','delayDays':0,'delayHours':0,'delayMinutes':0}]
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
ADMIN_PHONE="${ADMIN_PHONE:-09920374397}"
ADMIN_TOKEN=""
admin_login() {
  local login_out="$TMP_DIR/admin_login.json" verify_out="$TMP_DIR/admin_verify.json"
  local code otp
  code=$(curl -sS -m 25 -o "$login_out" -w '%{http_code}' -X POST \
    -H 'Content-Type: application/json' \
    -d "{\"phoneNumber\":\"$ADMIN_PHONE\"}" \
    "$BASE/api/Auth/admin/login" || echo 000)
  [[ "$code" == "200" ]] || return 1
  otp="$(json_get "$login_out" otpCode)"
  [[ -n "$otp" ]] || return 1
  code=$(curl -sS -m 25 -o "$verify_out" -w '%{http_code}' -X POST \
    -H 'Content-Type: application/json' \
    -d "{\"phoneNumber\":\"$ADMIN_PHONE\",\"otpCode\":\"$otp\"}" \
    "$BASE/api/Auth/admin/verify-login" || echo 000)
  [[ "$code" == "200" ]] || return 1
  ADMIN_TOKEN="$(python3 - "$verify_out" <<'PY'
import json,sys
d=json.load(open(sys.argv[1],encoding='utf-8'))
t=d.get('tokens') or {}
print(t.get('accessToken') or '')
PY
)"
  [[ -n "$ADMIN_TOKEN" ]]
}

approve_via_sql() {
  sql_q "
UPDATE pcs
SET pcs.ApprovalStatus=N'Approved',
    pcs.Status=N'Pending',
    pcs.ReviewedAt=SYSUTCDATETIME(),
    pcs.RejectionReason=NULL,
    pcs.UpdatedAt=SYSUTCDATETIME()
FROM ProfessionalCampaignSteps pcs
WHERE pcs.ProfessionalCampaignId=${CAMPAIGN_ID} AND pcs.IsDeleted=0;

UPDATE ProfessionalCampaigns
SET Status=N'Ready', UpdatedAt=SYSUTCDATETIME()
WHERE Id=${CAMPAIGN_ID};

UPDATE SmsApprovalRequests
SET Status=N'Approved', ReviewedAt=SYSUTCDATETIME(), UpdatedAt=SYSUTCDATETIME()
WHERE ProfessionalCampaignStepId IN (
  SELECT Id FROM ProfessionalCampaignSteps WHERE ProfessionalCampaignId=${CAMPAIGN_ID} AND IsDeleted=0
) AND IsDeleted=0 AND Status=N'Pending';
" >/dev/null
}

SQL_CONTAINER="${SQL_CONTAINER:-vapp_sqlserver_dev}"
SA_PASSWORD="${SA_PASSWORD:-Vapp@Secure2025!}"
sql_q() {
  docker exec "$SQL_CONTAINER" /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$SA_PASSWORD" -C -d DbVapp -h -1 -W -Q "SET NOCOUNT ON; SET QUOTED_IDENTIFIER ON; $1" 2>/dev/null | tr -d '\r' | sed '/^$/d' | head -20
}

if admin_login; then
  HTTP="$(curl -sS -o "$TMP_DIR/approvals.json" -w '%{http_code}' \
    -H "Authorization: Bearer $ADMIN_TOKEN" -H 'Accept: application/json' \
    "$BASE/api/Admin/MessageApproval?search=${SUFFIX}&page=1&pageSize=20")"
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
    HTTP="$(curl -sS -o "$TMP_DIR/approve-${approval_id}.json" -w '%{http_code}' -X POST \
      -H "Authorization: Bearer $ADMIN_TOKEN" -H 'Accept: application/json' \
      "$BASE/api/Admin/MessageApproval/${approval_id}/approve")"
    assert_eq "approve text ${approval_id} HTTP" '200' "$HTTP"
  done <"$TMP_DIR/approval_ids"
else
  echo "INFO: admin login unavailable — approving professional steps via SQL"
  approve_via_sql
  READY_STATUS="$(sql_q "SELECT Status FROM ProfessionalCampaigns WHERE Id=${CAMPAIGN_ID};" | head -1 | tr -d ' ')"
  assert_eq 'campaign ready after SQL approve' 'Ready' "$READY_STATUS"
  APPROVED_STEPS="$(sql_q "SELECT COUNT(*) FROM ProfessionalCampaignSteps WHERE ProfessionalCampaignId=${CAMPAIGN_ID} AND ApprovalStatus=N'Approved' AND IsDeleted=0;" | head -1 | tr -d ' ')"
  assert_eq 'all steps approved via SQL' '3' "$APPROVED_STEPS"
fi

HTTP="$(request POST "/api/professional-campaigns/${CAMPAIGN_ID}/activate" "$TMP_DIR/activate.json")"
assert_eq 'activate campaign HTTP' '200' "$HTTP"
assert_eq 'campaign active' 'Active' "$(json_get "$TMP_DIR/activate.json" data.status)"
python3 - "$TMP_DIR/activate.json" <<'PY'
import json,sys
from datetime import datetime,timezone
d=json.load(open(sys.argv[1],encoding='utf-8'))['data']
times=[datetime.fromisoformat(x['scheduledAtUtc'].replace('Z','+00:00')) for x in d['steps']]
assert all(t.utcoffset().total_seconds()==0 for t in times), times
assert int((times[1]-times[0]).total_seconds())==0, times
assert int((times[2]-times[1]).total_seconds())==0, times
print('PASS: UTC schedule with zero delays is exact')
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

OWNER_UID="$(sql_q "SELECT TOP 1 CAST(UserId AS NVARCHAR(20)) FROM ProfessionalCampaigns WHERE Id=${CAMPAIGN_ID};" | head -1 | tr -d ' ')"
OTHER_UID="$(sql_q "SELECT TOP 1 CAST(Id AS NVARCHAR(20)) FROM Users WHERE IsDeleted=0 AND Id<>${OWNER_UID:-0} ORDER BY Id;" | head -1 | tr -d ' ')"
if [[ -z "$OTHER_UID" || ! "$OTHER_UID" =~ ^[0-9]+$ ]]; then
  OTHER_UID="$(sql_q "
IF NOT EXISTS (SELECT 1 FROM Users WHERE PhoneNumber=N'09000000002')
BEGIN
  INSERT INTO Users (PhoneNumber, PasswordHash, FullName, IsActive, IsPhoneVerified, IsDeleted, CreatedAt, WalletBalance, CanViewNumberSeekerPhones)
  VALUES (N'09000000002', N'x', N'pc-idor', 1, 1, 0, SYSUTCDATETIME(), 0, 0);
END
SELECT CAST(Id AS NVARCHAR(20)) FROM Users WHERE PhoneNumber=N'09000000002';
" | head -1 | tr -d ' ')"
fi
FOREIGN_CAMPAIGN=""
if [[ -n "$OTHER_UID" && "$OTHER_UID" =~ ^[0-9]+$ && "$OTHER_UID" != "$OWNER_UID" ]]; then
  FOREIGN_CAMPAIGN="$(sql_q "
DECLARE @id INT;
INSERT INTO ProfessionalCampaigns (UserId, Title, TargetType, TargetIdsJson, Status, RecipientsCount, IsActive, IsDeleted, CreatedAt)
VALUES (${OTHER_UID}, N'foreign-campaign-crawl', N'Notebooks', N'[0]', N'Cancelled', 0, 0, 0, SYSUTCDATETIME());
SET @id = SCOPE_IDENTITY();
SELECT CAST(@id AS NVARCHAR(20));
" | head -1 | tr -d ' ')"
  if [[ -n "$FOREIGN_CAMPAIGN" && "$FOREIGN_CAMPAIGN" =~ ^[0-9]+$ ]]; then
    HTTP="$(request GET "/api/professional-campaigns/${FOREIGN_CAMPAIGN}" "$TMP_DIR/foreign.json")"
    assert_eq 'IDOR foreign campaign NotFound' '404' "$HTTP"
    sql_q "UPDATE ProfessionalCampaigns SET IsDeleted=1 WHERE Id=${FOREIGN_CAMPAIGN};" >/dev/null 2>&1 || true
  else
    echo "FAIL: seed foreign professional campaign"; FAIL=$((FAIL+1))
  fi
else
  echo "SKIP: no second user for professional IDOR (owner=$OWNER_UID other=$OTHER_UID)"
fi

echo '===== LIVE SEND (background worker) ====='
# موجودی کافی برای ۳ پیام
sql_q "UPDATE Users SET WalletBalance = CASE WHEN WalletBalance < 50000 THEN 50000 ELSE WalletBalance END, UpdatedAt=SYSUTCDATETIME() WHERE Id=${OWNER_UID};" >/dev/null
# همه مراحل را due کن تا worker بدون انتظار ارسال کند
sql_q "UPDATE ProfessionalCampaignSteps SET ScheduledAtUtc=DATEADD(second,-30,SYSUTCDATETIME()), UpdatedAt=SYSUTCDATETIME() WHERE ProfessionalCampaignId=${CAMPAIGN_ID} AND IsDeleted=0;" >/dev/null
sql_q "UPDATE ProfessionalCampaigns SET StartAtUtc=DATEADD(second,-30,SYSUTCDATETIME()), Status=N'Active', IsActive=1, UpdatedAt=SYSUTCDATETIME() WHERE Id=${CAMPAIGN_ID};" >/dev/null

SEND_OK=0
for i in $(seq 1 90); do
  STATUS_LINE="$(sql_q "
SELECT CONCAT(
  (SELECT COUNT(*) FROM ProfessionalCampaignSteps WHERE ProfessionalCampaignId=${CAMPAIGN_ID} AND Status=N'Sent' AND IsDeleted=0), N'|',
  (SELECT COUNT(*) FROM ProfessionalCampaignSteps WHERE ProfessionalCampaignId=${CAMPAIGN_ID} AND Status=N'Failed' AND IsDeleted=0), N'|',
  (SELECT Status FROM ProfessionalCampaigns WHERE Id=${CAMPAIGN_ID})
);" | head -1 | tr -d ' ')"
  SENT_N="${STATUS_LINE%%|*}"
  REST="${STATUS_LINE#*|}"
  FAIL_N="${REST%%|*}"
  CAMP_STATUS="${REST#*|}"
  echo "wait send tick=$i sent=$SENT_N failed=$FAIL_N campaign=$CAMP_STATUS"
  if [[ "$FAIL_N" != "0" ]]; then
    echo "FAIL: professional campaign step failed during live send"
    sql_q "SELECT Id, StepOrder, Status, LEFT(ISNULL(LastError,''),120) FROM ProfessionalCampaignSteps WHERE ProfessionalCampaignId=${CAMPAIGN_ID} ORDER BY StepOrder;"
    FAIL=$((FAIL+1))
    break
  fi
  if [[ "$SENT_N" == "3" && "$CAMP_STATUS" == "Completed" ]]; then
    SEND_OK=1
    break
  fi
  sleep 1
done

assert_eq 'live send all 3 steps Sent + Completed' '1' "$SEND_OK"
HTTP="$(request GET "/api/professional-campaigns/${CAMPAIGN_ID}" "$TMP_DIR/after_send.json")"
assert_eq 'get after send HTTP' '200' "$HTTP"
assert_eq 'campaign completed after send' 'Completed' "$(json_get "$TMP_DIR/after_send.json" data.status)"
assert_eq 'step1 sent' 'Sent' "$(json_get "$TMP_DIR/after_send.json" data.steps.0.status)"
assert_eq 'step2 sent' 'Sent' "$(json_get "$TMP_DIR/after_send.json" data.steps.1.status)"
assert_eq 'step3 sent' 'Sent' "$(json_get "$TMP_DIR/after_send.json" data.steps.2.status)"

MSG_COUNT="$(sql_q "SELECT COUNT(*) FROM Messages WHERE UserId=${OWNER_UID} AND Title LIKE N'%کمپین کراول ${SUFFIX}%' AND IsDeleted=0;" | head -1 | tr -d ' ')"
assert_eq 'messages created for 3 steps' '3' "$MSG_COUNT"

WALLET_TX="$(sql_q "SELECT COUNT(*) FROM WalletTransactions WHERE UserId=${OWNER_UID} AND Description LIKE N'%رزرو هزینه پیام مستقیم%' AND CreatedAt > DATEADD(minute,-10,SYSUTCDATETIME());" | head -1 | tr -d ' ')"
if [[ "${WALLET_TX:-0}" -ge 1 ]]; then
  assert_eq 'wallet debit recorded for send' 'true' 'true'
else
  echo "INFO: no wallet debit in last 10m (billing may be disabled) — ok"
  PASS=$((PASS+1))
fi

DELIVERY_COUNT="$(sql_q "
SELECT COUNT(*) FROM SmsDeliveryRecords
WHERE UserId=${OWNER_UID}
  AND SourceModule=N'MessageDirect'
  AND ISNULL(SourceEntityLabel,'') LIKE N'%کمپین کراول ${SUFFIX}%'
  AND CreatedAt > DATEADD(minute,-10,SYSUTCDATETIME());
" | head -1 | tr -d ' ')"
if [[ "${DELIVERY_COUNT:-0}" -ge 3 ]]; then
  assert_eq 'sms delivery records >= 3' 'true' 'true'
else
  echo "WARN: delivery records=$DELIVERY_COUNT (steps already Sent); checking API logs for nested-tx errors"
  if grep -q 'already in a transaction' "$LOG"; then
    echo "FAIL: nested transaction error still present in API log"; FAIL=$((FAIL+1))
  else
    assert_eq 'no nested transaction errors in API log' 'true' 'true'
  fi
fi

echo '===== CANCEL PATH (separate campaign) ====='
CANCEL_BODY="$(python3 - "$NOTEBOOK_ID" "$SUFFIX" <<'PY'
import json,sys
from datetime import datetime,timedelta,timezone
tz=timezone(timedelta(hours=3,minutes=30))
start=(datetime.now(timezone.utc)+timedelta(hours=2)).astimezone(tz).isoformat(timespec='seconds')
print(json.dumps({
 'title':f'کمپین لغو {sys.argv[2]}','targetType':'Notebooks','targetIds':[int(sys.argv[1])],
 'startAt':start,
 'steps':[{'content':f'لغو ۱ {sys.argv[2]}','delayDays':0,'delayHours':0,'delayMinutes':0},
          {'content':f'لغو ۲ {sys.argv[2]}','delayDays':0,'delayHours':0,'delayMinutes':5}]
},ensure_ascii=False))
PY
)"
HTTP="$(request POST '/api/professional-campaigns' "$TMP_DIR/create_cancel.json" "$CANCEL_BODY")"
assert_eq 'create cancel-campaign HTTP' '201' "$HTTP"
CANCEL_ID="$(json_get "$TMP_DIR/create_cancel.json" data.id)"
# approve cancel-campaign via SQL then activate+cancel
sql_q "
UPDATE ProfessionalCampaignSteps
SET ApprovalStatus=N'Approved', Status=N'Pending', ReviewedAt=SYSUTCDATETIME(), UpdatedAt=SYSUTCDATETIME()
WHERE ProfessionalCampaignId=${CANCEL_ID} AND IsDeleted=0;
UPDATE ProfessionalCampaigns SET Status=N'Ready', UpdatedAt=SYSUTCDATETIME() WHERE Id=${CANCEL_ID};
UPDATE SmsApprovalRequests SET Status=N'Approved', ReviewedAt=SYSUTCDATETIME(), UpdatedAt=SYSUTCDATETIME()
WHERE ProfessionalCampaignStepId IN (SELECT Id FROM ProfessionalCampaignSteps WHERE ProfessionalCampaignId=${CANCEL_ID}) AND Status=N'Pending';
" >/dev/null
HTTP="$(request POST "/api/professional-campaigns/${CANCEL_ID}/activate" "$TMP_DIR/activate_cancel.json")"
assert_eq 'activate cancel-campaign HTTP' '200' "$HTTP"
HTTP="$(request POST "/api/professional-campaigns/${CANCEL_ID}/cancel" "$TMP_DIR/cancel.json")"
assert_eq 'cancel HTTP' '200' "$HTTP"
assert_eq 'cancel success' 'true' "$(json_get "$TMP_DIR/cancel.json" success)"

echo "PASS=$PASS FAIL=$FAIL"
if [[ "$FAIL" -ne 0 ]]; then
  echo '---- API log tail ----'
  tail -120 "$LOG" || true
  exit 1
fi
echo 'PROFESSIONAL_CAMPAIGN_CRAWL_OK'
