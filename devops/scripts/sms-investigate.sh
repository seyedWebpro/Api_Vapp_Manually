#!/usr/bin/env bash
# بررسی سریع وضعیت پیامک یک کاربر — Audit + دیتابیس + لاگ فایل
#
# Usage:
#   bash devops/scripts/sms-investigate.sh --user-id 3
#   bash devops/scripts/sms-investigate.sh --mobile 09229616084
#   bash devops/scripts/sms-investigate.sh --user-id 3 --days 3
#
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/../.." && pwd)"

USER_ID="" MOBILE="" DAYS=2 LINES=30

usage() { sed -n '2,10p' "$0"; exit 1; }

while [[ $# -gt 0 ]]; do
  case "$1" in
    --user-id) USER_ID="$2"; shift 2 ;;
    --mobile) MOBILE="$2"; shift 2 ;;
    --days) DAYS="$2"; shift 2 ;;
    --lines|-n) LINES="$2"; shift 2 ;;
    -h|--help) usage ;;
    *) echo "Unknown arg: $1"; usage ;;
  esac
done

if [[ -z "$USER_ID" && -z "$MOBILE" ]]; then
  echo "Provide --user-id or --mobile"
  usage
fi

sql_escape() { printf "%s" "$1" | sed "s/'/''/g"; }

run_sql() {
  local sql="$1"
  local pass db cname tool
  cname="$(docker ps --format '{{.Names}}' 2>/dev/null | grep -iE 'sql|mssql' | head -1 || true)"
  [[ -z "$cname" ]] && { echo "SQL container not found"; return 1; }
  db="DbVapp"
  pass="${MSSQL_SA_PASSWORD:-}"
  [[ -z "$pass" && -f "$ROOT_DIR/docker/.env" ]] && pass="$(grep -E '^(MSSQL_SA_PASSWORD|SA_PASSWORD)=' "$ROOT_DIR/docker/.env" | head -1 | cut -d= -f2-)"
  [[ -z "$pass" ]] && pass="Vapp@Secure2025!"
  for tool in /opt/mssql-tools/bin/sqlcmd /opt/mssql-tools18/bin/sqlcmd; do
    if docker exec "$cname" test -x "$tool" 2>/dev/null; then
      docker exec -i "$cname" "$tool" -C -S localhost -U sa -P "$pass" -d "$db" -Q "$sql" -s "|" -W
      return 0
    fi
  done
  echo "sqlcmd not found in container $cname"
  return 1
}

if [[ -n "$MOBILE" && -z "$USER_ID" ]]; then
  MOBILE_ESC="$(sql_escape "$MOBILE")"
  echo "═══ Resolve user by mobile: $MOBILE ═══"
  run_sql "SELECT TOP 5 Id, FullName, PhoneNumber, WalletBalance FROM Users WHERE PhoneNumber = N'$MOBILE_ESC';"
  USER_ID="$(run_sql "SET NOCOUNT ON; SELECT TOP 1 Id FROM Users WHERE PhoneNumber = N'$MOBILE_ESC';" | awk -F'|' '/^[0-9]+$/ {print $1; exit}')"
fi

if [[ -z "$USER_ID" ]]; then
  echo "Could not resolve UserId"
  exit 1
fi

echo ""
echo "═══ User #$USER_ID ═══"
run_sql "SELECT Id, FullName, PhoneNumber, WalletBalance, CreatedAt FROM Users WHERE Id = $USER_ID;"

echo ""
echo "═══ SMS Audit (last $LINES) ═══"
cd "$ROOT_DIR"
bash devops/scripts/audit-search.sh --category sms --actor "$USER_ID" --lines "$LINES" 2>/dev/null | tail -n +1

echo ""
echo "═══ SmsDeliveryRecords (last 20) ═══"
run_sql "SELECT TOP 20 Id, SourceModule, Mobile, Sid, SendStatus, DeliveryCategory, ProviderStatusCode, LEFT(ISNULL(ProviderStatusMessage,''),60) AS StatusMsg, SentAt, CheckAttempts, IsDeliveryFinal FROM SmsDeliveryRecords WHERE UserId = $USER_ID ORDER BY Id DESC;"

echo ""
echo "═══ WalletTransactions (last 15) ═══"
run_sql "SELECT TOP 15 Id, Amount, Type, Title, LEFT(Description,80) AS DescPreview, CreatedAt FROM WalletTransactions WHERE UserId = $USER_ID ORDER BY Id DESC;"

echo ""
echo "═══ File log grep (last $DAYS day(s)) — SMS_BILLING / insufficient ═══"
for ((d=0; d<DAYS; d++)); do
  f="$ROOT_DIR/log/log-$(date -v-${d}d +%Y%m%d 2>/dev/null || date -d "-${d} days" +%Y%m%d).txt"
  [[ -f "$f" ]] || continue
  echo "--- $f ---"
  grep -E "UserId[=: ]${USER_ID}\b|userId=${USER_ID}\b|owner ${USER_ID}\b|SMS_BILLING.*userId=${USER_ID}|insufficient wallet" "$f" 2>/dev/null | tail -40 || true
done

echo ""
echo "═══ Hint ═══"
echo "• SkippedInsufficientBalance → کیف پول کافی نبود"
echo "• ProviderFailed → پنل پیامک رد کرد (لاگ SMS_BILLING outcome=ProviderFailed)"
echo "• DeliveryCategory=PendingSync → هنوز وضعیت دلیوری sync نشده"
