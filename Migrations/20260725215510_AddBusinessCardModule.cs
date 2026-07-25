using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api_Vapp.Migrations
{
    /// <inheritdoc />
    public partial class AddBusinessCardModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BusinessCards",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    LogoUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Slug = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TemplateKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TemplateId = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PublishedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SliderEnabled = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DescriptionEnabled = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    ServicesEnabled = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    MapEnabled = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    ContactEnabled = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    DescriptionTitle = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DescriptionText = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    MapLatitude = table.Column<double>(type: "float", nullable: true),
                    MapLongitude = table.Column<double>(type: "float", nullable: true),
                    MapAddress = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ContactPhone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ContactEmail = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ContactInstagram = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessCards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BusinessCards_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BusinessCardServiceItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BusinessCardId = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ImageUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessCardServiceItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BusinessCardServiceItems_BusinessCards_BusinessCardId",
                        column: x => x.BusinessCardId,
                        principalTable: "BusinessCards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BusinessCardSliderImages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BusinessCardId = table.Column<int>(type: "int", nullable: false),
                    ImageUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessCardSliderImages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BusinessCardSliderImages_BusinessCards_BusinessCardId",
                        column: x => x.BusinessCardId,
                        principalTable: "BusinessCards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BusinessCards_IsDeleted",
                table: "BusinessCards",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessCards_Slug",
                table: "BusinessCards",
                column: "Slug",
                unique: true,
                filter: "[Slug] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessCards_Status",
                table: "BusinessCards",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessCards_UserId",
                table: "BusinessCards",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessCards_UserId_IsDeleted_CreatedAt",
                table: "BusinessCards",
                columns: new[] { "UserId", "IsDeleted", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_BusinessCardServiceItems_BusinessCardId",
                table: "BusinessCardServiceItems",
                column: "BusinessCardId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessCardSliderImages_BusinessCardId",
                table: "BusinessCardSliderImages",
                column: "BusinessCardId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BusinessCardServiceItems");

            migrationBuilder.DropTable(
                name: "BusinessCardSliderImages");

            migrationBuilder.DropTable(
                name: "BusinessCards");
        }
    }
}
