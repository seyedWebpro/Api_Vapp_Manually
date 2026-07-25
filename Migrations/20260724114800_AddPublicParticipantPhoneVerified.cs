using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api_Vapp.Migrations
{
    /// <inheritdoc />
    public partial class AddPublicParticipantPhoneVerified : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "PhoneVerifiedAt",
                table: "PublicParticipantSessions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.DropIndex(
                name: "IX_PublicParticipantSessions_TokenHash",
                table: "PublicParticipantSessions");

            migrationBuilder.CreateIndex(
                name: "IX_PublicParticipantSessions_TokenHash",
                table: "PublicParticipantSessions",
                column: "TokenHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PublicParticipantSessions_TokenHash",
                table: "PublicParticipantSessions");

            migrationBuilder.DropColumn(
                name: "PhoneVerifiedAt",
                table: "PublicParticipantSessions");

            migrationBuilder.CreateIndex(
                name: "IX_PublicParticipantSessions_TokenHash",
                table: "PublicParticipantSessions",
                column: "TokenHash");
        }
    }
}
