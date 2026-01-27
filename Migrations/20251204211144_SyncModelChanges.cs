using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api_Vapp.Migrations
{
    /// <inheritdoc />
    public partial class SyncModelChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ContactId",
                table: "AutomationExecutions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MessageContent",
                table: "AutomationExecutions",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ContactOccasions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ContactId = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    HasTime = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContactOccasions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContactOccasions_Contacts_ContactId",
                        column: x => x.ContactId,
                        principalTable: "Contacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AutomationExecutions_ContactId",
                table: "AutomationExecutions",
                column: "ContactId");

            migrationBuilder.CreateIndex(
                name: "IX_ContactOccasions_ContactId",
                table: "ContactOccasions",
                column: "ContactId");

            migrationBuilder.AddForeignKey(
                name: "FK_AutomationExecutions_Contacts_ContactId",
                table: "AutomationExecutions",
                column: "ContactId",
                principalTable: "Contacts",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AutomationExecutions_Contacts_ContactId",
                table: "AutomationExecutions");

            migrationBuilder.DropTable(
                name: "ContactOccasions");

            migrationBuilder.DropIndex(
                name: "IX_AutomationExecutions_ContactId",
                table: "AutomationExecutions");

            migrationBuilder.DropColumn(
                name: "ContactId",
                table: "AutomationExecutions");

            migrationBuilder.DropColumn(
                name: "MessageContent",
                table: "AutomationExecutions");
        }
    }
}
