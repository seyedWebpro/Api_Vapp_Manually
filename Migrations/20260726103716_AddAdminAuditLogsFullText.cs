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
            // Full-Text اختیاری است. روی SQL Server لوکال/داکر معمولاً نصب نیست
            // و با XACT_ABORT حتی داخل TRY/CATCH هم به کلاینت خطا می‌دهد.
            // بنابراین این migration عمداً no-op است تا استارت API نشکند.
            migrationBuilder.Sql("""
                PRINT 'AddAdminAuditLogsFullText skipped (optional Full-Text Search).';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // no-op
        }
    }
}
