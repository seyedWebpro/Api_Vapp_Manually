using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api_Vapp.Migrations
{
    /// <inheritdoc />
    public partial class SubscriptionFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SubscriptionFeatures",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionFeatures", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SubscriptionPlanFeatures",
                columns: table => new
                {
                    SubscriptionPlanId = table.Column<int>(type: "int", nullable: false),
                    SubscriptionFeatureId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionPlanFeatures", x => new { x.SubscriptionPlanId, x.SubscriptionFeatureId });
                    table.ForeignKey(
                        name: "FK_SubscriptionPlanFeatures_SubscriptionFeatures_SubscriptionFeatureId",
                        column: x => x.SubscriptionFeatureId,
                        principalTable: "SubscriptionFeatures",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_SubscriptionPlanFeatures_SubscriptionPlans_SubscriptionPlanId",
                        column: x => x.SubscriptionPlanId,
                        principalTable: "SubscriptionPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionFeatures_Code",
                table: "SubscriptionFeatures",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionFeatures_IsActive",
                table: "SubscriptionFeatures",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionPlanFeatures_SubscriptionFeatureId",
                table: "SubscriptionPlanFeatures",
                column: "SubscriptionFeatureId");

            migrationBuilder.InsertData(
                table: "SubscriptionFeatures",
                columns: new[] { "Id", "Name", "Code", "Description", "SortOrder", "IsActive", "IsDeleted", "CreatedAt" },
                values: new object[,]
                {
                    { 1, "ارسال سریع رایگان", "free_quick_send", "ارسال پیام سریع بدون هزینه اضافه", 1, true, false, new DateTime(2026, 6, 20, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 2, "کارت ویزیت", "business_card", "ساخت و مدیریت کارت ویزیت دیجیتال", 2, true, false, new DateTime(2026, 6, 20, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 3, "اتوماسیون پیام", "message_automation", "پیام‌های خودکار بر اساس رویداد و مناسبت", 3, true, false, new DateTime(2026, 6, 20, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 4, "کمپین پیام انبوه", "bulk_campaign", "ارسال کمپین پیامک به گروه‌های مخاطب", 4, true, false, new DateTime(2026, 6, 20, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 5, "کیف پول کش‌بک", "cashback_wallet", "مدیریت و پرداخت کش‌بک به مشتریان", 5, true, false, new DateTime(2026, 6, 20, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 6, "پشتیبانی اولویت‌دار", "priority_support", "پاسخگویی سریع‌تر تیم پشتیبانی", 6, true, false, new DateTime(2026, 6, 20, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 7, "گزارش‌گیری پیشرفته", "advanced_analytics", "گزارش‌های تحلیلی از پیام‌ها و مشتریان", 7, true, false, new DateTime(2026, 6, 20, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.Sql(@"
                INSERT INTO SubscriptionPlanFeatures (SubscriptionPlanId, SubscriptionFeatureId)
                SELECT p.Id, 1 FROM SubscriptionPlans p WHERE p.FreeQuickSendEnabled = 1 AND p.IsDeleted = 0;

                INSERT INTO SubscriptionPlanFeatures (SubscriptionPlanId, SubscriptionFeatureId)
                SELECT p.Id, 2 FROM SubscriptionPlans p WHERE p.BusinessCardEnabled = 1 AND p.IsDeleted = 0;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SubscriptionPlanFeatures");

            migrationBuilder.DropTable(
                name: "SubscriptionFeatures");
        }
    }
}
