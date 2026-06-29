using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api_Vapp.Migrations
{
    /// <inheritdoc />
    public partial class AddSubscriptionDiscountAndPaymentFulfillment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SourcePaymentId",
                table: "UserSubscriptions",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SubscriptionDiscountCodes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DiscountType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Value = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    MaxDiscountAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    MinOrderAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    SubscriptionPlanId = table.Column<int>(type: "int", nullable: true),
                    MaxTotalUses = table.Column<int>(type: "int", nullable: true),
                    UsedCount = table.Column<int>(type: "int", nullable: false),
                    MaxUsesPerUser = table.Column<int>(type: "int", nullable: true),
                    ValidFrom = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ValidUntil = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionDiscountCodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubscriptionDiscountCodes_SubscriptionPlans_SubscriptionPlanId",
                        column: x => x.SubscriptionPlanId,
                        principalTable: "SubscriptionPlans",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "SubscriptionDiscountUsages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SubscriptionDiscountCodeId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    PaymentId = table.Column<int>(type: "int", nullable: true),
                    UserSubscriptionId = table.Column<int>(type: "int", nullable: true),
                    DiscountAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    UsedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionDiscountUsages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubscriptionDiscountUsages_Payments_PaymentId",
                        column: x => x.PaymentId,
                        principalTable: "Payments",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_SubscriptionDiscountUsages_SubscriptionDiscountCodes_SubscriptionDiscountCodeId",
                        column: x => x.SubscriptionDiscountCodeId,
                        principalTable: "SubscriptionDiscountCodes",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_SubscriptionDiscountUsages_UserSubscriptions_UserSubscriptionId",
                        column: x => x.UserSubscriptionId,
                        principalTable: "UserSubscriptions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_SubscriptionDiscountUsages_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserSubscriptions_SourcePaymentId",
                table: "UserSubscriptions",
                column: "SourcePaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionDiscountCodes_Code",
                table: "SubscriptionDiscountCodes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionDiscountCodes_IsActive",
                table: "SubscriptionDiscountCodes",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionDiscountCodes_SubscriptionPlanId",
                table: "SubscriptionDiscountCodes",
                column: "SubscriptionPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionDiscountUsages_PaymentId",
                table: "SubscriptionDiscountUsages",
                column: "PaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionDiscountUsages_SubscriptionDiscountCodeId_UserId",
                table: "SubscriptionDiscountUsages",
                columns: new[] { "SubscriptionDiscountCodeId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionDiscountUsages_UserId",
                table: "SubscriptionDiscountUsages",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionDiscountUsages_UserSubscriptionId",
                table: "SubscriptionDiscountUsages",
                column: "UserSubscriptionId");

            migrationBuilder.AddForeignKey(
                name: "FK_UserSubscriptions_Payments_SourcePaymentId",
                table: "UserSubscriptions",
                column: "SourcePaymentId",
                principalTable: "Payments",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserSubscriptions_Payments_SourcePaymentId",
                table: "UserSubscriptions");

            migrationBuilder.DropTable(
                name: "SubscriptionDiscountUsages");

            migrationBuilder.DropTable(
                name: "SubscriptionDiscountCodes");

            migrationBuilder.DropIndex(
                name: "IX_UserSubscriptions_SourcePaymentId",
                table: "UserSubscriptions");

            migrationBuilder.DropColumn(
                name: "SourcePaymentId",
                table: "UserSubscriptions");
        }
    }
}
