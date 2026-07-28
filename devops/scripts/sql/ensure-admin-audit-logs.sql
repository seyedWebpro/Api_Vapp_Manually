-- Apply AdminAuditLogs + history row (idempotent) — fallback if API Migrate not yet run
IF OBJECT_ID(N'dbo.AdminAuditLogs', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[AdminAuditLogs] (
        [Id] bigint NOT NULL IDENTITY(1,1),
        [Category] nvarchar(50) NOT NULL,
        [Action] nvarchar(120) NOT NULL,
        [EntityType] nvarchar(100) NOT NULL,
        [EntityId] nvarchar(64) NULL,
        [ActorUserId] int NULL,
        [TargetUserId] int NULL,
        [OldValue] nvarchar(max) NULL,
        [NewValue] nvarchar(max) NULL,
        [Metadata] nvarchar(max) NULL,
        [CorrelationId] nvarchar(64) NULL,
        [IpAddress] nvarchar(45) NULL,
        [UserAgent] nvarchar(512) NULL,
        [RequestPath] nvarchar(500) NULL,
        [HttpMethod] nvarchar(16) NULL,
        [Source] nvarchar(20) NOT NULL CONSTRAINT [DF_AdminAuditLogs_Source] DEFAULT N'Http',
        [Succeeded] bit NOT NULL CONSTRAINT [DF_AdminAuditLogs_Succeeded] DEFAULT CAST(1 AS bit),
        [ErrorMessage] nvarchar(1000) NULL,
        [CreatedAt] datetime2 NOT NULL CONSTRAINT [DF_AdminAuditLogs_CreatedAt] DEFAULT (GETUTCDATE()),
        CONSTRAINT [PK_AdminAuditLogs] PRIMARY KEY ([Id])
    );

    CREATE INDEX [IX_AdminAuditLogs_Action] ON [dbo].[AdminAuditLogs] ([Action]);
    CREATE INDEX [IX_AdminAuditLogs_ActorUserId] ON [dbo].[AdminAuditLogs] ([ActorUserId]);
    CREATE INDEX [IX_AdminAuditLogs_ActorUserId_CreatedAt] ON [dbo].[AdminAuditLogs] ([ActorUserId], [CreatedAt]);
    CREATE INDEX [IX_AdminAuditLogs_Category] ON [dbo].[AdminAuditLogs] ([Category]);
    CREATE INDEX [IX_AdminAuditLogs_Category_CreatedAt] ON [dbo].[AdminAuditLogs] ([Category], [CreatedAt]);
    CREATE INDEX [IX_AdminAuditLogs_CorrelationId] ON [dbo].[AdminAuditLogs] ([CorrelationId]);
    CREATE INDEX [IX_AdminAuditLogs_CreatedAt] ON [dbo].[AdminAuditLogs] ([CreatedAt]);
    CREATE INDEX [IX_AdminAuditLogs_EntityType_EntityId] ON [dbo].[AdminAuditLogs] ([EntityType], [EntityId]);
END
GO

IF NOT EXISTS (SELECT 1 FROM [dbo].[__EFMigrationsHistory] WHERE [MigrationId] = N'20260725221445_AddAdminAuditLogs')
    INSERT INTO [dbo].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260725221445_AddAdminAuditLogs', N'9.0.4');
GO

-- Full-Text optional (some SQL images lack FTS — never fail the script)
BEGIN TRY
    IF SERVERPROPERTY('IsFullTextInstalled') = 1
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM sys.fulltext_catalogs WHERE name = N'AdminAuditLogsCatalog')
            CREATE FULLTEXT CATALOG AdminAuditLogsCatalog AS DEFAULT;

        IF OBJECT_ID(N'dbo.AdminAuditLogs', N'U') IS NOT NULL
           AND NOT EXISTS (
                SELECT 1 FROM sys.fulltext_indexes i
                INNER JOIN sys.objects o ON i.object_id = o.object_id
                WHERE o.name = N'AdminAuditLogs')
        BEGIN
            CREATE FULLTEXT INDEX ON dbo.AdminAuditLogs
            (OldValue LANGUAGE 0, NewValue LANGUAGE 0, Metadata LANGUAGE 0, ErrorMessage LANGUAGE 0)
            KEY INDEX PK_AdminAuditLogs ON AdminAuditLogsCatalog WITH CHANGE_TRACKING AUTO;
        END
    END
END TRY
BEGIN CATCH
    PRINT 'Full-Text skipped: ' + ERROR_MESSAGE();
END CATCH
GO

IF NOT EXISTS (SELECT 1 FROM [dbo].[__EFMigrationsHistory] WHERE [MigrationId] = N'20260726103716_AddAdminAuditLogsFullText')
    INSERT INTO [dbo].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260726103716_AddAdminAuditLogsFullText', N'9.0.4');
GO
