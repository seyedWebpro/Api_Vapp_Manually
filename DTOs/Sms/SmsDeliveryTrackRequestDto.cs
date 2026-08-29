namespace Api_Vapp.DTOs.Sms
{
    /// <summary>
    /// درخواست ثبت ارسال پیامک برای پیگیری دلیوری
    /// </summary>
    public class SmsDeliveryTrackRequestDto
    {
        public int UserId { get; set; }
        public string SourceModule { get; set; } = string.Empty;
        public int? SourceEntityId { get; set; }
        public string? SourceEntityLabel { get; set; }
        public string Mobile { get; set; } = string.Empty;
        public long Sid { get; set; }
        public string? MessageText { get; set; }
        public DateTime? SentAt { get; set; }

        /// <summary>مبلغ واقعی کسرشده از کیف پول کاربر برای این پیامک (۰ اگر صورتحساب غیرفعال بود)</summary>
        public decimal ChargedAmount { get; set; }

        /// <summary>تعداد پارت صورتحساب‌شده</summary>
        public int PartsCount { get; set; }
    }
}
