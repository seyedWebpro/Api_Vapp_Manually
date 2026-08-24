using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api_Vapp.Migrations
{
    /// <inheritdoc />
    public partial class AddReferralInviteSmsApproval : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "MessageId",
                table: "SmsApprovalRequests",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<int>(
                name: "ReferralProgramId",
                table: "SmsApprovalRequests",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InviteSmsApprovalStatus",
                table: "ReferralPrograms",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Approved");

            migrationBuilder.AddColumn<string>(
                name: "InviteSmsClosingText",
                table: "ReferralPrograms",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InviteSmsRejectionReason",
                table: "ReferralPrograms",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SmsApprovalRequests_ReferralProgramId",
                table: "SmsApprovalRequests",
                column: "ReferralProgramId");

            migrationBuilder.AddForeignKey(
                name: "FK_SmsApprovalRequests_ReferralPrograms_ReferralProgramId",
                table: "SmsApprovalRequests",
                column: "ReferralProgramId",
                principalTable: "ReferralPrograms",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SmsApprovalRequests_ReferralPrograms_ReferralProgramId",
                table: "SmsApprovalRequests");

            migrationBuilder.DropIndex(
                name: "IX_SmsApprovalRequests_ReferralProgramId",
                table: "SmsApprovalRequests");

            migrationBuilder.DropColumn(
                name: "ReferralProgramId",
                table: "SmsApprovalRequests");

            migrationBuilder.DropColumn(
                name: "InviteSmsApprovalStatus",
                table: "ReferralPrograms");

            migrationBuilder.DropColumn(
                name: "InviteSmsClosingText",
                table: "ReferralPrograms");

            migrationBuilder.DropColumn(
                name: "InviteSmsRejectionReason",
                table: "ReferralPrograms");

            migrationBuilder.AlterColumn<int>(
                name: "MessageId",
                table: "SmsApprovalRequests",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);
        }
    }
}
