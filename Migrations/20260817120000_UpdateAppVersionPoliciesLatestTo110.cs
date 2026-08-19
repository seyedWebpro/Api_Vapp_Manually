using Api_Vapp.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api_Vapp.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(Api_Context))]
    [Migration("20260817120000_UpdateAppVersionPoliciesLatestTo110")]
    public partial class UpdateAppVersionPoliciesLatestTo110 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE AppVersionPolicies
                SET LatestVersion = N'1.1.0',
                    MinSupportedVersion = N'1.0.0',
                    IsActive = 1,
                    UpdatedAt = SYSUTCDATETIME()
                WHERE IsDeleted = 0;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE AppVersionPolicies
                SET LatestVersion = N'1.0.0',
                    MinSupportedVersion = N'1.0.0',
                    UpdatedAt = SYSUTCDATETIME()
                WHERE IsDeleted = 0;
                """);
        }
    }
}
