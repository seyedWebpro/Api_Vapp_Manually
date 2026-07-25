using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api_Vapp.Migrations
{
    /// <inheritdoc />
    public partial class AddSmsDeliveryReportMessageTextAndIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MessageText",
                table: "SmsDeliveryRecords",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SmsDeliveryRecords_UserId_SentAt",
                table: "SmsDeliveryRecords",
                columns: new[] { "UserId", "SentAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SmsDeliveryRecords_UserId_Sid",
                table: "SmsDeliveryRecords",
                columns: new[] { "UserId", "Sid" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SmsDeliveryRecords_UserId_SentAt",
                table: "SmsDeliveryRecords");

            migrationBuilder.DropIndex(
                name: "IX_SmsDeliveryRecords_UserId_Sid",
                table: "SmsDeliveryRecords");

            migrationBuilder.DropColumn(
                name: "MessageText",
                table: "SmsDeliveryRecords");
        }
    }
}
