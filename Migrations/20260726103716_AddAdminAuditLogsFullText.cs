using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api_Vapp.Migrations
{
    /// <inheritdoc />
    public partial class AddAdminAuditLogsFullText : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Full-Text اختیاری — اگر روی SQL Server نصب نباشد، بدون خطا رد می‌شود.
            migrationBuilder.Sql("""
                BEGIN TRY
                    IF SERVERPROPERTY('IsFullTextInstalled') = 1
                    BEGIN
                        IF NOT EXISTS (SELECT 1 FROM sys.fulltext_catalogs WHERE name = N'AdminAuditLogsCatalog')
                            CREATE FULLTEXT CATALOG AdminAuditLogsCatalog AS DEFAULT;

                        IF OBJECT_ID(N'dbo.AdminAuditLogs', N'U') IS NOT NULL
                           AND NOT EXISTS (
                                SELECT 1
                                FROM sys.fulltext_indexes i
                                INNER JOIN sys.objects o ON i.object_id = o.object_id
                                WHERE o.name = N'AdminAuditLogs')
                        BEGIN
                            CREATE FULLTEXT INDEX ON dbo.AdminAuditLogs
                            (
                                OldValue LANGUAGE 0,
                                NewValue LANGUAGE 0,
                                Metadata LANGUAGE 0,
                                ErrorMessage LANGUAGE 0
                            )
                            KEY INDEX PK_AdminAuditLogs
                            ON AdminAuditLogsCatalog
                            WITH CHANGE_TRACKING AUTO;
                        END
                    END
                END TRY
                BEGIN CATCH
                    PRINT 'Full-Text skipped: ' + ERROR_MESSAGE();
                END CATCH
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF SERVERPROPERTY('IsFullTextInstalled') = 1
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM sys.fulltext_indexes i
                        INNER JOIN sys.objects o ON i.object_id = o.object_id
                        WHERE o.name = N'AdminAuditLogs')
                        DROP FULLTEXT INDEX ON dbo.AdminAuditLogs;

                    IF EXISTS (SELECT 1 FROM sys.fulltext_catalogs WHERE name = N'AdminAuditLogsCatalog')
                        DROP FULLTEXT CATALOG AdminAuditLogsCatalog;
                END
                """);
        }
    }
}
