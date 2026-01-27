using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api_Vapp.Migrations
{
    /// <inheritdoc />
    public partial class AddAutomatedMessageStatusAndCampaignLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AutomatedMessageId",
                table: "MessageCampaigns",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "AutomatedMessages",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Draft");

            migrationBuilder.CreateIndex(
                name: "IX_MessageCampaigns_AutomatedMessageId",
                table: "MessageCampaigns",
                column: "AutomatedMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_AutomatedMessages_Status",
                table: "AutomatedMessages",
                column: "Status");

            migrationBuilder.AddForeignKey(
                name: "FK_MessageCampaigns_AutomatedMessages_AutomatedMessageId",
                table: "MessageCampaigns",
                column: "AutomatedMessageId",
                principalTable: "AutomatedMessages",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MessageCampaigns_AutomatedMessages_AutomatedMessageId",
                table: "MessageCampaigns");

            migrationBuilder.DropIndex(
                name: "IX_MessageCampaigns_AutomatedMessageId",
                table: "MessageCampaigns");

            migrationBuilder.DropIndex(
                name: "IX_AutomatedMessages_Status",
                table: "AutomatedMessages");

            migrationBuilder.DropColumn(
                name: "AutomatedMessageId",
                table: "MessageCampaigns");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "AutomatedMessages");
        }
    }
}
