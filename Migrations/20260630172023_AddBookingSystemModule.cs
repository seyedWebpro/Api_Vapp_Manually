using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api_Vapp.Migrations
{
    /// <inheritdoc />
    public partial class AddBookingSystemModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BookingSystemDrafts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    DraftId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Step1Data = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Step2Data = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Step3Data = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Step4Data = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookingSystemDrafts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BookingSystemDrafts_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BookingSystems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ActivityType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Slug = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    SaveToPhonebook = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PublishedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookingSystems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BookingSystems_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BookingServiceItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BookingSystemId = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DurationMinutes = table.Column<int>(type: "int", nullable: false),
                    HasCost = table.Column<bool>(type: "bit", nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    ServiceCost = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    DepositAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    BufferMinutesBetweenAppointments = table.Column<int>(type: "int", nullable: false),
                    MaxDailyReservations = table.Column<int>(type: "int", nullable: true),
                    ReminderOffsetMinutes = table.Column<int>(type: "int", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookingServiceItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BookingServiceItems_BookingSystems_BookingSystemId",
                        column: x => x.BookingSystemId,
                        principalTable: "BookingSystems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BookingSystemNotebooks",
                columns: table => new
                {
                    BookingSystemId = table.Column<int>(type: "int", nullable: false),
                    ContactNotebookId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookingSystemNotebooks", x => new { x.BookingSystemId, x.ContactNotebookId });
                    table.ForeignKey(
                        name: "FK_BookingSystemNotebooks_BookingSystems_BookingSystemId",
                        column: x => x.BookingSystemId,
                        principalTable: "BookingSystems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BookingSystemNotebooks_ContactNotebooks_ContactNotebookId",
                        column: x => x.ContactNotebookId,
                        principalTable: "ContactNotebooks",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "BookingScheduleExceptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BookingServiceItemId = table.Column<int>(type: "int", nullable: false),
                    ExceptionDateUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Label = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookingScheduleExceptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BookingScheduleExceptions_BookingServiceItems_BookingServiceItemId",
                        column: x => x.BookingServiceItemId,
                        principalTable: "BookingServiceItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BookingServiceDaySchedules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BookingServiceItemId = table.Column<int>(type: "int", nullable: false),
                    DayOfWeek = table.Column<int>(type: "int", nullable: false),
                    IsOpen = table.Column<bool>(type: "bit", nullable: false),
                    StartTimeUtc = table.Column<TimeSpan>(type: "time", nullable: true),
                    EndTimeUtc = table.Column<TimeSpan>(type: "time", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookingServiceDaySchedules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BookingServiceDaySchedules_BookingServiceItems_BookingServiceItemId",
                        column: x => x.BookingServiceItemId,
                        principalTable: "BookingServiceItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BookingScheduleExceptions_BookingServiceItemId",
                table: "BookingScheduleExceptions",
                column: "BookingServiceItemId");

            migrationBuilder.CreateIndex(
                name: "IX_BookingScheduleExceptions_BookingServiceItemId_ExceptionDateUtc",
                table: "BookingScheduleExceptions",
                columns: new[] { "BookingServiceItemId", "ExceptionDateUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_BookingServiceDaySchedules_BookingServiceItemId",
                table: "BookingServiceDaySchedules",
                column: "BookingServiceItemId");

            migrationBuilder.CreateIndex(
                name: "IX_BookingServiceDaySchedules_BookingServiceItemId_DayOfWeek",
                table: "BookingServiceDaySchedules",
                columns: new[] { "BookingServiceItemId", "DayOfWeek" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BookingServiceItems_BookingSystemId",
                table: "BookingServiceItems",
                column: "BookingSystemId");

            migrationBuilder.CreateIndex(
                name: "IX_BookingServiceItems_BookingSystemId_IsDeleted",
                table: "BookingServiceItems",
                columns: new[] { "BookingSystemId", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_BookingSystemDrafts_DraftId_UserId",
                table: "BookingSystemDrafts",
                columns: new[] { "DraftId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_BookingSystemDrafts_ExpiresAt",
                table: "BookingSystemDrafts",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_BookingSystemDrafts_UserId",
                table: "BookingSystemDrafts",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_BookingSystemNotebooks_ContactNotebookId",
                table: "BookingSystemNotebooks",
                column: "ContactNotebookId");

            migrationBuilder.CreateIndex(
                name: "IX_BookingSystems_IsDeleted",
                table: "BookingSystems",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_BookingSystems_Slug",
                table: "BookingSystems",
                column: "Slug",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_BookingSystems_UserId",
                table: "BookingSystems",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_BookingSystems_UserId_IsDeleted_CreatedAt",
                table: "BookingSystems",
                columns: new[] { "UserId", "IsDeleted", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BookingScheduleExceptions");

            migrationBuilder.DropTable(
                name: "BookingServiceDaySchedules");

            migrationBuilder.DropTable(
                name: "BookingSystemDrafts");

            migrationBuilder.DropTable(
                name: "BookingSystemNotebooks");

            migrationBuilder.DropTable(
                name: "BookingServiceItems");

            migrationBuilder.DropTable(
                name: "BookingSystems");
        }
    }
}
