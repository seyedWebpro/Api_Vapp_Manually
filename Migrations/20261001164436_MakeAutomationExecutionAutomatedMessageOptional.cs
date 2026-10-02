using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api_Vapp.Migrations
{
    /// <inheritdoc />
    public partial class MakeAutomationExecutionAutomatedMessageOptional : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "AutomatedMessageId",
                table: "AutomationExecutions",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.CreateIndex(
                name: "IX_AutomationExecutions_SpecialOccasionId_ContactId_ExecutedAt",
                table: "AutomationExecutions",
                columns: new[] { "SpecialOccasionId", "ContactId", "ExecutedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AutomationExecutions_SpecialOccasionId_ContactId_ExecutedAt",
                table: "AutomationExecutions");

            migrationBuilder.AlterColumn<int>(
                name: "AutomatedMessageId",
                table: "AutomationExecutions",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);
        }
    }
}
