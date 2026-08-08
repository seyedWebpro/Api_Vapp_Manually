using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api_Vapp.Migrations
{
    [DbContext(typeof(Api_Vapp.Data.Api_Context))]
    [Migration("20260808143000_AddBusinessCardSocialAndBanking")]
    public partial class AddBusinessCardSocialAndBanking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "BankingEnabled",
                table: "BusinessCards",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "BankAccountNumber",
                table: "BusinessCards",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BankCardNumber",
                table: "BusinessCards",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BankShebaNumber",
                table: "BusinessCards",
                type: "nvarchar(26)",
                maxLength: 26,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BusinessCardSocialLinks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BusinessCardId = table.Column<int>(type: "int", nullable: false),
                    NetworkType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Label = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Value = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessCardSocialLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BusinessCardSocialLinks_BusinessCards_BusinessCardId",
                        column: x => x.BusinessCardId,
                        principalTable: "BusinessCards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BusinessCardSocialLinks_BusinessCardId",
                table: "BusinessCardSocialLinks",
                column: "BusinessCardId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessCardSocialLinks_BusinessCardId_DisplayOrder",
                table: "BusinessCardSocialLinks",
                columns: new[] { "BusinessCardId", "DisplayOrder" });

            // مهاجرت داده‌های قدیمی ContactInstagram به جدول لینک‌ها
            migrationBuilder.Sql(@"
INSERT INTO BusinessCardSocialLinks (BusinessCardId, NetworkType, Label, Value, DisplayOrder)
SELECT Id, N'instagram', NULL, ContactInstagram, 0
FROM BusinessCards
WHERE ContactInstagram IS NOT NULL
  AND LTRIM(RTRIM(ContactInstagram)) <> N''
  AND NOT EXISTS (
      SELECT 1 FROM BusinessCardSocialLinks s
      WHERE s.BusinessCardId = BusinessCards.Id
  );
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BusinessCardSocialLinks");

            migrationBuilder.DropColumn(
                name: "BankShebaNumber",
                table: "BusinessCards");

            migrationBuilder.DropColumn(
                name: "BankCardNumber",
                table: "BusinessCards");

            migrationBuilder.DropColumn(
                name: "BankAccountNumber",
                table: "BusinessCards");

            migrationBuilder.DropColumn(
                name: "BankingEnabled",
                table: "BusinessCards");
        }
    }
}
