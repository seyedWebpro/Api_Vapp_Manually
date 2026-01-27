namespace Api_Vapp.Models
{
    /// <summary>
    /// مدل پیام خودکار
    /// برای پیام‌های اتوماسیون مانند تبریک تولد، یادآوری و غیره
    /// </summary>
    public class AutomatedMessage
    {
        // شناسه یکتای پیام خودکار
        public int Id { get; set; }

        // شناسه کاربر
        public int UserId { get; set; }

        // نوع اتوماسیون
        public string AutomationType { get; set; } = string.Empty;
        // Birthday, CashbackExpiry, Welcome, PurchaseReminder, SpecialOccasion, Custom

        // عنوان پیام خودکار
        public string? Title { get; set; }

        // توضیحات
        public string? Description { get; set; }

        // شناسه پیام (برای استفاده از متن پیام)
        public int? MessageId { get; set; }

        // متن پیام (مستقیم یا از Message)
        public string? MessageContent { get; set; }

        // شرایط فعال‌سازی (JSON - برای Custom Automation)
        public string? ActivationConditions { get; set; }

        // تعداد روز قبل از رویداد برای ارسال (برای CashbackExpiry و PurchaseReminder)
        public int? DaysBeforeEvent { get; set; }

        // شناسه مناسبت خاص (برای SpecialOccasion)
        public int? SpecialOccasionId { get; set; }

        // زمان ارسال برنامه‌ریزی شده (ساعت و دقیقه در روز - برای Birthday و سایر اتوماسیون‌ها)
        public TimeSpan? ScheduledTime { get; set; }

        // آیکون
        public string? Icon { get; set; }

        // فعال/غیرفعال
        public bool IsActive { get; set; } = true;

        // وضعیت پیام خودکار (Draft, Active, Paused, Completed)
        public string Status { get; set; } = "Draft"; // Draft, Active, Paused, Completed

        // تاریخ آخرین اجرا
        public DateTime? LastExecutedAt { get; set; }

        // حذف شده (Soft Delete)
        public bool IsDeleted { get; set; } = false;

        #region Timestamps

        // تاریخ و زمان ایجاد
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // تاریخ و زمان آخرین به‌روزرسانی
        public DateTime? UpdatedAt { get; set; }

        #endregion

        #region Navigation Properties

        // کاربر مالک
        public virtual User User { get; set; } = null!;

        // پیام مربوطه
        public virtual Message? Message { get; set; }

        // مناسبت خاص
        public virtual SpecialOccasion? SpecialOccasion { get; set; }

        // تاریخچه اجراها
        public virtual ICollection<AutomationExecution> Executions { get; set; } = new List<AutomationExecution>();

        // کمپین‌های ایجاد شده از این پیام خودکار
        public virtual ICollection<MessageCampaign> Campaigns { get; set; } = new List<MessageCampaign>();

        #endregion
    }
}


