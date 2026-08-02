using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api_Vapp.Migrations
{
    /// <inheritdoc />
    public partial class AddPushEnabledToNotificationSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF COL_LENGTH('dbo.UserNotificationSettings', 'PushEnabled') IS NULL
                BEGIN
                    ALTER TABLE [UserNotificationSettings] ADD [PushEnabled] bit NOT NULL
                        CONSTRAINT [DF_UserNotificationSettings_PushEnabled] DEFAULT CAST(1 AS bit);
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF COL_LENGTH('dbo.UserNotificationSettings', 'PushEnabled') IS NOT NULL
                BEGIN
                    DECLARE @df sysname;
                    SELECT @df = dc.name
                    FROM sys.default_constraints dc
                    INNER JOIN sys.columns c ON c.default_object_id = dc.object_id
                    WHERE dc.parent_object_id = OBJECT_ID(N'dbo.UserNotificationSettings')
                      AND c.name = N'PushEnabled';
                    IF @df IS NOT NULL
                        EXEC(N'ALTER TABLE [UserNotificationSettings] DROP CONSTRAINT [' + @df + N']');
                    ALTER TABLE [UserNotificationSettings] DROP COLUMN [PushEnabled];
                END
                """);
        }
    }
}
