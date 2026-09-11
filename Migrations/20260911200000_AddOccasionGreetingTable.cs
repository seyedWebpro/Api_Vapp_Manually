using Api_Vapp.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api_Vapp.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(Api_Context))]
    [Migration("20260911200000_AddOccasionGreetingTable")]
    public partial class AddOccasionGreetingTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "SpecialOccasions",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Congratulation");

            migrationBuilder.AddColumn<string>(
                name: "CalendarType",
                table: "SpecialOccasions",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Jalali");

            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "SpecialOccasions",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<byte>(
                name: "Month",
                table: "SpecialOccasions",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)1);

            migrationBuilder.AddColumn<byte>(
                name: "Day",
                table: "SpecialOccasions",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)1);

            migrationBuilder.AddColumn<int>(
                name: "SortOrder",
                table: "SpecialOccasions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SpecialOccasionId",
                table: "AutomationExecutions",
                type: "int",
                nullable: true);

            // Backfill Month/Day/Category/CalendarType از OccasionDate و Type موجود
            migrationBuilder.Sql("""
                UPDATE SpecialOccasions
                SET
                    Month = CASE WHEN DATEPART(month, OccasionDate) BETWEEN 1 AND 12 THEN CAST(DATEPART(month, OccasionDate) AS tinyint) ELSE CAST(1 AS tinyint) END,
                    Day = CASE WHEN DATEPART(day, OccasionDate) BETWEEN 1 AND 31 THEN CAST(DATEPART(day, OccasionDate) AS tinyint) ELSE CAST(1 AS tinyint) END,
                    CalendarType = 'Gregorian',
                    Category = CASE WHEN Type = 'Death' THEN 'Condolence' ELSE 'Congratulation' END
                WHERE IsDeleted = 0 OR IsDeleted = 1;
                """);

            migrationBuilder.CreateTable(
                name: "UserOccasionProfiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    BusinessName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CongratulationsEnabled = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CondolencesEnabled = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    ScheduledTimeTehran = table.Column<TimeSpan>(type: "time", nullable: true),
                    AutomatedMessageId = table.Column<int>(type: "int", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserOccasionProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserOccasionProfiles_AutomatedMessages_AutomatedMessageId",
                        column: x => x.AutomatedMessageId,
                        principalTable: "AutomatedMessages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_UserOccasionProfiles_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UserOccasionPreferences",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    SpecialOccasionId = table.Column<int>(type: "int", nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CustomMessage = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TemplateApprovalStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Approved"),
                    TemplateApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TemplateApprovedByUserId = table.Column<int>(type: "int", nullable: true),
                    TemplateRejectionReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    MessageTemplateId = table.Column<int>(type: "int", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserOccasionPreferences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserOccasionPreferences_MessageTemplates_MessageTemplateId",
                        column: x => x.MessageTemplateId,
                        principalTable: "MessageTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_UserOccasionPreferences_SpecialOccasions_SpecialOccasionId",
                        column: x => x.SpecialOccasionId,
                        principalTable: "SpecialOccasions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserOccasionPreferences_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SpecialOccasions_CalendarType_Month_Day_IsActive_IsDeleted",
                table: "SpecialOccasions",
                columns: new[] { "CalendarType", "Month", "Day", "IsActive", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_SpecialOccasions_Category_IsActive_IsDeleted",
                table: "SpecialOccasions",
                columns: new[] { "Category", "IsActive", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_SpecialOccasions_Code",
                table: "SpecialOccasions",
                column: "Code",
                unique: true,
                filter: "[Code] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_AutomationExecutions_AutomatedMessageId_SpecialOccasionId_ContactId_ExecutedAt",
                table: "AutomationExecutions",
                columns: new[] { "AutomatedMessageId", "SpecialOccasionId", "ContactId", "ExecutedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_UserOccasionPreferences_MessageTemplateId",
                table: "UserOccasionPreferences",
                column: "MessageTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_UserOccasionPreferences_SpecialOccasionId",
                table: "UserOccasionPreferences",
                column: "SpecialOccasionId");

            migrationBuilder.CreateIndex(
                name: "IX_UserOccasionPreferences_UserId_IsEnabled_IsDeleted",
                table: "UserOccasionPreferences",
                columns: new[] { "UserId", "IsEnabled", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_UserOccasionPreferences_UserId_SpecialOccasionId",
                table: "UserOccasionPreferences",
                columns: new[] { "UserId", "SpecialOccasionId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_UserOccasionProfiles_AutomatedMessageId",
                table: "UserOccasionProfiles",
                column: "AutomatedMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_UserOccasionProfiles_UserId",
                table: "UserOccasionProfiles",
                column: "UserId",
                unique: true,
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "UserOccasionPreferences");
            migrationBuilder.DropTable(name: "UserOccasionProfiles");

            migrationBuilder.DropIndex(
                name: "IX_AutomationExecutions_AutomatedMessageId_SpecialOccasionId_ContactId_ExecutedAt",
                table: "AutomationExecutions");

            migrationBuilder.DropIndex(
                name: "IX_SpecialOccasions_CalendarType_Month_Day_IsActive_IsDeleted",
                table: "SpecialOccasions");

            migrationBuilder.DropIndex(
                name: "IX_SpecialOccasions_Category_IsActive_IsDeleted",
                table: "SpecialOccasions");

            migrationBuilder.DropIndex(
                name: "IX_SpecialOccasions_Code",
                table: "SpecialOccasions");

            migrationBuilder.DropColumn(name: "SpecialOccasionId", table: "AutomationExecutions");
            migrationBuilder.DropColumn(name: "Category", table: "SpecialOccasions");
            migrationBuilder.DropColumn(name: "CalendarType", table: "SpecialOccasions");
            migrationBuilder.DropColumn(name: "Code", table: "SpecialOccasions");
            migrationBuilder.DropColumn(name: "Month", table: "SpecialOccasions");
            migrationBuilder.DropColumn(name: "Day", table: "SpecialOccasions");
            migrationBuilder.DropColumn(name: "SortOrder", table: "SpecialOccasions");
        }
    }
}
