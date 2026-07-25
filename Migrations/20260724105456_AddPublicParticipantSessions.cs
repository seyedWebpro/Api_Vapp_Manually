using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api_Vapp.Migrations
{
    /// <inheritdoc />
    public partial class AddPublicParticipantSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PublicParticipantSessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ResourceType = table.Column<byte>(type: "tinyint", nullable: false),
                    ResourceId = table.Column<int>(type: "int", nullable: false),
                    ParticipantFullName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ParticipantMobile = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TokenHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ConsumedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PublicParticipantSessions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserFormSubmissions_UserFormId_ParticipantMobile",
                table: "UserFormSubmissions",
                columns: new[] { "UserFormId", "ParticipantMobile" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PublicParticipantSessions_ExpiresAt",
                table: "PublicParticipantSessions",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_PublicParticipantSessions_ResourceType_ResourceId_ParticipantMobile",
                table: "PublicParticipantSessions",
                columns: new[] { "ResourceType", "ResourceId", "ParticipantMobile" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_PublicParticipantSessions_TokenHash",
                table: "PublicParticipantSessions",
                column: "TokenHash");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PublicParticipantSessions");

            migrationBuilder.DropIndex(
                name: "IX_UserFormSubmissions_UserFormId_ParticipantMobile",
                table: "UserFormSubmissions");
        }
    }
}
