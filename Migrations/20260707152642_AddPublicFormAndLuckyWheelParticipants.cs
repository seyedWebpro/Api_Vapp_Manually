using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api_Vapp.Migrations
{
    /// <inheritdoc />
    public partial class AddPublicFormAndLuckyWheelParticipants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LuckyWheelParticipants",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LuckyWheelId = table.Column<int>(type: "int", nullable: false),
                    ParticipantFullName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ParticipantMobile = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    WonLuckyWheelItemId = table.Column<int>(type: "int", nullable: false),
                    ContactId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LuckyWheelParticipants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LuckyWheelParticipants_Contacts_ContactId",
                        column: x => x.ContactId,
                        principalTable: "Contacts",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_LuckyWheelParticipants_LuckyWheelItems_WonLuckyWheelItemId",
                        column: x => x.WonLuckyWheelItemId,
                        principalTable: "LuckyWheelItems",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_LuckyWheelParticipants_LuckyWheels_LuckyWheelId",
                        column: x => x.LuckyWheelId,
                        principalTable: "LuckyWheels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserFormSubmissions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserFormId = table.Column<int>(type: "int", nullable: false),
                    ParticipantFullName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ParticipantMobile = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ContactId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserFormSubmissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserFormSubmissions_Contacts_ContactId",
                        column: x => x.ContactId,
                        principalTable: "Contacts",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_UserFormSubmissions_UserForms_UserFormId",
                        column: x => x.UserFormId,
                        principalTable: "UserForms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserFormFieldValues",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserFormSubmissionId = table.Column<int>(type: "int", nullable: false),
                    FieldKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserFormFieldValues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserFormFieldValues_UserFormSubmissions_UserFormSubmissionId",
                        column: x => x.UserFormSubmissionId,
                        principalTable: "UserFormSubmissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LuckyWheelParticipants_ContactId",
                table: "LuckyWheelParticipants",
                column: "ContactId");

            migrationBuilder.CreateIndex(
                name: "IX_LuckyWheelParticipants_CreatedAt",
                table: "LuckyWheelParticipants",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_LuckyWheelParticipants_LuckyWheelId",
                table: "LuckyWheelParticipants",
                column: "LuckyWheelId");

            migrationBuilder.CreateIndex(
                name: "IX_LuckyWheelParticipants_LuckyWheelId_ParticipantMobile",
                table: "LuckyWheelParticipants",
                columns: new[] { "LuckyWheelId", "ParticipantMobile" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LuckyWheelParticipants_ParticipantMobile",
                table: "LuckyWheelParticipants",
                column: "ParticipantMobile");

            migrationBuilder.CreateIndex(
                name: "IX_LuckyWheelParticipants_WonLuckyWheelItemId",
                table: "LuckyWheelParticipants",
                column: "WonLuckyWheelItemId");

            migrationBuilder.CreateIndex(
                name: "IX_UserFormFieldValues_UserFormSubmissionId",
                table: "UserFormFieldValues",
                column: "UserFormSubmissionId");

            migrationBuilder.CreateIndex(
                name: "IX_UserFormFieldValues_UserFormSubmissionId_FieldKey",
                table: "UserFormFieldValues",
                columns: new[] { "UserFormSubmissionId", "FieldKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserFormSubmissions_ContactId",
                table: "UserFormSubmissions",
                column: "ContactId");

            migrationBuilder.CreateIndex(
                name: "IX_UserFormSubmissions_CreatedAt",
                table: "UserFormSubmissions",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_UserFormSubmissions_ParticipantMobile",
                table: "UserFormSubmissions",
                column: "ParticipantMobile");

            migrationBuilder.CreateIndex(
                name: "IX_UserFormSubmissions_UserFormId",
                table: "UserFormSubmissions",
                column: "UserFormId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LuckyWheelParticipants");

            migrationBuilder.DropTable(
                name: "UserFormFieldValues");

            migrationBuilder.DropTable(
                name: "UserFormSubmissions");
        }
    }
}
