using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api_Vapp.Migrations
{
    /// <inheritdoc />
    public partial class AddNumberSeekerImportFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ImportedAt",
                table: "NumberSeekerTasks",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ImportedCount",
                table: "NumberSeekerTasks",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ImportedNotebookId",
                table: "NumberSeekerTasks",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ImportedAt",
                table: "NumberSeekerTasks");

            migrationBuilder.DropColumn(
                name: "ImportedCount",
                table: "NumberSeekerTasks");

            migrationBuilder.DropColumn(
                name: "ImportedNotebookId",
                table: "NumberSeekerTasks");
        }
    }
}
