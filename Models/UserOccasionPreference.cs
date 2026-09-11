namespace Api_Vapp.Models
{
    /// <summary>
    /// تنظیمات هر کاربر برای یک مناسبت: فعال/غیرفعال، قالب ویرایش‌شده، تأیید متن
    /// </summary>
    public class UserOccasionPreference
    {
        public int Id { get; set; }

        public int UserId { get; set; }

        public int SpecialOccasionId { get; set; }

        /// <summary>فعال بودن این مناسبت برای کاربر</summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// متن قالب اختصاصی کاربر.
        /// اگر خالی باشد از DefaultMessage مناسبت استفاده می‌شود.
        /// با ویرایش → Pending و نیاز به تأیید ادمین.
        /// </summary>
        public string? CustomMessage { get; set; }

        /// <summary>Pending | Approved | Rejected</summary>
        public string TemplateApprovalStatus { get; set; } = "Approved";

        public DateTime? TemplateApprovedAt { get; set; }

        public int? TemplateApprovedByUserId { get; set; }

        public string? TemplateRejectionReason { get; set; }

        /// <summary>قالب پیام لینک‌شده (اختیاری)</summary>
        public int? MessageTemplateId { get; set; }

        public bool IsDeleted { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        public virtual User User { get; set; } = null!;

        public virtual SpecialOccasion SpecialOccasion { get; set; } = null!;

        public virtual MessageTemplate? MessageTemplate { get; set; }
    }
}
