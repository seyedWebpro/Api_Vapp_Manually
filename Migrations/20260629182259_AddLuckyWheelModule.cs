using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api_Vapp.Migrations
{
    /// <inheritdoc />
    public partial class AddLuckyWheelModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LuckyWheels",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Slug = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    SaveToPhonebook = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PublishedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LuckyWheels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LuckyWheels_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LuckyWheelItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LuckyWheelId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Probability = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LuckyWheelItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LuckyWheelItems_LuckyWheels_LuckyWheelId",
                        column: x => x.LuckyWheelId,
                        principalTable: "LuckyWheels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LuckyWheelNotebooks",
                columns: table => new
                {
                    LuckyWheelId = table.Column<int>(type: "int", nullable: false),
                    ContactNotebookId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LuckyWheelNotebooks", x => new { x.LuckyWheelId, x.ContactNotebookId });
                    table.ForeignKey(
                        name: "FK_LuckyWheelNotebooks_ContactNotebooks_ContactNotebookId",
                        column: x => x.ContactNotebookId,
                        principalTable: "ContactNotebooks",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_LuckyWheelNotebooks_LuckyWheels_LuckyWheelId",
                        column: x => x.LuckyWheelId,
                        principalTable: "LuckyWheels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LuckyWheelItems_LuckyWheelId",
                table: "LuckyWheelItems",
                column: "LuckyWheelId");

            migrationBuilder.CreateIndex(
                name: "IX_LuckyWheelNotebooks_ContactNotebookId",
                table: "LuckyWheelNotebooks",
                column: "ContactNotebookId");

            migrationBuilder.CreateIndex(
                name: "IX_LuckyWheels_IsDeleted",
                table: "LuckyWheels",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_LuckyWheels_Slug",
                table: "LuckyWheels",
                column: "Slug",
                unique: true,
                filter: "[Slug] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_LuckyWheels_Status",
                table: "LuckyWheels",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_LuckyWheels_UserId",
                table: "LuckyWheels",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_LuckyWheels_UserId_IsDeleted_CreatedAt",
                table: "LuckyWheels",
                columns: new[] { "UserId", "IsDeleted", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LuckyWheelItems");

            migrationBuilder.DropTable(
                name: "LuckyWheelNotebooks");

            migrationBuilder.DropTable(
                name: "LuckyWheels");
        }
    }
}
