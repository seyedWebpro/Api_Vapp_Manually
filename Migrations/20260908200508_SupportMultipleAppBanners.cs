using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api_Vapp.Migrations
{
    /// <inheritdoc />
    public partial class SupportMultipleAppBanners : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppBanners_Key",
                table: "AppBanners");

            migrationBuilder.AddColumn<bool>(
                name: "IsSystemManaged",
                table: "AppBanners",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql(
                "UPDATE [AppBanners] SET [IsSystemManaged] = 1 WHERE [Key] IN ('home', 'tool', 'tools_wheel')");

            migrationBuilder.CreateIndex(
                name: "IX_AppBanners_Key",
                table: "AppBanners",
                column: "Key");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM [AppBanners] WHERE [IsSystemManaged] = 0");

            migrationBuilder.DropIndex(
                name: "IX_AppBanners_Key",
                table: "AppBanners");

            migrationBuilder.DropColumn(
                name: "IsSystemManaged",
                table: "AppBanners");

            migrationBuilder.CreateIndex(
                name: "IX_AppBanners_Key",
                table: "AppBanners",
                column: "Key",
                unique: true);
        }
    }
}
