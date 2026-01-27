using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api_Vapp.Migrations
{
    /// <inheritdoc />
    public partial class FixTemplateGroupForeignKeyConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MessageTemplates_TemplateGroups_GroupId",
                table: "MessageTemplates");

            migrationBuilder.AddForeignKey(
                name: "FK_MessageTemplates_TemplateGroups_GroupId",
                table: "MessageTemplates",
                column: "GroupId",
                principalTable: "TemplateGroups",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MessageTemplates_TemplateGroups_GroupId",
                table: "MessageTemplates");

            migrationBuilder.AddForeignKey(
                name: "FK_MessageTemplates_TemplateGroups_GroupId",
                table: "MessageTemplates",
                column: "GroupId",
                principalTable: "TemplateGroups",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
