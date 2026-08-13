using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api_Vapp.Migrations
{
    /// <inheritdoc />
    public partial class AddReferralPersonalCodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsReferrerRewardActive",
                table: "ReferralPrograms",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateTable(
                name: "ReferralContactCodes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReferralProgramId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    ContactId = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReferralContactCodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReferralContactCodes_Contacts_ContactId",
                        column: x => x.ContactId,
                        principalTable: "Contacts",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ReferralContactCodes_ReferralPrograms_ReferralProgramId",
                        column: x => x.ReferralProgramId,
                        principalTable: "ReferralPrograms",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ReferralContactCodes_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReferralContactCodes_ContactId",
                table: "ReferralContactCodes",
                column: "ContactId");

            migrationBuilder.CreateIndex(
                name: "IX_ReferralContactCodes_IsDeleted",
                table: "ReferralContactCodes",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_ReferralContactCodes_ReferralProgramId",
                table: "ReferralContactCodes",
                column: "ReferralProgramId");

            migrationBuilder.CreateIndex(
                name: "IX_ReferralContactCodes_ReferralProgramId_ContactId",
                table: "ReferralContactCodes",
                columns: new[] { "ReferralProgramId", "ContactId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReferralContactCodes_UserId_Code",
                table: "ReferralContactCodes",
                columns: new[] { "UserId", "Code" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReferralContactCodes");

            migrationBuilder.DropColumn(
                name: "IsReferrerRewardActive",
                table: "ReferralPrograms");
        }
    }
}
