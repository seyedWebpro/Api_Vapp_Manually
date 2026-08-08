using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api_Vapp.Migrations
{
    [DbContext(typeof(Api_Vapp.Data.Api_Context))]
    [Migration("20260808174500_EnhanceBookingReminders")]
    public partial class EnhanceBookingReminders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReminderOffsetsJson",
                table: "BookingServiceItems",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "[60]");

            migrationBuilder.AddColumn<bool>(
                name: "RemindersEnabled",
                table: "BookingAppointments",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "ReminderSentOffsetsCsv",
                table: "BookingAppointments",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            // مهاجرت داده: ReminderOffsetMinutes موجود → JSON تک‌عضوی
            migrationBuilder.Sql(@"
UPDATE BookingServiceItems
SET ReminderOffsetsJson = N'[' + CAST(
    CASE
        WHEN ReminderOffsetMinutes IS NULL OR ReminderOffsetMinutes < 1 THEN 60
        WHEN ReminderOffsetMinutes > 43200 THEN 43200
        ELSE ReminderOffsetMinutes
    END AS nvarchar(20)) + N']';
");

            // نوبت‌هایی که قبلاً یادآوری گرفته‌اند: offset سرویس را در SentCsv ثبت کن
            migrationBuilder.Sql(@"
UPDATE a
SET a.ReminderSentOffsetsCsv = CAST(
    CASE
        WHEN s.ReminderOffsetMinutes IS NULL OR s.ReminderOffsetMinutes < 1 THEN 60
        WHEN s.ReminderOffsetMinutes > 43200 THEN 43200
        ELSE s.ReminderOffsetMinutes
    END AS nvarchar(20))
FROM BookingAppointments a
INNER JOIN BookingServiceItems s ON s.Id = a.BookingServiceItemId
WHERE a.ReminderSentAt IS NOT NULL
  AND (a.ReminderSentOffsetsCsv IS NULL OR LTRIM(RTRIM(a.ReminderSentOffsetsCsv)) = N'');
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReminderOffsetsJson",
                table: "BookingServiceItems");

            migrationBuilder.DropColumn(
                name: "RemindersEnabled",
                table: "BookingAppointments");

            migrationBuilder.DropColumn(
                name: "ReminderSentOffsetsCsv",
                table: "BookingAppointments");
        }
    }
}
