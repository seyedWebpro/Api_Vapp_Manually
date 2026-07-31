using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api_Vapp.Migrations
{
    /// <inheritdoc />
    public partial class AddSmsPricingSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SmsPricingSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IsBillingEnabled = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CostPerPart = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PersianFirstPageChars = table.Column<int>(type: "int", nullable: false),
                    PersianSecondPageChars = table.Column<int>(type: "int", nullable: false),
                    PersianOtherPagesChars = table.Column<int>(type: "int", nullable: false),
                    EnglishFirstPageChars = table.Column<int>(type: "int", nullable: false),
                    EnglishOtherPagesChars = table.Column<int>(type: "int", nullable: false),
                    MaxPages = table.Column<int>(type: "int", nullable: false),
                    RegularCharWeight = table.Column<int>(type: "int", nullable: false),
                    SpaceCharWeight = table.Column<int>(type: "int", nullable: false),
                    EmojiCharWeight = table.Column<int>(type: "int", nullable: false),
                    TrimContentBeforeCount = table.Column<bool>(type: "bit", nullable: false),
                    CountLeadingTrailingSpaces = table.Column<bool>(type: "bit", nullable: false),
                    LanguageDetectionSampleLength = table.Column<int>(type: "int", nullable: false),
                    DefaultLanguageIsPersian = table.Column<bool>(type: "bit", nullable: false),
                    IncludeOptOutSuffixInCalculation = table.Column<bool>(type: "bit", nullable: false),
                    OptOutSuffix = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SmsPricingSettings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SmsPricingSettings_IsDeleted",
                table: "SmsPricingSettings",
                column: "IsDeleted");

            migrationBuilder.Sql("""
                INSERT INTO SmsPricingSettings (
                    IsBillingEnabled, CostPerPart,
                    PersianFirstPageChars, PersianSecondPageChars, PersianOtherPagesChars,
                    EnglishFirstPageChars, EnglishOtherPagesChars, MaxPages,
                    RegularCharWeight, SpaceCharWeight, EmojiCharWeight,
                    TrimContentBeforeCount, CountLeadingTrailingSpaces,
                    LanguageDetectionSampleLength, DefaultLanguageIsPersian,
                    IncludeOptOutSuffixInCalculation, OptOutSuffix,
                    IsDeleted, CreatedAt)
                SELECT
                    0, 160,
                    70, 64, 67,
                    160, 153, 10,
                    1, 1, 3,
                    1, 1,
                    50, 1,
                    1, N'لغو11',
                    0, GETUTCDATE()
                WHERE NOT EXISTS (SELECT 1 FROM SmsPricingSettings WHERE IsDeleted = 0);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SmsPricingSettings");
        }
    }
}
