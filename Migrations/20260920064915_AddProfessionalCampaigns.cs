using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api_Vapp.Migrations
{
    /// <inheritdoc />
    public partial class AddProfessionalCampaigns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ProfessionalCampaignStepId",
                table: "SmsApprovalRequests",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ProfessionalCampaigns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TargetType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    TargetIdsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "PendingApproval"),
                    StartAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RecipientsCount = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProfessionalCampaigns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProfessionalCampaigns_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ProfessionalCampaignRecipients",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProfessionalCampaignId = table.Column<int>(type: "int", nullable: false),
                    ContactId = table.Column<int>(type: "int", nullable: true),
                    MobileNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProfessionalCampaignRecipients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProfessionalCampaignRecipients_Contacts_ContactId",
                        column: x => x.ContactId,
                        principalTable: "Contacts",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ProfessionalCampaignRecipients_ProfessionalCampaigns_ProfessionalCampaignId",
                        column: x => x.ProfessionalCampaignId,
                        principalTable: "ProfessionalCampaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProfessionalCampaignSteps",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProfessionalCampaignId = table.Column<int>(type: "int", nullable: false),
                    StepOrder = table.Column<int>(type: "int", nullable: false),
                    Content = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    DelayAfterPreviousMinutes = table.Column<int>(type: "int", nullable: false),
                    ScheduledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "PendingApproval"),
                    ApprovalStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "Pending"),
                    ReviewedByUserId = table.Column<int>(type: "int", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    SentCount = table.Column<int>(type: "int", nullable: false),
                    FailedCount = table.Column<int>(type: "int", nullable: false),
                    LastError = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    SentAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProfessionalCampaignSteps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProfessionalCampaignSteps_ProfessionalCampaigns_ProfessionalCampaignId",
                        column: x => x.ProfessionalCampaignId,
                        principalTable: "ProfessionalCampaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProfessionalCampaignSteps_Users_ReviewedByUserId",
                        column: x => x.ReviewedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_SmsApprovalRequests_ProfessionalCampaignStepId",
                table: "SmsApprovalRequests",
                column: "ProfessionalCampaignStepId");

            migrationBuilder.CreateIndex(
                name: "IX_ProfessionalCampaignRecipients_ContactId",
                table: "ProfessionalCampaignRecipients",
                column: "ContactId");

            migrationBuilder.CreateIndex(
                name: "IX_ProfessionalCampaignRecipients_ProfessionalCampaignId_MobileNumber",
                table: "ProfessionalCampaignRecipients",
                columns: new[] { "ProfessionalCampaignId", "MobileNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProfessionalCampaigns_Status_IsActive",
                table: "ProfessionalCampaigns",
                columns: new[] { "Status", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_ProfessionalCampaigns_UserId_IsDeleted_CreatedAt",
                table: "ProfessionalCampaigns",
                columns: new[] { "UserId", "IsDeleted", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ProfessionalCampaignSteps_ApprovalStatus",
                table: "ProfessionalCampaignSteps",
                column: "ApprovalStatus");

            migrationBuilder.CreateIndex(
                name: "IX_ProfessionalCampaignSteps_ProfessionalCampaignId_StepOrder",
                table: "ProfessionalCampaignSteps",
                columns: new[] { "ProfessionalCampaignId", "StepOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProfessionalCampaignSteps_ReviewedByUserId",
                table: "ProfessionalCampaignSteps",
                column: "ReviewedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ProfessionalCampaignSteps_Status_ScheduledAtUtc",
                table: "ProfessionalCampaignSteps",
                columns: new[] { "Status", "ScheduledAtUtc" });

            migrationBuilder.AddForeignKey(
                name: "FK_SmsApprovalRequests_ProfessionalCampaignSteps_ProfessionalCampaignStepId",
                table: "SmsApprovalRequests",
                column: "ProfessionalCampaignStepId",
                principalTable: "ProfessionalCampaignSteps",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SmsApprovalRequests_ProfessionalCampaignSteps_ProfessionalCampaignStepId",
                table: "SmsApprovalRequests");

            migrationBuilder.DropTable(
                name: "ProfessionalCampaignRecipients");

            migrationBuilder.DropTable(
                name: "ProfessionalCampaignSteps");

            migrationBuilder.DropTable(
                name: "ProfessionalCampaigns");

            migrationBuilder.DropIndex(
                name: "IX_SmsApprovalRequests_ProfessionalCampaignStepId",
                table: "SmsApprovalRequests");

            migrationBuilder.DropColumn(
                name: "ProfessionalCampaignStepId",
                table: "SmsApprovalRequests");
        }
    }
}
