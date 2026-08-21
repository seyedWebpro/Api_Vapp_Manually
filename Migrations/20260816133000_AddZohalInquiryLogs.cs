using Api_Vapp.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api_Vapp.Migrations
{
    /// <summary>
    /// Idempotent: table may already exist from devops SQL or a duplicate SyncAppVersionModel migration.
    /// </summary>
    [DbContext(typeof(Api_Context))]
    [Migration("20260816133000_AddZohalInquiryLogs")]
    public partial class AddZohalInquiryLogs : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF OBJECT_ID(N'dbo.ZohalInquiryLogs', N'U') IS NULL
                BEGIN
                    CREATE TABLE [dbo].[ZohalInquiryLogs] (
                        [Id] bigint NOT NULL IDENTITY(1,1),
                        [InquiryType] nvarchar(40) NOT NULL,
                        [Source] nvarchar(40) NOT NULL,
                        [MobileMasked] nvarchar(20) NOT NULL,
                        [NationalCodeMasked] nvarchar(20) NOT NULL,
                        [Matched] bit NULL,
                        [HttpStatusCode] int NULL,
                        [ZohalResultCode] int NULL,
                        [ProviderErrorCode] nvarchar(120) NULL,
                        [ProviderMessage] nvarchar(1000) NULL,
                        [OutcomeStatus] nvarchar(40) NOT NULL,
                        [UserFacingErrorCode] nvarchar(64) NULL,
                        [RequestJson] nvarchar(max) NULL,
                        [ResponseJson] nvarchar(max) NULL,
                        [DurationMs] int NOT NULL,
                        [Succeeded] bit NOT NULL,
                        [TraceId] nvarchar(128) NULL,
                        [IpAddress] nvarchar(45) NULL,
                        [CreatedAt] datetime2 NOT NULL CONSTRAINT [DF_ZohalInquiryLogs_CreatedAt] DEFAULT (GETUTCDATE()),
                        CONSTRAINT [PK_ZohalInquiryLogs] PRIMARY KEY ([Id])
                    );
                END

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ZohalInquiryLogs_CreatedAt' AND object_id = OBJECT_ID(N'dbo.ZohalInquiryLogs'))
                    CREATE INDEX [IX_ZohalInquiryLogs_CreatedAt] ON [dbo].[ZohalInquiryLogs] ([CreatedAt]);
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ZohalInquiryLogs_TraceId' AND object_id = OBJECT_ID(N'dbo.ZohalInquiryLogs'))
                    CREATE INDEX [IX_ZohalInquiryLogs_TraceId] ON [dbo].[ZohalInquiryLogs] ([TraceId]);
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ZohalInquiryLogs_MobileMasked' AND object_id = OBJECT_ID(N'dbo.ZohalInquiryLogs'))
                    CREATE INDEX [IX_ZohalInquiryLogs_MobileMasked] ON [dbo].[ZohalInquiryLogs] ([MobileMasked]);
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ZohalInquiryLogs_InquiryType_CreatedAt' AND object_id = OBJECT_ID(N'dbo.ZohalInquiryLogs'))
                    CREATE INDEX [IX_ZohalInquiryLogs_InquiryType_CreatedAt] ON [dbo].[ZohalInquiryLogs] ([InquiryType], [CreatedAt]);
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ZohalInquiryLogs_OutcomeStatus_CreatedAt' AND object_id = OBJECT_ID(N'dbo.ZohalInquiryLogs'))
                    CREATE INDEX [IX_ZohalInquiryLogs_OutcomeStatus_CreatedAt] ON [dbo].[ZohalInquiryLogs] ([OutcomeStatus], [CreatedAt]);
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF OBJECT_ID(N'dbo.ZohalInquiryLogs', N'U') IS NOT NULL
                    DROP TABLE [dbo].[ZohalInquiryLogs];
                """);
        }
    }
}
