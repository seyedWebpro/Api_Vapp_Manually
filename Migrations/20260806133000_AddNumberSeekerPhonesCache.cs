using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api_Vapp.Migrations
{
    [DbContext(typeof(Api_Vapp.Data.Api_Context))]
    [Migration("20260806133000_AddNumberSeekerPhonesCache")]
    public partial class AddNumberSeekerPhonesCache : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PhonesJson",
                table: "NumberSeekerTasks",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PhonesPersistedAt",
                table: "NumberSeekerTasks",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "NumberSeekerTasks",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PhonesJson",
                table: "NumberSeekerTasks");

            migrationBuilder.DropColumn(
                name: "PhonesPersistedAt",
                table: "NumberSeekerTasks");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "NumberSeekerTasks");
        }
    }
}
