namespace Api_Vapp.Models
{
    /// <summary>
    /// مدل پیام
    /// شامل متن پیام، قالب و تنظیمات
    /// </summary>
    public class Message
    {
        // شناسه یکتای پیام
        public int Id { get; set; }

        // شناسه کاربر ایجادکننده
        public int UserId { get; set; }

        // عنوان پیام
        public string? Title { get; set; }

        // متن پیام (می‌تواند شامل placeholder باشد)
        public string Content { get; set; } = string.Empty;

        // شناسه قالب (اختیاری)
        public int? TemplateId { get; set; }

        // تعداد کاراکترها
        public int CharacterCount { get; set; }

        // تعداد پارت‌های پیام (هر پارت 70 کاراکتر)
        public int PartsCount { get; set; }

        // آیا پیام شخصی‌سازی شده است
        public bool IsPersonalized { get; set; } = false;

        // لیست placeholder های استفاده شده (JSON)
        public string? Placeholders { get; set; }

        // وضعیت (Draft, Ready, Sent)
        public string Status { get; set; } = "Draft"; // Draft, Ready, Sent

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

        // قالب پیام
        public virtual MessageTemplate? Template { get; set; }

        // کمپین‌های مربوط به این پیام
        public virtual ICollection<MessageCampaign> Campaigns { get; set; } = new List<MessageCampaign>();

        #endregion
    }
}


