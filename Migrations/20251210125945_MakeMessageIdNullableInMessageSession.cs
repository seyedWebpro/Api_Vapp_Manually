using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api_Vapp.Migrations
{
    /// <inheritdoc />
    public partial class MakeMessageIdNullableInMessageSession : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // حذف Foreign Key constraint موجود
            migrationBuilder.DropForeignKey(
                name: "FK_MessageSessions_Messages_MessageId",
                table: "MessageSessions");

            // تغییر column به nullable
            migrationBuilder.AlterColumn<int>(
                name: "MessageId",
                table: "MessageSessions",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            // ایجاد مجدد Foreign Key constraint با optional
            migrationBuilder.AddForeignKey(
                name: "FK_MessageSessions_Messages_MessageId",
                table: "MessageSessions",
                column: "MessageId",
                principalTable: "Messages",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // حذف Foreign Key constraint
            migrationBuilder.DropForeignKey(
                name: "FK_MessageSessions_Messages_MessageId",
                table: "MessageSessions");

            // تغییر column به NOT NULL
            migrationBuilder.AlterColumn<int>(
                name: "MessageId",
                table: "MessageSessions",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            // ایجاد مجدد Foreign Key constraint با required
            migrationBuilder.AddForeignKey(
                name: "FK_MessageSessions_Messages_MessageId",
                table: "MessageSessions",
                column: "MessageId",
                principalTable: "Messages",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
