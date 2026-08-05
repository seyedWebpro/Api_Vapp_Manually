using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api_Vapp.Migrations
{
    /// <inheritdoc />
    public partial class RemoveBookingServiceCost : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // اگر فقط ServiceCost پر بوده، به Price منتقل شود تا داده از بین نرود
            migrationBuilder.Sql("""
                UPDATE BookingServiceItems
                SET Price = ServiceCost
                WHERE Price IS NULL AND ServiceCost IS NOT NULL;
                """);

            migrationBuilder.DropColumn(
                name: "ServiceCost",
                table: "BookingServiceItems");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ServiceCost",
                table: "BookingServiceItems",
                type: "decimal(18,2)",
                nullable: true);
        }
    }
}
