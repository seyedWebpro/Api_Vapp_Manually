using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api_Vapp.Migrations
{
    /// <inheritdoc />
    public partial class AddReferralUsageModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReferralUsages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReferralProgramId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    PublicCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PurchaseAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    CustomerDiscountAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ReferrerRewardAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CustomerContactId = table.Column<int>(type: "int", nullable: true),
                    ReferrerContactId = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "Completed"),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReferralUsages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReferralUsages_Contacts_CustomerContactId",
                        column: x => x.CustomerContactId,
                        principalTable: "Contacts",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ReferralUsages_Contacts_ReferrerContactId",
                        column: x => x.ReferrerContactId,
                        principalTable: "Contacts",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ReferralUsages_ReferralPrograms_ReferralProgramId",
                        column: x => x.ReferralProgramId,
                        principalTable: "ReferralPrograms",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ReferralUsages_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReferralUsages_CreatedAt",
                table: "ReferralUsages",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ReferralUsages_CustomerContactId",
                table: "ReferralUsages",
                column: "CustomerContactId");

            migrationBuilder.CreateIndex(
                name: "IX_ReferralUsages_PublicCode",
                table: "ReferralUsages",
                column: "PublicCode");

            migrationBuilder.CreateIndex(
                name: "IX_ReferralUsages_ReferralProgramId",
                table: "ReferralUsages",
                column: "ReferralProgramId");

            migrationBuilder.CreateIndex(
                name: "IX_ReferralUsages_ReferrerContactId",
                table: "ReferralUsages",
                column: "ReferrerContactId");

            migrationBuilder.CreateIndex(
                name: "IX_ReferralUsages_Status",
                table: "ReferralUsages",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ReferralUsages_UserId",
                table: "ReferralUsages",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReferralUsages");
        }
    }
}
