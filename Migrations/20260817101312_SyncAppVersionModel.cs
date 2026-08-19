using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api_Vapp.Migrations
{
    /// <inheritdoc />
    public partial class SyncAppVersionModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ZohalInquiryLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InquiryType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Source = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    MobileMasked = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    NationalCodeMasked = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Matched = table.Column<bool>(type: "bit", nullable: true),
                    HttpStatusCode = table.Column<int>(type: "int", nullable: true),
                    ZohalResultCode = table.Column<int>(type: "int", nullable: true),
                    ProviderErrorCode = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    ProviderMessage = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    OutcomeStatus = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    UserFacingErrorCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    RequestJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ResponseJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DurationMs = table.Column<int>(type: "int", nullable: false),
                    Succeeded = table.Column<bool>(type: "bit", nullable: false),
                    TraceId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    IpAddress = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ZohalInquiryLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ZohalInquiryLogs_CreatedAt",
                table: "ZohalInquiryLogs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ZohalInquiryLogs_InquiryType_CreatedAt",
                table: "ZohalInquiryLogs",
                columns: new[] { "InquiryType", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ZohalInquiryLogs_MobileMasked",
                table: "ZohalInquiryLogs",
                column: "MobileMasked");

            migrationBuilder.CreateIndex(
                name: "IX_ZohalInquiryLogs_OutcomeStatus_CreatedAt",
                table: "ZohalInquiryLogs",
                columns: new[] { "OutcomeStatus", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ZohalInquiryLogs_TraceId",
                table: "ZohalInquiryLogs",
                column: "TraceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ZohalInquiryLogs");
        }
    }
}
