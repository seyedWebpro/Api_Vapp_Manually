namespace Api_Vapp.Models
{
    /// <summary>
    /// مدل گیرنده پیام در کمپین
    /// ارتباط بین کمپین و مخاطبین
    /// </summary>
    public class MessageRecipient
    {
        // شناسه یکتای گیرنده
        public int Id { get; set; }

        // شناسه کمپین
        public int CampaignId { get; set; }

        // شناسه مخاطب (اختیاری - می‌تواند مستقیم شماره باشد)
        public int? ContactId { get; set; }

        // شماره موبایل گیرنده
        public string MobileNumber { get; set; } = string.Empty;

        // نام کامل گیرنده (اختیاری)
        public string? FullName { get; set; }

        // متن پیام شخصی‌سازی شده برای این گیرنده
        public string? PersonalizedContent { get; set; }

        // وضعیت ارسال (Pending, Sent, Failed)
        public string Status { get; set; } = "Pending"; // Pending, Sent, Failed

        // تاریخ و زمان ارسال
        public DateTime? SentAt { get; set; }

        // شناسه ارسال از سرویس SMS (برای پیگیری)
        public string? SmsServiceId { get; set; }

        // پیام خطا (در صورت شکست)
        public string? ErrorMessage { get; set; }

        // تعداد تلاش برای ارسال
        public int RetryCount { get; set; } = 0;

        #region Timestamps

        // تاریخ و زمان ایجاد
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        #endregion

        #region Navigation Properties

        // کمپین مربوطه
        public virtual MessageCampaign Campaign { get; set; } = null!;

        // مخاطب مربوطه (در صورت وجود)
        public virtual Contact? Contact { get; set; }

        #endregion
    }
}


