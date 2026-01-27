namespace Api_Vapp.Models
{
    /// <summary>
    /// مدل تراکنش‌های کیف پول
    /// ثبت تمام تراکنش‌های مالی (شارژ، کش‌بک، خرید اشتراک و ...)
    /// </summary>
    public class WalletTransaction
    {
        // شناسه یکتای تراکنش
        public int Id { get; set; }

        // شناسه کاربر صاحب کیف پول
        public int UserId { get; set; }

        // نوع تراکنش (Deposit: واریز، Withdrawal: برداشت، Cashback: کش‌بک، Purchase: خرید)
        public string TransactionType { get; set; } = string.Empty;

        // مبلغ تراکنش (مثبت برای واریز، منفی برای برداشت)
        public decimal Amount { get; set; }

        // موجودی قبل از تراکنش
        public decimal BalanceBefore { get; set; }

        // موجودی بعد از تراکنش
        public decimal BalanceAfter { get; set; }

        // عنوان تراکنش (مثل: شارژ کیف پول، کش‌بک خرید، خرید اشتراک پیامک)
        public string Title { get; set; } = string.Empty;

        // توضیحات تراکنش
        public string? Description { get; set; }

        // شماره پیگیری (برای پرداخت‌های درگاه بانکی)
        public string? ReferenceNumber { get; set; }

        // شناسه پرداخت مرتبط (در صورت وجود)
        public int? PaymentId { get; set; }

        // شناسه کش‌بک مرتبط (در صورت وجود)
        public int? CashbackId { get; set; }

        // وضعیت تراکنش (Pending: در انتظار، Completed: موفق، Failed: ناموفق، Cancelled: لغو شده)
        public string Status { get; set; } = "Pending";

        #region Timestamps

        // تاریخ و زمان ایجاد
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // تاریخ و زمان تکمیل تراکنش
        public DateTime? CompletedAt { get; set; }

        #endregion

        #region Navigation Properties

        // کاربر صاحب کیف پول
        public virtual User User { get; set; } = null!;

        // پرداخت مرتبط
        public virtual Payment? Payment { get; set; }

        // کش‌بک مرتبط
        public virtual Cashback? Cashback { get; set; }

        #endregion
    }

    /// <summary>
    /// انواع تراکنش کیف پول
    /// </summary>
    public static class WalletTransactionTypes
    {
        public const string Deposit = "Deposit";           // واریز (شارژ کیف پول)
        public const string Withdrawal = "Withdrawal";     // برداشت
        public const string Cashback = "Cashback";         // کش‌بک
        public const string Purchase = "Purchase";         // خرید (مثلاً خرید اشتراک پیامک)
        public const string Refund = "Refund";             // استرداد
    }

    /// <summary>
    /// وضعیت‌های تراکنش
    /// </summary>
    public static class TransactionStatuses
    {
        public const string Pending = "Pending";       // در انتظار
        public const string Completed = "Completed";   // موفق
        public const string Failed = "Failed";         // ناموفق
        public const string Cancelled = "Cancelled";   // لغو شده
    }
}

