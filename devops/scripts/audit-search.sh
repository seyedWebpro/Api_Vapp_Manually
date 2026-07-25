#!/usr/bin/env bash
# جستجوی AdminAuditLogs — معادل grep لاگ فایل، روی جدول SQL
#
# Usage:
#   bash devops/scripts/audit-search.sh --entity-type SubscriptionPlan --entity-id 12
#   bash devops/scripts/audit-search.sh --action SubscriptionPlan.PriceUpdated
#   bash devops/scripts/audit-search.sh --actor 5 --from 2026-07-25
#   bash devops/scripts/audit-search.sh --correlation-id 0HN...
#   bash devops/scripts/audit-search.sh --q "PriceUpdated"   # فقط Action/EntityId/Correlation/Error (سریع)
#   bash devops/scripts/audit-search.sh --category payment --lines 100
#   bash devops/scripts/audit-search.sh --sql-only   # فقط چاپ SQL
#
# Env:
#   AUDIT_SQL_CONNECTION  — connection string کامل (اختیاری)
#   MSSQL_SA_PASSWORD     — اگر از docker exec استفاده شود
#
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/../.." && pwd)"

CATEGORY="" ACTION="" ENTITY_TYPE="" ENTITY_ID=""
ACTOR="" TARGET="" CORRELATION="" SOURCE="" Q=""
FROM_UTC="" TO_UTC="" LINES=50 SQL_ONLY=0
CONNECTION_STRING="${AUDIT_SQL_CONNECTION:-}"

usage() { sed -n '2,16p' "$0"; exit 1; }

while [[ $# -gt 0 ]]; do
  case "$1" in
    --category) CATEGORY="$2"; shift 2 ;;
    --action) ACTION="$2"; shift 2 ;;
    --entity-type) ENTITY_TYPE="$2"; shift 2 ;;
    --entity-id) ENTITY_ID="$2"; shift 2 ;;
    --actor) ACTOR="$2"; shift 2 ;;
    --target) TARGET="$2"; shift 2 ;;
    --correlation-id) CORRELATION="$2"; shift 2 ;;
    --source) SOURCE="$2"; shift 2 ;;
    --q) Q="$2"; shift 2 ;;
    --from) FROM_UTC="$2"; shift 2 ;;
    --to) TO_UTC="$2"; shift 2 ;;
    --lines|-n) LINES="$2"; shift 2 ;;
    --connection) CONNECTION_STRING="$2"; shift 2 ;;
    --sql-only) SQL_ONLY=1; shift ;;
    -h|--help) usage ;;
    *) echo "Unknown arg: $1"; usage ;;
  esac
done

sql_escape() { printf "%s" "$1" | sed "s/'/''/g"; }

WHERE="1=1"
[[ -n "$CATEGORY" ]] && WHERE+=" AND Category = N'$(sql_escape "$CATEGORY")'"
[[ -n "$ACTION" ]] && WHERE+=" AND Action = N'$(sql_escape "$ACTION")'"
[[ -n "$ENTITY_TYPE" ]] && WHERE+=" AND EntityType = N'$(sql_escape "$ENTITY_TYPE")'"
[[ -n "$ENTITY_ID" ]] && WHERE+=" AND EntityId = N'$(sql_escape "$ENTITY_ID")'"
[[ -n "$ACTOR" ]] && WHERE+=" AND ActorUserId = $ACTOR"
[[ -n "$TARGET" ]] && WHERE+=" AND TargetUserId = $TARGET"
[[ -n "$CORRELATION" ]] && WHERE+=" AND CorrelationId = N'$(sql_escape "$CORRELATION")'"
[[ -n "$SOURCE" ]] && WHERE+=" AND Source = N'$(sql_escape "$SOURCE")'"
[[ -n "$FROM_UTC" ]] && WHERE+=" AND CreatedAt >= '$(sql_escape "$FROM_UTC")'"
[[ -n "$TO_UTC" ]] && WHERE+=" AND CreatedAt <= '$(sql_escape "$TO_UTC")'"
if [[ -n "$Q" ]]; then
  QE="$(sql_escape "$Q")"
  WHERE+=" AND (Action LIKE N'%${QE}%' OR EntityId LIKE N'%${QE}%' OR ISNULL(OldValue,'') LIKE N'%${QE}%' OR ISNULL(NewValue,'') LIKE N'%${QE}%' OR ISNULL(Metadata,'') LIKE N'%${QE}%' OR ISNULL(ErrorMessage,'') LIKE N'%${QE}%')"
