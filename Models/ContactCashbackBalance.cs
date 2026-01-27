namespace Api_Vapp.Models
{
    /// <summary>
    /// مدل موجودی کش‌بک مخاطب
    /// نگهداری موجودی کش‌بک هر مخاطب
    /// </summary>
    public class ContactCashbackBalance
    {
        // شناسه یکتا
        public int Id { get; set; }

        // شناسه مخاطب
        public int ContactId { get; set; }

        // شناسه کاربر (صاحب کسب‌وکار)
        public int UserId { get; set; }

        // موجودی کل کش‌بک (تومان)
        public decimal TotalBalance { get; set; } = 0;

        // موجودی قابل استفاده (تومان) - کش‌بک‌هایی که منقضی نشده‌اند
        public decimal UsableBalance { get; set; } = 0;

        // درصد کش‌بک فعال (آخرین کش‌بک اعمال شده)
        public decimal? ActiveCashbackPercentage { get; set; }

        // روزهای باقیمانده تا انقضای کش‌بک
        public int? ExpiryDays { get; set; }

        // تاریخ انقضای کش‌بک (برای قدیمی‌ترین کش‌بک فعال)
        public DateTime? ExpiryDate { get; set; }

        #region Timestamps

        // تاریخ و زمان ایجاد
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // تاریخ و زمان آخرین به‌روزرسانی
        public DateTime? UpdatedAt { get; set; }

        // RowVersion برای Optimistic Concurrency Control
        public byte[]? RowVersion { get; set; }

        #endregion

        #region Navigation Properties

        // مخاطب مرتبط
        public virtual Contact Contact { get; set; } = null!;

        // کاربر (صاحب کسب‌وکار)
        public virtual User User { get; set; } = null!;

        #endregion
    }

    /// <summary>
    /// مدل تراکنش کش‌بک دستی
    /// ثبت افزودن یا برداشت دستی کش‌بک
    /// </summary>
    public class ManualCashbackTransaction
    {
        // شناسه یکتا
        public int Id { get; set; }

        // شناسه مخاطب
        public int ContactId { get; set; }

        // شناسه کاربر (صاحب کسب‌وکار)
        public int UserId { get; set; }

        // نوع تراکنش (Add: افزودن، Withdraw: برداشت)
        public string TransactionType { get; set; } = ManualCashbackTransactionTypes.Add;

        // مبلغ تراکنش (تومان)
        public decimal Amount { get; set; }

        // موجودی قبل از تراکنش (تومان)
        public decimal BalanceBefore { get; set; }

        // موجودی بعد از تراکنش (تومان)
        public decimal BalanceAfter { get; set; }

        // توضیحات
        public string? Description { get; set; }

        // تاریخ انقضا (برای تراکنش‌های افزودن)
        public DateTime? ExpiryDate { get; set; }

        // روزهای اعتبار (برای تراکنش‌های افزودن)
        public int? ValidityDays { get; set; }

        #region Timestamps

        // تاریخ و زمان ایجاد
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        #endregion

        #region Navigation Properties

        // مخاطب مرتبط
        public virtual Contact Contact { get; set; } = null!;

        // کاربر (صاحب کسب‌وکار)
        public virtual User User { get; set; } = null!;

        #endregion
    }

    /// <summary>
    /// انواع تراکنش کش‌بک دستی
    /// </summary>
    public static class ManualCashbackTransactionTypes
    {
        public const string Add = "Add";           // افزودن کش‌بک
        public const string Withdraw = "Withdraw"; // برداشت کش‌بک
    }
}
