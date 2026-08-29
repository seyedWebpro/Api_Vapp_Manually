namespace Api_Vapp.Models
{
    /// <summary>
    /// لاگ مرکزی ارسال و وضعیت دلیوری پیامک — مشترک بین همه ماژول‌ها
    /// </summary>
    public class SmsDeliveryRecord
    {
        public int Id { get; set; }

        public int UserId { get; set; }

        /// <summary>ماژول مبدأ — مثلا MessageCampaign, Cashback</summary>
        public string SourceModule { get; set; } = string.Empty;

        /// <summary>شناسه موجودیت مبدأ (کمپین، کش‌بک، ...)</summary>
        public int? SourceEntityId { get; set; }

        /// <summary>عنوان نمایشی مبدأ</summary>
        public string? SourceEntityLabel { get; set; }

        public string Mobile { get; set; } = string.Empty;

        public long Sid { get; set; }

        /// <summary>متن پیامک ارسالی — برای گزارش جزئیات (ایران‌نوین برنمی‌گرداند)</summary>
        public string? MessageText { get; set; }

        /// <summary>مبلغ کسرشده از کیف پول کاربر هنگام ارسال (۰ = بدون صورتحساب / ارسال دستی)</summary>
        public decimal ChargedAmount { get; set; }

        /// <summary>تعداد پارت صورتحساب‌شده — فقط برای گزارش</summary>
        public int PartsCount { get; set; }

        /// <summary>Sent | Failed</summary>
        public string SendStatus { get; set; } = "Sent";

        /// <summary>دسته نمایشی — DeliveredToPhone, SentToOperator, ...</summary>
        public string DeliveryCategory { get; set; } = Constants.SmsDeliveryCategories.PendingSync;

        public int? ProviderStatusCode { get; set; }

        public string? ProviderStatusMessage { get; set; }

        public bool IsDeliveryFinal { get; set; }

        /// <summary>زمان claim/ثبت برگشت هزینه به‌خاطر نرسیدن به گوشی (idempotency)</summary>
        public DateTime? WalletRefundedAt { get; set; }

        /// <summary>شناسه تراکنش کیف پول برگشت (در صورت موفقیت)</summary>
        public int? WalletRefundTransactionId { get; set; }

        public DateTime SentAt { get; set; } = DateTime.UtcNow;

        public DateTime? LastCheckedAt { get; set; }

        public int CheckAttempts { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        public bool IsDeleted { get; set; }

        public virtual User User { get; set; } = null!;
    }

    public static class SmsSendStatuses
    {
        public const string Sent = "Sent";
        public const string Failed = "Failed";
    }
}
