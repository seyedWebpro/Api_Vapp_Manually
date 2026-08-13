using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api_Vapp.Migrations
{
    /// <summary>
    /// تأیید یک‌باره محتوای ارسال سریع روی ماژول‌ها (مثل قالب‌ها).
    /// آیتم‌های موجود برای جلوگیری از قطع سرویس grandfather می‌شوند به Approved.
    /// </summary>
    [DbContext(typeof(Api_Vapp.Data.Api_Context))]
    [Migration("20260813154500_AddQuickSendContentApproval")]
    public partial class AddQuickSendContentApproval : Migration
    {
        private static readonly string[] Tables =
        [
            "BusinessCards",
            "BookingSystems",
            "UserForms",
            "LuckyWheels",
            "SocialMediaLinks",
            "QuickActions"
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var table in Tables)
            {
                migrationBuilder.AddColumn<string>(
                    name: "ApprovalStatus",
                    table: table,
                    type: "nvarchar(50)",
                    maxLength: 50,
                    nullable: false,
                    defaultValue: "Pending");

                migrationBuilder.AddColumn<DateTime>(
                    name: "ApprovedAt",
                    table: table,
                    type: "datetime2",
                    nullable: true);

                migrationBuilder.AddColumn<int>(
                    name: "ApprovedByUserId",
                    table: table,
                    type: "int",
                    nullable: true);

                migrationBuilder.AddColumn<string>(
                    name: "RejectionReason",
                    table: table,
                    type: "nvarchar(1000)",
                    maxLength: 1000,
                    nullable: true);

                migrationBuilder.CreateIndex(
                    name: $"IX_{table}_ApprovalStatus",
                    table: table,
                    column: "ApprovalStatus");
            }

            // Grandfathering: محتوای موجود قطع نشود — Approved
            migrationBuilder.Sql("""
                UPDATE BusinessCards SET ApprovalStatus = N'Approved', ApprovedAt = SYSUTCDATETIME()
                WHERE IsDeleted = 0 AND Status = 1;

                UPDATE BookingSystems SET ApprovalStatus = N'Approved', ApprovedAt = SYSUTCDATETIME()
                WHERE IsDeleted = 0 AND Status = 1;

                UPDATE UserForms SET ApprovalStatus = N'Approved', ApprovedAt = SYSUTCDATETIME()
                WHERE IsDeleted = 0 AND Status = 1;

                UPDATE LuckyWheels SET ApprovalStatus = N'Approved', ApprovedAt = SYSUTCDATETIME()
                WHERE IsDeleted = 0 AND Status = 1;

                UPDATE SocialMediaLinks SET ApprovalStatus = N'Approved', ApprovedAt = SYSUTCDATETIME()
                WHERE IsDeleted = 0;

                UPDATE QuickActions SET ApprovalStatus = N'Approved', ApprovedAt = SYSUTCDATETIME()
                WHERE IsDeleted = 0;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var table in Tables)
            {
                migrationBuilder.DropIndex(
                    name: $"IX_{table}_ApprovalStatus",
                    table: table);

                migrationBuilder.DropColumn(name: "RejectionReason", table: table);
                migrationBuilder.DropColumn(name: "ApprovedByUserId", table: table);
                migrationBuilder.DropColumn(name: "ApprovedAt", table: table);
                migrationBuilder.DropColumn(name: "ApprovalStatus", table: table);
            }
        }
    }
}
