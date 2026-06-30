using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api_Vapp.Migrations
{
    /// <inheritdoc />
    public partial class AddBookingAppointmentModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BookingAppointments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BookingSystemId = table.Column<int>(type: "int", nullable: false),
                    BookingServiceItemId = table.Column<int>(type: "int", nullable: false),
                    CustomerFullName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CustomerMobile = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ContactId = table.Column<int>(type: "int", nullable: true),
                    StartUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ReminderSentAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancellationReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookingAppointments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BookingAppointments_BookingServiceItems_BookingServiceItemId",
                        column: x => x.BookingServiceItemId,
                        principalTable: "BookingServiceItems",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_BookingAppointments_BookingSystems_BookingSystemId",
                        column: x => x.BookingSystemId,
                        principalTable: "BookingSystems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BookingAppointments_Contacts_ContactId",
                        column: x => x.ContactId,
                        principalTable: "Contacts",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_BookingAppointments_BookingServiceItemId",
                table: "BookingAppointments",
                column: "BookingServiceItemId");

            migrationBuilder.CreateIndex(
                name: "IX_BookingAppointments_BookingServiceItemId_StartUtc",
                table: "BookingAppointments",
                columns: new[] { "BookingServiceItemId", "StartUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_BookingAppointments_BookingSystemId",
                table: "BookingAppointments",
                column: "BookingSystemId");

            migrationBuilder.CreateIndex(
                name: "IX_BookingAppointments_ContactId",
                table: "BookingAppointments",
                column: "ContactId");

            migrationBuilder.CreateIndex(
                name: "IX_BookingAppointments_Status_StartUtc_ReminderSentAt",
                table: "BookingAppointments",
                columns: new[] { "Status", "StartUtc", "ReminderSentAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BookingAppointments");
        }
    }
}
