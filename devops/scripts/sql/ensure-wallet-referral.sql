-- Idempotent ensure script for Wallet Referral system
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF COL_LENGTH('dbo.Users', 'ReferralCode') IS NULL
BEGIN
    ALTER TABLE dbo.Users ADD ReferralCode nvarchar(50) NULL;
END
GO

SET QUOTED_IDENTIFIER ON;
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_Users_ReferralCode' AND object_id = OBJECT_ID(N'dbo.Users'))
BEGIN
    CREATE UNIQUE INDEX IX_Users_ReferralCode
    ON dbo.Users(ReferralCode)
    WHERE [ReferralCode] IS NOT NULL;
END
GO

IF OBJECT_ID(N'dbo.WalletReferralSettings', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.WalletReferralSettings
    (
        Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_WalletReferralSettings PRIMARY KEY,
        IsEnabled bit NOT NULL CONSTRAINT DF_WalletReferralSettings_IsEnabled DEFAULT(1),
        DiscountPercent decimal(5,2) NOT NULL,
        BonusPercent decimal(5,2) NOT NULL,
        DescriptionTemplate nvarchar(1000) NOT NULL,
        IsDeleted bit NOT NULL CONSTRAINT DF_WalletReferralSettings_IsDeleted DEFAULT(0),
        CreatedAt datetime2 NOT NULL CONSTRAINT DF_WalletReferralSettings_CreatedAt DEFAULT(GETUTCDATE()),
        UpdatedAt datetime2 NULL
    );

    CREATE INDEX IX_WalletReferralSettings_IsDeleted ON dbo.WalletReferralSettings(IsDeleted);
END
GO

IF OBJECT_ID(N'dbo.WalletReferralRewards', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.WalletReferralRewards
    (
        Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_WalletReferralRewards PRIMARY KEY,
        PaymentId int NOT NULL,
        BeneficiaryUserId int NOT NULL,
        ReferrerUserId int NOT NULL,
        ReferralCode nvarchar(50) NOT NULL,
        RequestedAmount decimal(18,2) NOT NULL,
        PayableAmount decimal(18,2) NOT NULL,
        DiscountAmount decimal(18,2) NOT NULL,
        DiscountPercent decimal(5,2) NOT NULL,
        BonusAmount decimal(18,2) NOT NULL,
        BonusPercent decimal(5,2) NOT NULL,
        ReferrerWalletTransactionId int NULL,
        IsDeleted bit NOT NULL CONSTRAINT DF_WalletReferralRewards_IsDeleted DEFAULT(0),
        CreatedAt datetime2 NOT NULL CONSTRAINT DF_WalletReferralRewards_CreatedAt DEFAULT(GETUTCDATE()),
        UpdatedAt datetime2 NULL,
        CONSTRAINT FK_WalletReferralRewards_Payments_PaymentId
            FOREIGN KEY (PaymentId) REFERENCES dbo.Payments(Id),
        CONSTRAINT FK_WalletReferralRewards_Users_BeneficiaryUserId
            FOREIGN KEY (BeneficiaryUserId) REFERENCES dbo.Users(Id),
        CONSTRAINT FK_WalletReferralRewards_Users_ReferrerUserId
            FOREIGN KEY (ReferrerUserId) REFERENCES dbo.Users(Id),
        CONSTRAINT FK_WalletReferralRewards_WalletTransactions_ReferrerWalletTransactionId
            FOREIGN KEY (ReferrerWalletTransactionId) REFERENCES dbo.WalletTransactions(Id)
    );

    CREATE UNIQUE INDEX IX_WalletReferralRewards_PaymentId ON dbo.WalletReferralRewards(PaymentId);
    CREATE INDEX IX_WalletReferralRewards_BeneficiaryUserId ON dbo.WalletReferralRewards(BeneficiaryUserId);
    CREATE INDEX IX_WalletReferralRewards_ReferrerUserId ON dbo.WalletReferralRewards(ReferrerUserId);
    CREATE INDEX IX_WalletReferralRewards_CreatedAt ON dbo.WalletReferralRewards(CreatedAt);
    CREATE INDEX IX_WalletReferralRewards_ReferrerWalletTransactionId ON dbo.WalletReferralRewards(ReferrerWalletTransactionId);
END
GO

IF NOT EXISTS (SELECT 1 FROM dbo.WalletReferralSettings WHERE IsDeleted = 0)
BEGIN
    INSERT INTO dbo.WalletReferralSettings (IsEnabled, DiscountPercent, BonusPercent, DescriptionTemplate, IsDeleted, CreatedAt)
    VALUES (
        1, 10, 10,
        N'کافیه کاربر معرفی‌شده این کد رو موقع شارژ کیف پول وارد کنه؛ در این صورت {DiscountPercent}٪ تخفیف براشون اعمال می‌شه و {BonusPercent}٪ پاداش هم به شما واریز می‌شه.',
        0, GETUTCDATE()
    );
END
GO

SET QUOTED_IDENTIFIER ON;
UPDATE dbo.Users
SET ReferralCode = CONCAT('@u', Id),
    UpdatedAt = GETUTCDATE()
WHERE ReferralCode IS NULL AND IsDeleted = 0;
GO

IF OBJECT_ID(N'dbo.__EFMigrationsHistory', N'U') IS NOT NULL
AND NOT EXISTS (
    SELECT 1 FROM dbo.__EFMigrationsHistory
    WHERE MigrationId = N'20260730211910_AddWalletReferralSystem')
BEGIN
    INSERT INTO dbo.__EFMigrationsHistory (MigrationId, ProductVersion)
    VALUES (N'20260730211910_AddWalletReferralSystem', N'9.0.0');
END
GO