fi

SQL=$(cat <<EOF
SELECT TOP ($LINES)
  Id, CreatedAt, Category, Action, EntityType, EntityId,
  ActorUserId, TargetUserId, Succeeded, Source, CorrelationId,
  IpAddress, RequestPath, HttpMethod,
  LEFT(ISNULL(OldValue,''), 240) AS OldValuePreview,
  LEFT(ISNULL(NewValue,''), 240) AS NewValuePreview,
  LEFT(ISNULL(Metadata,''), 200) AS MetadataPreview,
  LEFT(ISNULL(ErrorMessage,''), 120) AS ErrorMessage
FROM AdminAuditLogs
WHERE $WHERE
ORDER BY CreatedAt DESC, Id DESC;
EOF
)

echo "── AdminAuditLogs (TOP $LINES) ──"
echo "$SQL"
echo "────────────────────────────────"

[[ "$SQL_ONLY" -eq 1 ]] && exit 0

# Resolve connection string from appsettings if needed
if [[ -z "$CONNECTION_STRING" ]]; then
  for f in \
    "$ROOT_DIR/appsettings.Development.Local.json" \
    "$ROOT_DIR/appsettings.Development.Mac.json" \
    "$ROOT_DIR/appsettings.Development.Windows.json" \
    "$ROOT_DIR/appsettings.Development.json"
  do
    if [[ -f "$f" ]] && command -v python3 >/dev/null 2>&1; then
      CONNECTION_STRING="$(python3 -c "
import json
data=json.load(open('$f'))
cs=data.get('ConnectionStrings',{})
print(cs.get('DefaultConnection') or cs.get('LocalDocker') or '')
")"
      [[ -n "$CONNECTION_STRING" ]] && break
    fi
  done
fi

parse_cs() {
  python3 -c "
import re, sys
cs = sys.argv[1]
def g(*keys):
    for k in keys:
        m = re.search(rf'{k}=([^;]+)', cs, re.I)
        if m: return m.group(1)
    return ''
print(g('Server','Data Source'))
print(g('Database','Initial Catalog'))
print(g('User Id','UID','User'))
print(g('Password','PWD'))
" "$1"
}

run_sqlcmd() {
  local server="$1" db="$2" user="$3" pass="$4"
  if [[ -n "$user" && -n "$pass" ]]; then
    sqlcmd -C -S "$server" -d "$db" -U "$user" -P "$pass" -Q "$SQL" -s "|" -W
  else
    sqlcmd -C -S "$server" -d "$db" -E -Q "$SQL" -s "|" -W
  fi
}

if command -v sqlcmd >/dev/null 2>&1 && [[ -n "$CONNECTION_STRING" ]]; then
  mapfile -t PARTS < <(parse_cs "$CONNECTION_STRING")
  run_sqlcmd "${PARTS[0]}" "${PARTS[1]}" "${PARTS[2]}" "${PARTS[3]}"
  exit 0
fi

# Docker fallback (local SQL container)
if command -v docker >/dev/null 2>&1; then
  CNAME="$(docker ps --format '{{.Names}}' 2>/dev/null | grep -iE 'sql|mssql' | head -1 || true)"
  if [[ -n "$CNAME" ]]; then
    DB_NAME="VappDb"
    [[ -n "$CONNECTION_STRING" ]] && DB_NAME="$(parse_cs "$CONNECTION_STRING" | sed -n '2p')"
    PASS="${MSSQL_SA_PASSWORD:-Your_password123}"
    echo "Using docker exec: $CNAME (db=$DB_NAME)"
    for TOOL in /opt/mssql-tools18/bin/sqlcmd /opt/mssql-tools/bin/sqlcmd; do
      if docker exec "$CNAME" test -x "$TOOL" 2>/dev/null; then
        docker exec -i "$CNAME" "$TOOL" -C -S localhost -U sa -P "$PASS" -d "$DB_NAME" -Q "$SQL" -s "|" -W
        exit 0
      fi
    done
  fi
fi

echo "Could not execute automatically. Copy the SQL above into SSMS / Azure Data Studio,"
echo "or install sqlcmd and set AUDIT_SQL_CONNECTION."
exit 0
