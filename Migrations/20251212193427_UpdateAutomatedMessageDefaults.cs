using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api_Vapp.Migrations
{
    /// <inheritdoc />
    public partial class UpdateAutomatedMessageDefaults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Update existing AutomatedMessage records with default titles and descriptions
            migrationBuilder.Sql(@"
                UPDATE AutomatedMessages
                SET Title = CASE AutomationType
                    WHEN 'Birthday' THEN 'تبریک تولد'
                    WHEN 'CashbackExpiry' THEN 'یادآوری انقضای کش‌بک'
                    WHEN 'Welcome' THEN 'پیام خوش‌آمدگویی'
                    WHEN 'PurchaseReminder' THEN 'یادآوری خرید'
                    WHEN 'SpecialOccasion' THEN 'مناسبت‌های خاص'
                    WHEN 'Custom' THEN 'اتوماسیون سفارشی'
                    ELSE 'پیام خودکار'
                END,
                Description = CASE AutomationType
                    WHEN 'Birthday' THEN 'ارسال پیام خودکار در روز تولد مشتریان'
                    WHEN 'CashbackExpiry' THEN '۲ روز قبل از پایان اعتبار کش بک برای مشتری پیام ارسال می‌شود'
                    WHEN 'Welcome' THEN 'پس از اولین ثبت شماره مشتری، پیام خوش آمدگویی ارسال می‌شود'
                    WHEN 'PurchaseReminder' THEN 'اگر مشتری ۳۰ روز خرید نداشته باشد، پیام ارسال می‌شود'
                    WHEN 'SpecialOccasion' THEN 'ارسال پیام در مناسبت‌های مخصوص سال'
                    WHEN 'Custom' THEN 'شرط، زمان و پیام را خودتان مشخص کنید'
                    ELSE 'پیام خودکار سفارشی'
                END
                WHERE Title IS NULL OR Description IS NULL
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Rollback: Set titles and descriptions back to NULL for automated messages
            // This will only affect records that were updated by this migration
            migrationBuilder.Sql(@"
                UPDATE AutomatedMessages
                SET Title = NULL, Description = NULL
                WHERE AutomationType IN ('Birthday', 'CashbackExpiry', 'Welcome', 'PurchaseReminder', 'SpecialOccasion', 'Custom')
                AND Title IN ('تبریک تولد', 'یادآوری انقضای کش‌بک', 'پیام خوش‌آمدگویی', 'یادآوری خرید', 'مناسبت‌های خاص', 'اتوماسیون سفارشی')
            ");
        }
    }
}
