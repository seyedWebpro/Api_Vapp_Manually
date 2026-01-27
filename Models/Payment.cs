namespace Api_Vapp.Models
{
    /// <summary>
    /// مدل پرداخت
    /// ثبت اطلاعات پرداخت‌های درگاه بانکی
    /// </summary>
    public class Payment
    {
        // شناسه یکتای پرداخت
        public int Id { get; set; }

        // شناسه کاربر
        public int UserId { get; set; }

        // مبلغ پرداخت (تومان)
        public decimal Amount { get; set; }

        // نوع پرداخت (WalletCharge: شارژ کیف پول، Subscription: خرید اشتراک)
        public string PaymentType { get; set; } = string.Empty;

        // درگاه پرداخت (Behpardakht: به‌پرداخت، Zarinpal: زرین‌پال، ...)
        public string Gateway { get; set; } = string.Empty;

        // شماره سفارش (Order ID)
        public string OrderId { get; set; } = string.Empty;

        // شماره مرجع درگاه (Reference ID)
        public string? RefId { get; set; }

        // شماره پیگیری بانکی (Reference Number)
        public string? ReferenceNumber { get; set; }

        // شماره تراکنش بانکی (Transaction ID)
        public string? TransactionId { get; set; }

        // شماره کارت پرداخت کننده (Masked)
        public string? CardNumber { get; set; }

        // وضعیت پرداخت
        public string Status { get; set; } = "Pending";

        // کد خطای درگاه (در صورت وجود)
        public string? ErrorCode { get; set; }

        // پیام خطای درگاه (در صورت وجود)
        public string? ErrorMessage { get; set; }

        // توضیحات
        public string? Description { get; set; }

        // آدرس IP کاربر
        public string? IpAddress { get; set; }

        // User Agent مرورگر
        public string? UserAgent { get; set; }

        // URL بازگشت بعد از پرداخت
        public string? CallbackUrl { get; set; }

        // داده‌های اضافی (JSON)
        public string? MetaData { get; set; }

        #region Timestamps

        // تاریخ و زمان ایجاد
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // تاریخ و زمان پرداخت
        public DateTime? PaidAt { get; set; }

        // تاریخ و زمان تأیید
        public DateTime? VerifiedAt { get; set; }

        #endregion

        #region Navigation Properties

        // کاربر پرداخت‌کننده
        public virtual User User { get; set; } = null!;

        // تراکنش کیف پول مرتبط
        public virtual ICollection<WalletTransaction> WalletTransactions { get; set; } = new List<WalletTransaction>();

        #endregion
    }

    /// <summary>
    /// انواع پرداخت
    /// </summary>
    public static class PaymentTypes
    {
        public const string WalletCharge = "WalletCharge";       // شارژ کیف پول
        public const string Subscription = "Subscription";       // خرید اشتراک
        public const string SmsPurchase = "SmsPurchase";         // خرید پیامک
    }

    /// <summary>
    /// درگاه‌های پرداخت
    /// </summary>
    public static class PaymentGateways
    {
        public const string Behpardakht = "Behpardakht";   // به‌پرداخت ملت
        public const string Zarinpal = "Zarinpal";         // زرین‌پال
        public const string Wallet = "Wallet";             // پرداخت از کیف پول
    }

    /// <summary>
    /// وضعیت‌های پرداخت
    /// </summary>
    public static class PaymentStatuses
    {
        public const string Pending = "Pending";           // در انتظار پرداخت
        public const string Processing = "Processing";     // در حال پردازش
        public const string Paid = "Paid";                 // پرداخت شده (در انتظار تأیید)
        public const string Verified = "Verified";         // تأیید شده
        public const string Failed = "Failed";             // ناموفق
        public const string Cancelled = "Cancelled";       // لغو شده
        public const string Refunded = "Refunded";         // استرداد شده
    }
}

