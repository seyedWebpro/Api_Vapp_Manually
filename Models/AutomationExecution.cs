namespace Api_Vapp.Models
{
    /// <summary>
    /// مدل اجرای اتوماسیون
    /// ثبت تاریخچه اجراهای پیام‌های خودکار
    /// </summary>
    public class AutomationExecution
    {
        // شناسه یکتای اجرا
        public int Id { get; set; }

        // شناسه پیام خودکار
        public int AutomatedMessageId { get; set; }

        // شناسه مخاطب (اختیاری - برای پیام‌های شخصی)
        public int? ContactId { get; set; }

        // محتوای پیام ارسال شده
        public string? MessageContent { get; set; }

        // تعداد پیام‌های ارسال شده در این اجرا
        public int SentCount { get; set; } = 0;

        // تعداد پیام‌های ناموفق
        public int FailedCount { get; set; } = 0;

        // وضعیت اجرا (Success, Partial, Failed)
        public string Status { get; set; } = "Pending"; // Success, Partial, Failed

        // پیام خطا (در صورت وجود)
        public string? ErrorMessage { get; set; }

        // تاریخ و زمان اجرا
        public DateTime ExecutedAt { get; set; } = DateTime.UtcNow;

        // مدت زمان اجرا (به میلی‌ثانیه)
        public int? ExecutionDurationMs { get; set; }

        #region Navigation Properties

        // پیام خودکار مربوطه
        public virtual AutomatedMessage AutomatedMessage { get; set; } = null!;

        // مخاطب مربوطه
        public virtual Contact? Contact { get; set; }

        #endregion
    }
}


