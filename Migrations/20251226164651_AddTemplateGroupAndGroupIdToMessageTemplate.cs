using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api_Vapp.Migrations
{
    /// <inheritdoc />
    public partial class AddTemplateGroupAndGroupIdToMessageTemplate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "GroupId",
                table: "MessageTemplates",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TemplateGroups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Icon = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TemplateGroups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TemplateGroups_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MessageTemplates_GroupId",
                table: "MessageTemplates",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_TemplateGroups_DisplayOrder",
                table: "TemplateGroups",
                column: "DisplayOrder");

            migrationBuilder.CreateIndex(
                name: "IX_TemplateGroups_IsDeleted",
                table: "TemplateGroups",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_TemplateGroups_UserId",
                table: "TemplateGroups",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_MessageTemplates_TemplateGroups_GroupId",
                table: "MessageTemplates",
                column: "GroupId",
                principalTable: "TemplateGroups",
                principalColumn: "Id",
                onDelete: ReferentialAction.NoAction);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MessageTemplates_TemplateGroups_GroupId",
                table: "MessageTemplates");

            migrationBuilder.DropTable(
                name: "TemplateGroups");

            migrationBuilder.DropIndex(
                name: "IX_MessageTemplates_GroupId",
                table: "MessageTemplates");

            migrationBuilder.DropColumn(
                name: "GroupId",
                table: "MessageTemplates");
        }
    }
}
