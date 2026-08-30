-- به‌روزرسانی سیاست نسخه اپ → latest=1.1.0 ، min=1.0.0 (آپدیت اختیاری)
-- Usage (روی سرور — فقط DbVapp در Docker production):
--   docker exec vapp_sqlserver_prod /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P "$SA_PASSWORD" -C -d DbVapp -I \
--     -i devops/scripts/sql/update-app-version-1.1.0.sql

SET QUOTED_IDENTIFIER ON;
GO

UPDATE AppVersionPolicies
SET LatestVersion = N'1.1.0',
    MinSupportedVersion = N'1.0.0',
    IsActive = 1,
    UpdatedAt = SYSUTCDATETIME()
WHERE IsDeleted = 0;
GO

SELECT Platform, LatestVersion, MinSupportedVersion, IsActive, UpdatedAt
FROM AppVersionPolicies
WHERE IsDeleted = 0;
GO
