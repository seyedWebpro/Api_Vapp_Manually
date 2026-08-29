using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api_Vapp.Migrations
{
    [DbContext(typeof(Api_Vapp.Data.Api_Context))]
    [Migration("20260830120000_AddSmsDeliveryWalletRefund")]
    public partial class AddSmsDeliveryWalletRefund : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ChargedAmount",
                table: "SmsDeliveryRecords",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "PartsCount",
                table: "SmsDeliveryRecords",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "WalletRefundedAt",
                table: "SmsDeliveryRecords",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WalletRefundTransactionId",
                table: "SmsDeliveryRecords",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SmsDeliveryRecords_WalletRefundPending",
                table: "SmsDeliveryRecords",
                columns: new[] { "IsDeliveryFinal", "WalletRefundedAt", "ChargedAmount" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SmsDeliveryRecords_WalletRefundPending",
                table: "SmsDeliveryRecords");

            migrationBuilder.DropColumn(
                name: "ChargedAmount",
                table: "SmsDeliveryRecords");

            migrationBuilder.DropColumn(
                name: "PartsCount",
                table: "SmsDeliveryRecords");

            migrationBuilder.DropColumn(
                name: "WalletRefundedAt",
                table: "SmsDeliveryRecords");

            migrationBuilder.DropColumn(
                name: "WalletRefundTransactionId",
                table: "SmsDeliveryRecords");
        }
    }
}
