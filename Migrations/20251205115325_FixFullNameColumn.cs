using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api_Vapp.Migrations
{
    /// <inheritdoc />
    public partial class FixFullNameColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Add FullName columns
            migrationBuilder.AddColumn<string>(
                name: "FullName",
                table: "Users",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FullName",
                table: "MessageRecipients",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FullName",
                table: "Contacts",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            // 2. Migrate data safely (Concatenate FirstName and LastName)
            // Using CONCAT which handles NULLs (treats them as empty string)
            // LTRIM/RTRIM removes extra spaces if one part is missing
            migrationBuilder.Sql("UPDATE Users SET FullName = LTRIM(RTRIM(CONCAT(FirstName, ' ', LastName)))");
            migrationBuilder.Sql("UPDATE MessageRecipients SET FullName = LTRIM(RTRIM(CONCAT(FirstName, ' ', LastName)))");
            migrationBuilder.Sql("UPDATE Contacts SET FullName = LTRIM(RTRIM(CONCAT(FirstName, ' ', LastName)))");

            // 3. Drop old columns
            migrationBuilder.DropColumn(
                name: "FirstName",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "LastName",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "FirstName",
                table: "MessageRecipients");

            migrationBuilder.DropColumn(
                name: "LastName",
                table: "MessageRecipients");

            migrationBuilder.DropColumn(
                name: "FirstName",
                table: "Contacts");

            migrationBuilder.DropColumn(
                name: "LastName",
                table: "Contacts");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Revert Contacts
            migrationBuilder.AddColumn<string>(
                name: "LastName",
                table: "Contacts",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FirstName",
                table: "Contacts",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            // Revert MessageRecipients
            migrationBuilder.AddColumn<string>(
                name: "LastName",
                table: "MessageRecipients",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FirstName",
                table: "MessageRecipients",
                type: "nvarchar(max)",
                nullable: true);

            // Revert Users
            migrationBuilder.AddColumn<string>(
                name: "LastName",
                table: "Users",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FirstName",
                table: "Users",
                type: "nvarchar(max)",
                nullable: true);

            // Attempt to restore data (rough approximation)
            migrationBuilder.Sql("UPDATE Users SET LastName = FullName"); 
            migrationBuilder.Sql("UPDATE MessageRecipients SET LastName = FullName");
            migrationBuilder.Sql("UPDATE Contacts SET LastName = FullName");

            // Drop FullName columns
            migrationBuilder.DropColumn(
                name: "FullName",
                table: "Contacts");

            migrationBuilder.DropColumn(
                name: "FullName",
                table: "MessageRecipients");

            migrationBuilder.DropColumn(
                name: "FullName",
                table: "Users");
        }
    }
}
