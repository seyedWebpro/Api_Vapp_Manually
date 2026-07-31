using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api_Vapp.Migrations
{
    /// <inheritdoc />
    public partial class AddUserDeviceIsDeleted : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Idempotent — ممکن است ستون قبلاً دستی اضافه شده باشد
            migrationBuilder.Sql("""
                IF COL_LENGTH('dbo.UserDevices', 'IsDeleted') IS NULL
                BEGIN
                    ALTER TABLE [UserDevices] ADD [IsDeleted] bit NOT NULL
                        CONSTRAINT [DF_UserDevices_IsDeleted] DEFAULT CAST(0 AS bit);
                END

                IF NOT EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE name = N'IX_UserDevices_IsDeleted'
                      AND object_id = OBJECT_ID(N'dbo.UserDevices'))
                BEGIN
                    CREATE INDEX [IX_UserDevices_IsDeleted] ON [UserDevices] ([IsDeleted]);
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE name = N'IX_UserDevices_IsDeleted'
                      AND object_id = OBJECT_ID(N'dbo.UserDevices'))
                    DROP INDEX [IX_UserDevices_IsDeleted] ON [UserDevices];

                IF COL_LENGTH('dbo.UserDevices', 'IsDeleted') IS NOT NULL
                BEGIN
                    DECLARE @df sysname;
                    SELECT @df = dc.name
                    FROM sys.default_constraints dc
                    INNER JOIN sys.columns c ON c.default_object_id = dc.object_id
                    WHERE dc.parent_object_id = OBJECT_ID(N'dbo.UserDevices')
                      AND c.name = N'IsDeleted';
                    IF @df IS NOT NULL
                        EXEC(N'ALTER TABLE [UserDevices] DROP CONSTRAINT [' + @df + N']');
                    ALTER TABLE [UserDevices] DROP COLUMN [IsDeleted];
                END
                """);
        }
    }
}
