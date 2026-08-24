using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api_Vapp.Migrations
{
    /// <inheritdoc />
    public partial class AddSocialMediaLinkSmsCaption : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SmsCaption",
                table: "SocialMediaLinks",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SmsCaption",
                table: "SocialMediaLinks");
        }
    }
}
