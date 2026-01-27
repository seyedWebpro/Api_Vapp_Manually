namespace Api_Vapp.Models
{
    /// <summary>
    /// مدل تراکنش کش‌بک
    /// ثبت واریز کش‌بک به مخاطبین
    /// </summary>
    public class CashbackTransaction
    {
        // شناسه یکتای تراکنش کش‌بک
        public int Id { get; set; }

        // شناسه کش‌بک مرتبط
        public int CashbackId { get; set; }

        // شناسه مخاطب دریافت‌کننده
        public int ContactId { get; set; }

        // مبلغ کش‌بک واریز شده (تومان)
        public decimal Amount { get; set; }

        // مبلغ خرید مرتبط (تومان) - برای محاسبه کش‌بک درصدی
        public decimal? PurchaseAmount { get; set; }

        // وضعیت تراکنش کش‌بک
        public string Status { get; set; } = "Pending";

        // زمان برنامه‌ریزی شده برای واریز
        public DateTime? ScheduledAt { get; set; }

        // توضیحات
        public string? Description { get; set; }

        #region Timestamps

        // تاریخ و زمان ایجاد
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // تاریخ و زمان واریز
        public DateTime? DepositedAt { get; set; }

        #endregion

        #region Navigation Properties

        // کش‌بک مرتبط
        public virtual Cashback Cashback { get; set; } = null!;

        // مخاطب دریافت‌کننده
        public virtual Contact Contact { get; set; } = null!;

        #endregion
    }

    /// <summary>
    /// وضعیت‌های تراکنش کش‌بک
    /// </summary>
    public static class CashbackTransactionStatuses
    {
        public const string Pending = "Pending";         // در انتظار واریز
        public const string Scheduled = "Scheduled";     // زمان‌بندی شده
        public const string Deposited = "Deposited";     // واریز شده
        public const string Failed = "Failed";           // ناموفق
        public const string Cancelled = "Cancelled";     // لغو شده
    }
}

