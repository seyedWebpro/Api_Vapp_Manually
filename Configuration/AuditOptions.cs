namespace Api_Vapp.Configuration
{
    /// <summary>تنظیمات نگهداری و هشدار audit.</summary>
    public class AuditOptions
    {
        public const string SectionName = "Audit";

        /// <summary>حذف لاگ‌های قدیمی‌تر از این تعداد روز (پیش‌فرض ۱۸۰).</summary>
        public int RetentionDays { get; set; } = 180;

        /// <summary>حداقل روز نگهداری قبل از حذف — کمتر از این پاک نمی‌شود (پیش‌فرض ۹۰).</summary>
        public int MinRetentionDays { get; set; } = 90;

        /// <summary>فاصله اجرای جاب retention به ساعت.</summary>
        public int RetentionIntervalHours { get; set; } = 24;

        /// <summary>حداکثر ردیف حذف در هر batch.</summary>
        public int RetentionBatchSize { get; set; } = 5000;

        /// <summary>فعال بودن هشدار اسپایک لاگین ناموفق ادمین.</summary>
        public bool AdminLoginFailAlertEnabled { get; set; } = true;

        /// <summary>پنجره زمانی بررسی اسپایک (دقیقه).</summary>
        public int AdminLoginFailWindowMinutes { get; set; } = 15;

        /// <summary>آستانه تعداد fail در پنجره برای هشدار.</summary>
        public int AdminLoginFailThreshold { get; set; } = 10;

        /// <summary>فاصله چک آلرت به دقیقه.</summary>
        public int AdminLoginFailCheckIntervalMinutes { get; set; } = 5;

        /// <summary>
        /// اگر true و Full-Text روی SQL نصب باشد، SearchInJson از CONTAINS استفاده می‌کند.
        /// </summary>
        public bool FullTextEnabled { get; set; } = true;
    }
}
