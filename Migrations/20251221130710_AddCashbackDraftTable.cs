using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api_Vapp.Migrations
{
    /// <inheritdoc />
    public partial class AddCashbackDraftTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CashbackDrafts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    DraftId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Step1Data = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Step2Data = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashbackDrafts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CashbackDrafts_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_CashbackDrafts_DraftId",
                table: "CashbackDrafts",
                column: "DraftId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CashbackDrafts_ExpiresAt",
                table: "CashbackDrafts",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_CashbackDrafts_IsDeleted",
                table: "CashbackDrafts",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_CashbackDrafts_UserId",
                table: "CashbackDrafts",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CashbackDrafts");
        }
    }
}
