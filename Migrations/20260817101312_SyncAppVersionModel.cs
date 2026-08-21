using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api_Vapp.Migrations
{
    /// <inheritdoc />
    /// <remarks>
    /// Historically this migration duplicated AddZohalInquiryLogs (same CreateTable).
    /// Up is now a no-op so existing DBs with the table already present do not crash Migrate().
    /// </remarks>
    public partial class SyncAppVersionModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Intentionally empty / idempotent: ZohalInquiryLogs is owned by
            // 20260816133000_AddZohalInquiryLogs. Keep history row for this migration id.
            migrationBuilder.Sql("""
                IF OBJECT_ID(N'dbo.ZohalInquiryLogs', N'U') IS NULL
                BEGIN
                    -- Safety net if AddZohalInquiryLogs was skipped in history
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
                        [CreatedAt] datetime2 NOT NULL CONSTRAINT [DF_ZohalInquiryLogs_CreatedAt_Sync] DEFAULT (GETUTCDATE()),
                        CONSTRAINT [PK_ZohalInquiryLogs] PRIMARY KEY ([Id])
                    );
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Do not drop — shared with AddZohalInquiryLogs
        }
    }
}
