using System;
using Api_Vapp.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api_Vapp.Migrations
{
    [DbContext(typeof(Api_Context))]
    [Migration("20260925194500_AddNumberSeekerPhoneBank")]
    public partial class AddNumberSeekerPhoneBank : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NumberSeekerPhoneBanks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PhoneNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Source = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    City = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsAvailable = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    ServedCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    LastServedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NumberSeekerPhoneBanks", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NumberSeekerPhoneBanks_PhoneNumber",
                table: "NumberSeekerPhoneBanks",
                column: "PhoneNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NumberSeekerPhoneBanks_IsDeleted",
                table: "NumberSeekerPhoneBanks",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_NumberSeekerPhoneBanks_City_Category_Source_IsDeleted_IsAvailable",
                table: "NumberSeekerPhoneBanks",
                columns: new[] { "City", "Category", "Source", "IsDeleted", "IsAvailable" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NumberSeekerPhoneBanks");
        }
    }
}
