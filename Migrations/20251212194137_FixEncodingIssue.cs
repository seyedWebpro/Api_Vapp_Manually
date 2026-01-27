using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api_Vapp.Migrations
{
    /// <inheritdoc />
    public partial class FixEncodingIssue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Fix encoding issue by updating with proper Unicode strings
            migrationBuilder.Sql(@"
                UPDATE AutomatedMessages
                SET Title = CASE AutomationType
                    WHEN 'Birthday' THEN N'تبریک تولد'
                    WHEN 'CashbackExpiry' THEN N'یادآوری انقضای کش‌بک'
                    WHEN 'Welcome' THEN N'پیام خوش‌آمدگویی'
                    WHEN 'PurchaseReminder' THEN N'یادآوری خرید'
                    WHEN 'SpecialOccasion' THEN N'مناسبت‌های خاص'
                    WHEN 'Custom' THEN N'اتوماسیون سفارشی'
                    ELSE N'پیام خودکار'
                END,
                Description = CASE AutomationType
                    WHEN 'Birthday' THEN N'ارسال پیام خودکار در روز تولد مشتریان'
                    WHEN 'CashbackExpiry' THEN N'۲ روز قبل از پایان اعتبار کش بک برای مشتری پیام ارسال می‌شود'
                    WHEN 'Welcome' THEN N'پس از اولین ثبت شماره مشتری، پیام خوش آمدگویی ارسال می‌شود'
                    WHEN 'PurchaseReminder' THEN N'اگر مشتری ۳۰ روز خرید نداشته باشد، پیام ارسال می‌شود'
                    WHEN 'SpecialOccasion' THEN N'ارسال پیام در مناسبت‌های مخصوص سال'
                    WHEN 'Custom' THEN N'شرط، زمان و پیام را خودتان مشخص کنید'
                    ELSE N'پیام خودکار سفارشی'
                END
                WHERE Title IS NULL OR Description IS NULL
                OR Title LIKE '%?%' OR Description LIKE '%?%'
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Rollback: This migration only fixes encoding, so no need to rollback
            // The previous migration already handles rollback properly
        }
    }
}
