using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api_Vapp.Migrations
{
    /// <inheritdoc />
    public partial class CompleteBookingDashboardModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BookingAppointments_BookingServiceItemId_StartUtc",
                table: "BookingAppointments");

            migrationBuilder.AddColumn<string>(
                name: "Location",
                table: "BookingSystems",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomerNote",
                table: "BookingAppointments",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BookingSlotBlocks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BookingSystemId = table.Column<int>(type: "int", nullable: false),
                    SlotStartUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookingSlotBlocks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BookingSlotBlocks_BookingSystems_BookingSystemId",
                        column: x => x.BookingSystemId,
                        principalTable: "BookingSystems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BookingAppointments_BookingServiceItemId_StartUtc",
                table: "BookingAppointments",
                columns: new[] { "BookingServiceItemId", "StartUtc" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [Status] IN ('Confirmed', 'Pending')");

            migrationBuilder.CreateIndex(
                name: "IX_BookingSlotBlocks_BookingSystemId_SlotStartUtc",
                table: "BookingSlotBlocks",
                columns: new[] { "BookingSystemId", "SlotStartUtc" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BookingSlotBlocks");

            migrationBuilder.DropIndex(
                name: "IX_BookingAppointments_BookingServiceItemId_StartUtc",
                table: "BookingAppointments");

            migrationBuilder.DropColumn(
                name: "Location",
                table: "BookingSystems");

            migrationBuilder.DropColumn(
                name: "CustomerNote",
                table: "BookingAppointments");

            migrationBuilder.CreateIndex(
                name: "IX_BookingAppointments_BookingServiceItemId_StartUtc",
                table: "BookingAppointments",
                columns: new[] { "BookingServiceItemId", "StartUtc" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [Status] = 'Confirmed'");
        }
    }
}
