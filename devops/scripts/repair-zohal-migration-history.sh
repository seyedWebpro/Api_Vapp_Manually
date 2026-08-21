#!/usr/bin/env bash
# Repair EF history when ZohalInquiryLogs table exists but migrations are missing from history.
# Root cause of: "There is already an object named 'ZohalInquiryLogs'" crash-loop.
#
# Usage:
#   bash devops/scripts/repair-zohal-migration-history.sh
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=lib/load-server-conf.sh
source "$SCRIPT_DIR/lib/load-server-conf.sh" 2>/dev/null || true

API_DIR="${API_DIR:-${REMOTE_API_REPO:-$HOME/Api_Vapp_Manually}}"
ENV_FILE="${ENV_FILE:-$API_DIR/docker/.env}"
SQL_CONTAINER="${SQL_CONTAINER:-vapp_sqlserver_prod}"
DB_NAME="${DB_NAME:-DbVapp}"

[[ -f "$ENV_FILE" ]] || { echo "ERROR: missing $ENV_FILE" >&2; exit 1; }
SA="$(grep -E '^SA_PASSWORD=' "$ENV_FILE" | head -1 | cut -d= -f2-)"
SQLCMD="$(docker exec "$SQL_CONTAINER" sh -c 'command -v sqlcmd || ls /opt/mssql-tools18/bin/sqlcmd /opt/mssql-tools/bin/sqlcmd 2>/dev/null | head -1' 2>/dev/null || true)"
[[ -n "$SA" && -n "$SQLCMD" ]] || { echo "ERROR: SA/sqlcmd unavailable" >&2; exit 1; }

echo "=== repair-zohal-migration-history ==="
docker exec "$SQL_CONTAINER" "$SQLCMD" -S localhost -U sa -P "$SA" -C -d "$DB_NAME" -Q "
SET NOCOUNT ON;
IF OBJECT_ID(N'dbo.ZohalInquiryLogs', N'U') IS NULL
BEGIN
  PRINT 'MISS: ZohalInquiryLogs — nothing to repair (Migrate will create)';
END
ELSE
BEGIN
  PRINT 'OK: ZohalInquiryLogs exists';
  IF NOT EXISTS (SELECT 1 FROM dbo.__EFMigrationsHistory WHERE MigrationId = N'20260816133000_AddZohalInquiryLogs')
  BEGIN
    INSERT INTO dbo.__EFMigrationsHistory (MigrationId, ProductVersion)
    VALUES (N'20260816133000_AddZohalInquiryLogs', N'8.0.11');
    PRINT 'INSERTED: 20260816133000_AddZohalInquiryLogs';
  END
  ELSE PRINT 'HAVE: 20260816133000_AddZohalInquiryLogs';

  IF NOT EXISTS (SELECT 1 FROM dbo.__EFMigrationsHistory WHERE MigrationId = N'20260817101312_SyncAppVersionModel')
  BEGIN
    INSERT INTO dbo.__EFMigrationsHistory (MigrationId, ProductVersion)
    VALUES (N'20260817101312_SyncAppVersionModel', N'8.0.11');
    PRINT 'INSERTED: 20260817101312_SyncAppVersionModel';
  END
  ELSE PRINT 'HAVE: 20260817101312_SyncAppVersionModel';
END

IF NOT EXISTS (SELECT 1 FROM dbo.__EFMigrationsHistory WHERE MigrationId = N'20260817120000_UpdateAppVersionPoliciesLatestTo110')
BEGIN
  -- safe to mark applied if AppVersionPolicies already has 1.1.0 rows
  IF EXISTS (SELECT 1 FROM dbo.AppVersionPolicies WHERE LatestVersion = N'1.1.0')
  BEGIN
    INSERT INTO dbo.__EFMigrationsHistory (MigrationId, ProductVersion)
    VALUES (N'20260817120000_UpdateAppVersionPoliciesLatestTo110', N'8.0.11');
    PRINT 'INSERTED: 20260817120000_UpdateAppVersionPoliciesLatestTo110';
  END
END

SELECT MigrationId FROM dbo.__EFMigrationsHistory
WHERE MigrationId LIKE N'%Zohal%' OR MigrationId LIKE N'%SyncAppVersion%' OR MigrationId LIKE N'%AppVersionPolicies%'
ORDER BY MigrationId;
"

echo "OK: repair done — restart API to clear migrate-retry state"
