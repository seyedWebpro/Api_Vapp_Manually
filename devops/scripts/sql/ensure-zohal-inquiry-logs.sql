-- ایجاد جدول ZohalInquiryLogs در صورت نبود (idempotent)
IF OBJECT_ID(N'dbo.ZohalInquiryLogs', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ZohalInquiryLogs (
        Id BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        InquiryType NVARCHAR(40) NOT NULL,
        Source NVARCHAR(40) NOT NULL,
        MobileMasked NVARCHAR(20) NOT NULL,
        NationalCodeMasked NVARCHAR(20) NOT NULL,
        Matched BIT NULL,
        HttpStatusCode INT NULL,
        ZohalResultCode INT NULL,
        ProviderErrorCode NVARCHAR(120) NULL,
        ProviderMessage NVARCHAR(1000) NULL,
        OutcomeStatus NVARCHAR(40) NOT NULL,
        UserFacingErrorCode NVARCHAR(64) NULL,
        RequestJson NVARCHAR(MAX) NULL,
        ResponseJson NVARCHAR(MAX) NULL,
        DurationMs INT NOT NULL,
        Succeeded BIT NOT NULL,
        TraceId NVARCHAR(128) NULL,
        IpAddress NVARCHAR(45) NULL,
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_ZohalInquiryLogs_CreatedAt DEFAULT (GETUTCDATE())
    );

    CREATE INDEX IX_ZohalInquiryLogs_CreatedAt ON dbo.ZohalInquiryLogs (CreatedAt);
    CREATE INDEX IX_ZohalInquiryLogs_TraceId ON dbo.ZohalInquiryLogs (TraceId);
    CREATE INDEX IX_ZohalInquiryLogs_MobileMasked ON dbo.ZohalInquiryLogs (MobileMasked);
    CREATE INDEX IX_ZohalInquiryLogs_InquiryType_CreatedAt ON dbo.ZohalInquiryLogs (InquiryType, CreatedAt);
    CREATE INDEX IX_ZohalInquiryLogs_OutcomeStatus_CreatedAt ON dbo.ZohalInquiryLogs (OutcomeStatus, CreatedAt);
END;

IF NOT EXISTS (SELECT 1 FROM dbo.__EFMigrationsHistory WHERE MigrationId = N'20260816133000_AddZohalInquiryLogs')
BEGIN
    INSERT INTO dbo.__EFMigrationsHistory (MigrationId, ProductVersion)
    VALUES (N'20260816133000_AddZohalInquiryLogs', N'8.0.0');
END;
