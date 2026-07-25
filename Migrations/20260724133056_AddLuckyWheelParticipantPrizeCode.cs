using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api_Vapp.Migrations
{
    /// <inheritdoc />
    public partial class AddLuckyWheelParticipantPrizeCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PrizeCode",
                table: "LuckyWheelParticipants",
                type: "nvarchar(12)",
                maxLength: 12,
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE LuckyWheelParticipants
                SET PrizeCode = 'LW-' + RIGHT('000000' + CAST(Id AS varchar(10)), 6)
                WHERE PrizeCode IS NULL OR PrizeCode = ''
                """);

            migrationBuilder.AlterColumn<string>(
                name: "PrizeCode",
                table: "LuckyWheelParticipants",
                type: "nvarchar(12)",
                maxLength: 12,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(12)",
                oldMaxLength: 12,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_LuckyWheelParticipants_LuckyWheelId_PrizeCode",
                table: "LuckyWheelParticipants",
                columns: new[] { "LuckyWheelId", "PrizeCode" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LuckyWheelParticipants_LuckyWheelId_PrizeCode",
                table: "LuckyWheelParticipants");

            migrationBuilder.DropColumn(
                name: "PrizeCode",
                table: "LuckyWheelParticipants");
        }
    }
}
