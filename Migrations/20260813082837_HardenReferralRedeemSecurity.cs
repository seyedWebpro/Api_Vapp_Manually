using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api_Vapp.Migrations
{
    /// <inheritdoc />
    public partial class HardenReferralRedeemSecurity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                table: "ReferralUsages",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReferralUsages_UserId_IdempotencyKey",
                table: "ReferralUsages",
                columns: new[] { "UserId", "IdempotencyKey" },
                unique: true,
                filter: "[IdempotencyKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ReferralUsages_UserId_PublicCode_CustomerContactId_CreatedAt",
                table: "ReferralUsages",
                columns: new[] { "UserId", "PublicCode", "CustomerContactId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ReferralUsages_UserId_IdempotencyKey",
                table: "ReferralUsages");

            migrationBuilder.DropIndex(
                name: "IX_ReferralUsages_UserId_PublicCode_CustomerContactId_CreatedAt",
                table: "ReferralUsages");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                table: "ReferralUsages");
        }
    }
}
