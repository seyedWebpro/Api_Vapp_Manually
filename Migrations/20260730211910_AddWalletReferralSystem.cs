using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api_Vapp.Migrations
{
    /// <inheritdoc />
    public partial class AddWalletReferralSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReferralCode",
                table: "Users",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "WalletReferralRewards",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PaymentId = table.Column<int>(type: "int", nullable: false),
                    BeneficiaryUserId = table.Column<int>(type: "int", nullable: false),
                    ReferrerUserId = table.Column<int>(type: "int", nullable: false),
                    ReferralCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    RequestedAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PayableAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DiscountPercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    BonusAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    BonusPercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    ReferrerWalletTransactionId = table.Column<int>(type: "int", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WalletReferralRewards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WalletReferralRewards_Payments_PaymentId",
                        column: x => x.PaymentId,
                        principalTable: "Payments",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_WalletReferralRewards_Users_BeneficiaryUserId",
                        column: x => x.BeneficiaryUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_WalletReferralRewards_Users_ReferrerUserId",
                        column: x => x.ReferrerUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_WalletReferralRewards_WalletTransactions_ReferrerWalletTransactionId",
                        column: x => x.ReferrerWalletTransactionId,
                        principalTable: "WalletTransactions",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "WalletReferralSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    DiscountPercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    BonusPercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    DescriptionTemplate = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WalletReferralSettings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_ReferralCode",
                table: "Users",
                column: "ReferralCode",
                unique: true,
                filter: "[ReferralCode] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_WalletReferralRewards_BeneficiaryUserId",
                table: "WalletReferralRewards",
                column: "BeneficiaryUserId");

            migrationBuilder.CreateIndex(
                name: "IX_WalletReferralRewards_CreatedAt",
                table: "WalletReferralRewards",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_WalletReferralRewards_PaymentId",
                table: "WalletReferralRewards",
                column: "PaymentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WalletReferralRewards_ReferrerUserId",
                table: "WalletReferralRewards",
                column: "ReferrerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_WalletReferralRewards_ReferrerWalletTransactionId",
                table: "WalletReferralRewards",
                column: "ReferrerWalletTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_WalletReferralSettings_IsDeleted",
                table: "WalletReferralSettings",
                column: "IsDeleted");

            migrationBuilder.Sql("""
                INSERT INTO WalletReferralSettings (IsEnabled, DiscountPercent, BonusPercent, DescriptionTemplate, IsDeleted, CreatedAt)
                SELECT 1, 10, 10,
                    N'کافیه کاربر معرفی‌شده این کد رو موقع شارژ کیف پول وارد کنه؛ در این صورت {DiscountPercent}٪ تخفیف براشون اعمال می‌شه و {BonusPercent}٪ پاداش هم به شما واریز می‌شه.',
                    0, GETUTCDATE()
                WHERE NOT EXISTS (SELECT 1 FROM WalletReferralSettings WHERE IsDeleted = 0);

                UPDATE Users
                SET ReferralCode = CONCAT('@u', Id),
                    UpdatedAt = GETUTCDATE()
                WHERE ReferralCode IS NULL AND IsDeleted = 0;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WalletReferralRewards");

            migrationBuilder.DropTable(
                name: "WalletReferralSettings");

            migrationBuilder.DropIndex(
                name: "IX_Users_ReferralCode",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ReferralCode",
                table: "Users");
        }
    }
}
