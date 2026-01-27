namespace Api_Vapp.Models
{
    /// <summary>
    /// مدل کمپین پیام
    /// شامل تنظیمات ارسال، گیرندگان و وضعیت ارسال
    /// </summary>
    public class MessageCampaign
    {
        // شناسه یکتای کمپین
        public int Id { get; set; }

        // شناسه پیام
        public int MessageId { get; set; }

        // شناسه کاربر ایجادکننده
        public int UserId { get; set; }

        // عنوان کمپین
        public string? Title { get; set; }

        // نوع ارسال (Quick, Scheduled, Automated)
        public string SendType { get; set; } = "Quick"; // Quick, Scheduled, Automated

        // شناسه پیام خودکار (برای پیام‌های خودکار)
        public int? AutomatedMessageId { get; set; }

        // تاریخ و زمان برنامه‌ریزی شده برای ارسال (در صورت Scheduled)
        public DateTime? ScheduledAt { get; set; }

        // آیا جلوگیری از ارسال تکراری فعال است
        public bool PreventDuplicate { get; set; } = false;

        // تعداد ساعت جلوگیری از ارسال تکراری (پیش‌فرض 24)
        public int DuplicatePreventionHours { get; set; } = 24;

        // آیا ارسال برای تگ‌های خاص فعال است
        public bool SendToSpecificTags { get; set; } = false;

        // لیست تگ‌های مورد نظر (JSON)
        public string? SelectedTags { get; set; }

        // تعداد گیرندگان
        public int RecipientsCount { get; set; } = 0;

        // تعداد پارت‌های پیام
        public int PartsCount { get; set; } = 0;

        // هزینه هر پارت
        public decimal CostPerPart { get; set; } = 0;

        // هزینه کل تخمینی
        public decimal EstimatedTotalCost { get; set; } = 0;

        // هزینه کل واقعی (پس از ارسال)
        public decimal ActualTotalCost { get; set; } = 0;

        // وضعیت کیف پول (Sufficient, Insufficient)
        public string WalletStatus { get; set; } = "Unknown";

        // وضعیت کمپین (Draft, Pending, Sending, Sent, Failed, Cancelled)
        public string Status { get; set; } = "Draft"; // Draft, Pending, Sending, Sent, Failed, Cancelled

        // فعال/غیرفعال بودن کمپین
        public bool IsActive { get; set; } = true;

        // تاریخ و زمان ارسال واقعی
        public DateTime? SentAt { get; set; }

        // تعداد پیام‌های ارسال شده موفق
        public int SentCount { get; set; } = 0;

        // تعداد پیام‌های ناموفق
        public int FailedCount { get; set; } = 0;

        // پیام خطا (در صورت شکست)
        public string? ErrorMessage { get; set; }

        // حذف شده (Soft Delete)
        public bool IsDeleted { get; set; } = false;

        #region Timestamps

        // تاریخ و زمان ایجاد
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // تاریخ و زمان آخرین به‌روزرسانی
        public DateTime? UpdatedAt { get; set; }

        #endregion

        #region Navigation Properties

        // پیام مربوطه
        public virtual Message Message { get; set; } = null!;

        // کاربر ایجادکننده
        public virtual User User { get; set; } = null!;

        // گیرندگان کمپین
        public virtual ICollection<MessageRecipient> Recipients { get; set; } = new List<MessageRecipient>();

        // پیام خودکار مربوطه (برای پیام‌های خودکار)
        public virtual AutomatedMessage? AutomatedMessage { get; set; }

        #endregion
    }
}


