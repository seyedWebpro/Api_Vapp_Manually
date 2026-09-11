using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api_Vapp.Migrations
{
    /// <inheritdoc />
    public partial class AddQuickSendTemplateSelection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsQuickSendDefault",
                table: "MessageTemplates",
                type: "bit",
                nullable: false,
                defaultValue: false);

            // حفظ رفتار قبلی: از بین قالب‌های سیستمی فعال هر کاربر، یک مورد به‌صورت پایدار
            // به‌عنوان انتخاب اولیهٔ ارسال سریع علامت‌گذاری می‌شود.
            migrationBuilder.Sql(@"
                WITH RankedTemplates AS (
                    SELECT [Id], ROW_NUMBER() OVER (PARTITION BY [UserId] ORDER BY [Id]) AS [RowNumber]
                    FROM [MessageTemplates]
                    WHERE [IsDefault] = 1
                      AND [IsActive] = 1
                      AND [IsDeleted] = 0
                      AND [ApprovalStatus] = N'Approved'
                )
                UPDATE mt
                SET [IsQuickSendDefault] = 1
                FROM [MessageTemplates] mt
                INNER JOIN RankedTemplates ranked ON ranked.[Id] = mt.[Id]
                WHERE ranked.[RowNumber] = 1;");

            migrationBuilder.CreateIndex(
                name: "IX_MessageTemplates_UserId_IsQuickSendDefault_IsDeleted",
                table: "MessageTemplates",
                columns: new[] { "UserId", "IsQuickSendDefault", "IsDeleted" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MessageTemplates_UserId_IsQuickSendDefault_IsDeleted",
                table: "MessageTemplates");

            migrationBuilder.DropColumn(
                name: "IsQuickSendDefault",
                table: "MessageTemplates");
        }
    }
}
