using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api_Vapp.Migrations
{
    /// <inheritdoc />
    public partial class AddReferralProgramModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReferralProgramDrafts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    DraftId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Step1Data = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Step2Data = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Step3Data = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReferralProgramDrafts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReferralProgramDrafts_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ReferralPrograms",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    RewardType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "Percentage"),
                    ReferrerRewardValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    IsCustomerRewardActive = table.Column<bool>(type: "bit", nullable: false),
                    CustomerRewardValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    PublicCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TargetAudience = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "All"),
                    TargetNotebookIds = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    TargetContactIds = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    TargetTagIds = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    SendToSpecificTags = table.Column<bool>(type: "bit", nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NotifiedContactsCount = table.Column<int>(type: "int", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReferralPrograms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReferralPrograms_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReferralProgramDrafts_DraftId",
                table: "ReferralProgramDrafts",
                column: "DraftId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReferralProgramDrafts_ExpiresAt",
                table: "ReferralProgramDrafts",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_ReferralProgramDrafts_IsDeleted",
                table: "ReferralProgramDrafts",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_ReferralProgramDrafts_UserId",
                table: "ReferralProgramDrafts",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ReferralPrograms_EndDate",
                table: "ReferralPrograms",
                column: "EndDate");

            migrationBuilder.CreateIndex(
                name: "IX_ReferralPrograms_IsActive",
                table: "ReferralPrograms",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_ReferralPrograms_IsDeleted",
                table: "ReferralPrograms",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_ReferralPrograms_StartDate",
                table: "ReferralPrograms",
                column: "StartDate");

            migrationBuilder.CreateIndex(
                name: "IX_ReferralPrograms_UserId",
                table: "ReferralPrograms",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ReferralPrograms_UserId_PublicCode",
                table: "ReferralPrograms",
                columns: new[] { "UserId", "PublicCode" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReferralProgramDrafts");

            migrationBuilder.DropTable(
                name: "ReferralPrograms");
        }
    }
}
