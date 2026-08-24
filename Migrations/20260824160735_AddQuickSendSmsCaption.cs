using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api_Vapp.Migrations
{
    /// <inheritdoc />
    public partial class AddQuickSendSmsCaption : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SmsCaption",
                table: "UserForms",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SmsCaption",
                table: "LuckyWheels",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SmsCaption",
                table: "BusinessCards",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SmsCaption",
                table: "BookingSystems",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SmsCaption",
                table: "UserForms");

            migrationBuilder.DropColumn(
                name: "SmsCaption",
                table: "LuckyWheels");

            migrationBuilder.DropColumn(
                name: "SmsCaption",
                table: "BusinessCards");

            migrationBuilder.DropColumn(
                name: "SmsCaption",
                table: "BookingSystems");
        }
    }
}
