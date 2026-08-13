using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api_Vapp.Migrations
{
    [DbContext(typeof(Api_Vapp.Data.Api_Context))]
    [Migration("20260813120000_AddBookingSystemBookingWindowDays")]
    public partial class AddBookingSystemBookingWindowDays : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BookingWindowDays",
                table: "BookingSystems",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BookingWindowDays",
                table: "BookingSystems");
        }
    }
}
