namespace Api_Vapp.Models
{
    /// <summary>
    /// مدل کش‌بک
    /// تعریف کش‌بک‌های فعال توسط صاحب کسب‌وکار
    /// </summary>
    public class Cashback
    {
        // شناسه یکتای کش‌بک
        public int Id { get; set; }

        // شناسه کاربر ایجادکننده (صاحب کسب‌وکار)
        public int UserId { get; set; }

        // عنوان کش‌بک
        public string Title { get; set; } = string.Empty;

        // توضیحات کش‌بک
        public string? Description { get; set; }

        // نوع کش‌بک (Percentage: درصدی، FixedAmount: مبلغ ثابت)
        public string CashbackType { get; set; } = "Percentage";

        // درصد کش‌بک (برای نوع درصدی)
        public decimal? Percentage { get; set; }

        // مبلغ ثابت کش‌بک (برای نوع مبلغ ثابت) - تومان
        public decimal? FixedAmount { get; set; }

        // حداکثر مبلغ کش‌بک برای هر خرید (تومان)
        public decimal? MaxCashbackAmount { get; set; }

        // حداقل مبلغ خرید برای دریافت کش‌بک (تومان)
        public decimal? MinPurchaseAmount { get; set; }

        // مدت اعتبار به روز
        public int ValidityDays { get; set; } = 30;

        // تاریخ شروع اعتبار
        public DateTime StartDate { get; set; } = DateTime.UtcNow;

        // تاریخ پایان اعتبار
        public DateTime? EndDate { get; set; }

        // زمان واریز کش‌بک (فوری، ساعتی، روزانه، ...)
        public string DepositTiming { get; set; } = "Immediate";

        // زمان مشخص واریز (در صورت انتخاب زمان‌بندی) - فقط ساعت و دقیقه
        public TimeSpan? ScheduledDepositTime { get; set; }

        // تاریخ و زمان دقیق واریز زمان‌بندی شده (UTC)
        public DateTime? ScheduledDepositDateTime { get; set; }

        // وضعیت پردازش زمان‌بندی شده
        public string ScheduleStatus { get; set; } = "None";

        // تاریخ آخرین پردازش زمان‌بندی شده
        public DateTime? LastScheduledProcessedAt { get; set; }

        // نوع مخاطبین (All: همه، NewContacts: مخاطبین جدید، SpecificNotebooks: دفترچه خاص)
        public string TargetAudience { get; set; } = "All";

        // لیست شناسه دفترچه‌ها (JSON Array) - برای نوع دفترچه خاص
        public string? TargetNotebookIds { get; set; }

        // لیست شناسه تگ‌ها (JSON Array) - برای فیلتر بر اساس تگ
        public string? TargetTagIds { get; set; }

        // ارسال برای تگ‌های خاص؟
        public bool SendToSpecificTags { get; set; } = false;

        // وضعیت فعال بودن
        public bool IsActive { get; set; } = true;

        // حذف شده (Soft Delete)
        public bool IsDeleted { get; set; } = false;

        #region Timestamps

        // تاریخ و زمان ایجاد
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // تاریخ و زمان آخرین به‌روزرسانی
        public DateTime? UpdatedAt { get; set; }

        #endregion

        #region Navigation Properties

        // کاربر ایجادکننده
        public virtual User User { get; set; } = null!;

        // تراکنش‌های کش‌بک مرتبط
        public virtual ICollection<CashbackTransaction> CashbackTransactions { get; set; } = new List<CashbackTransaction>();

        // تراکنش‌های کیف پول مرتبط
        public virtual ICollection<WalletTransaction> WalletTransactions { get; set; } = new List<WalletTransaction>();

        #endregion
    }

    /// <summary>
    /// انواع کش‌بک
    /// </summary>
    public static class CashbackTypes
    {
        public const string Percentage = "Percentage";     // درصدی
        public const string FixedAmount = "FixedAmount";   // مبلغ ثابت
    }

    /// <summary>
    /// زمان‌بندی واریز کش‌بک
    /// </summary>
    public static class CashbackDepositTiming
    {
        public const string Immediate = "Immediate";         // فوری
        public const string Scheduled = "Scheduled";         // زمان‌بندی شده
        public const string EndOfDay = "EndOfDay";           // پایان روز
    }

    /// <summary>
    /// نوع مخاطبین کش‌بک
    /// </summary>
    public static class CashbackTargetAudience
    {
        public const string All = "All";                         // همه مخاطبین
        public const string NewContacts = "NewContacts";         // مخاطبین جدید (15 روز اخیر)
        public const string SpecificNotebooks = "SpecificNotebooks"; // دفترچه‌های خاص
    }

    /// <summary>
    /// وضعیت پردازش زمان‌بندی شده کش‌بک
    /// </summary>
    public static class CashbackScheduleStatus
    {
        public const string None = "None";               // بدون زمان‌بندی
        public const string Pending = "Pending";         // در انتظار پردازش
        public const string Processing = "Processing";   // در حال پردازش
        public const string Completed = "Completed";     // پردازش شده
        public const string Failed = "Failed";           // پردازش ناموفق
        public const string Cancelled = "Cancelled";     // لغو شده
    }
}

