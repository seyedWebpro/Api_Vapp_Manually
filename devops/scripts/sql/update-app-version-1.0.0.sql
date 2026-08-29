-- هم‌ترازی سیاست نسخه اپ با بیلد فعلی سیب‌اپ (1.0.0)
-- تا check برای currentVersion=1.0.0 → updateType=none برگردد (بدون دیالوگ به‌روزرسانی).
-- Usage (روی سرور) — حتماً -I برای QUOTED_IDENTIFIER:
--   sqlcmd -S ... -d DbVapp -U sa -P ... -C -I -i devops/scripts/sql/update-app-version-1.0.0.sql

SET QUOTED_IDENTIFIER ON;
GO

UPDATE AppVersionPolicies
SET LatestVersion = N'1.0.0',
    MinSupportedVersion = N'1.0.0',
    IsActive = 1,
    UpdatedAt = SYSUTCDATETIME()
WHERE IsDeleted = 0;
GO

SELECT Platform, LatestVersion, MinSupportedVersion, IsActive, UpdatedAt
FROM AppVersionPolicies
WHERE IsDeleted = 0;
GO
