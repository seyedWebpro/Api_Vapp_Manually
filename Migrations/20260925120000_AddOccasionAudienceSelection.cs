using Api_Vapp.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api_Vapp.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(Api_Context))]
    [Migration("20260925120000_AddOccasionAudienceSelection")]
    public partial class AddOccasionAudienceSelection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ApplyToAllContacts",
                table: "UserOccasionPreferences",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "ContactNotebookIdsJson",
                table: "UserOccasionPreferences",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContactIdsJson",
                table: "UserOccasionPreferences",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExcludedContactIdsJson",
                table: "UserOccasionPreferences",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ApplyToAllContacts",
                table: "UserOccasionPreferences");

            migrationBuilder.DropColumn(
                name: "ContactNotebookIdsJson",
                table: "UserOccasionPreferences");

            migrationBuilder.DropColumn(
                name: "ContactIdsJson",
                table: "UserOccasionPreferences");

            migrationBuilder.DropColumn(
                name: "ExcludedContactIdsJson",
                table: "UserOccasionPreferences");
        }
    }
}
