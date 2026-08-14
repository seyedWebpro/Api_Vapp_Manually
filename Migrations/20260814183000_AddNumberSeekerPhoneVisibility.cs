using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api_Vapp.Migrations
{
    /// <summary>
    /// مخفی‌سازی شماره‌های شماره‌جو برای کاربر عادی + مجوز مشاهده برای کاربران خاص.
    /// </summary>
    [DbContext(typeof(Api_Vapp.Data.Api_Context))]
    [Migration("20260814183000_AddNumberSeekerPhoneVisibility")]
    public partial class AddNumberSeekerPhoneVisibility : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CanViewNumberSeekerPhones",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HideMobileNumber",
                table: "Contacts",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_Contacts_HideMobileNumber",
                table: "Contacts",
                column: "HideMobileNumber");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Contacts_HideMobileNumber",
                table: "Contacts");

            migrationBuilder.DropColumn(
                name: "HideMobileNumber",
                table: "Contacts");

            migrationBuilder.DropColumn(
                name: "CanViewNumberSeekerPhones",
                table: "Users");
        }
    }
}
