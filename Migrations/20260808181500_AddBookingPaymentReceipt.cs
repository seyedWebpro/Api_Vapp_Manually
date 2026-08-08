using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api_Vapp.Migrations
{
    [DbContext(typeof(Api_Vapp.Data.Api_Context))]
    [Migration("20260808181500_AddBookingPaymentReceipt")]
    public partial class AddBookingPaymentReceipt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PaymentReceiptPath",
                table: "BookingAppointments",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PaymentReceiptPath",
                table: "BookingAppointments");
        }
    }
}
