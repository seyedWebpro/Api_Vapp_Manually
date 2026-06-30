using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api_Vapp.Migrations
{
    /// <inheritdoc />
    public partial class AddBookingAppointmentUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BookingAppointments_BookingServiceItemId_StartUtc",
                table: "BookingAppointments");

            migrationBuilder.CreateIndex(
                name: "IX_BookingAppointments_BookingServiceItemId_StartUtc",
                table: "BookingAppointments",
                columns: new[] { "BookingServiceItemId", "StartUtc" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [Status] = 'Confirmed'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BookingAppointments_BookingServiceItemId_StartUtc",
                table: "BookingAppointments");

            migrationBuilder.CreateIndex(
                name: "IX_BookingAppointments_BookingServiceItemId_StartUtc",
                table: "BookingAppointments",
                columns: new[] { "BookingServiceItemId", "StartUtc" });
        }
    }
}
