using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api_Vapp.Migrations
{
    [DbContext(typeof(Api_Vapp.Data.Api_Context))]
    [Migration("20260805183000_EnableSmsBillingByDefault")]
    public partial class EnableSmsBillingByDefault : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE SmsPricingSettings
                SET IsBillingEnabled = 1,
                    UpdatedAt = SYSUTCDATETIME()
                WHERE IsDeleted = 0;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // عمداً IsBillingEnabled را برنمی‌گردانیم تا تنظیمات ادمین حفظ شود
        }
    }
}
