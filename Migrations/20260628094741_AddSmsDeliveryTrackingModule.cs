using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api_Vapp.Migrations
{
    /// <inheritdoc />
    public partial class AddSmsDeliveryTrackingModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SmsDeliveryRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    SourceModule = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SourceEntityId = table.Column<int>(type: "int", nullable: true),
                    SourceEntityLabel = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Mobile = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Sid = table.Column<long>(type: "bigint", nullable: false),
                    SendStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Sent"),
                    DeliveryCategory = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ProviderStatusCode = table.Column<int>(type: "int", nullable: true),
                    ProviderStatusMessage = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    IsDeliveryFinal = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    SentAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    LastCheckedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CheckAttempts = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SmsDeliveryRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SmsDeliveryRecords_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_SmsDeliveryRecords_DeliveryCategory",
                table: "SmsDeliveryRecords",
                column: "DeliveryCategory");

            migrationBuilder.CreateIndex(
                name: "IX_SmsDeliveryRecords_IsDeliveryFinal_SendStatus_SentAt",
                table: "SmsDeliveryRecords",
                columns: new[] { "IsDeliveryFinal", "SendStatus", "SentAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SmsDeliveryRecords_SentAt",
                table: "SmsDeliveryRecords",
                column: "SentAt");

            migrationBuilder.CreateIndex(
                name: "IX_SmsDeliveryRecords_Sid",
                table: "SmsDeliveryRecords",
                column: "Sid");

            migrationBuilder.CreateIndex(
                name: "IX_SmsDeliveryRecords_SourceModule_SourceEntityId",
                table: "SmsDeliveryRecords",
                columns: new[] { "SourceModule", "SourceEntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_SmsDeliveryRecords_UserId",
                table: "SmsDeliveryRecords",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SmsDeliveryRecords");
        }
    }
}
