using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api_Vapp.Migrations
{
    /// <inheritdoc />
    public partial class AddUserFormBuilderModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserForms",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Slug = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TemplateKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TemplateId = table.Column<int>(type: "int", nullable: true),
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
                    table.PrimaryKey("PK_UserForms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserForms_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserFormFields",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserFormId = table.Column<int>(type: "int", nullable: false),
                    FieldKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FieldType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Label = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Placeholder = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    HelpText = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    IsRequired = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    SourceFieldKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserFormFields", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserFormFields_UserForms_UserFormId",
                        column: x => x.UserFormId,
                        principalTable: "UserForms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserFormNotebooks",
                columns: table => new
                {
                    UserFormId = table.Column<int>(type: "int", nullable: false),
                    ContactNotebookId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserFormNotebooks", x => new { x.UserFormId, x.ContactNotebookId });
                    table.ForeignKey(
                        name: "FK_UserFormNotebooks_ContactNotebooks_ContactNotebookId",
                        column: x => x.ContactNotebookId,
                        principalTable: "ContactNotebooks",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_UserFormNotebooks_UserForms_UserFormId",
                        column: x => x.UserFormId,
                        principalTable: "UserForms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserFormFields_UserFormId",
                table: "UserFormFields",
                column: "UserFormId");

            migrationBuilder.CreateIndex(
                name: "IX_UserFormFields_UserFormId_FieldKey",
                table: "UserFormFields",
                columns: new[] { "UserFormId", "FieldKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserFormNotebooks_ContactNotebookId",
                table: "UserFormNotebooks",
                column: "ContactNotebookId");

            migrationBuilder.CreateIndex(
                name: "IX_UserForms_IsDeleted",
                table: "UserForms",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_UserForms_Slug",
                table: "UserForms",
                column: "Slug",
                unique: true,
                filter: "[Slug] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_UserForms_Status",
                table: "UserForms",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_UserForms_UserId",
                table: "UserForms",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserFormFields");

            migrationBuilder.DropTable(
                name: "UserFormNotebooks");

            migrationBuilder.DropTable(
                name: "UserForms");
        }
    }
}
