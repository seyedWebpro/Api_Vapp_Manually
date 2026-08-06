using Api_Vapp.Constants;

namespace Api_Vapp.Models
{
    /// <summary>
    /// اعلان درون‌برنامه‌ای برای نمایش در زنگوله اپ
    /// </summary>
    public class InAppNotification
    {
        public int Id { get; set; }

        public int UserId { get; set; }

        public string Title { get; set; } = string.Empty;

        public string Body { get; set; } = string.Empty;

        /// <summary>
        /// نوع اعلان — مثلاً TemplateApproved / MessageRejected
        /// </summary>
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// دسته تنظیمات Push متناظر
        /// </summary>
        public NotificationCategory Category { get; set; } = NotificationCategory.Suggestions;

        public bool IsRead { get; set; }

        public DateTime? ReadAt { get; set; }

        /// <summary>
        /// مسیر عمیق اپ (اختیاری) — مثلاً /sms/templates
        /// </summary>
        public string? ActionUrl { get; set; }

        public int? RelatedEntityId { get; set; }

        public string? RelatedEntityType { get; set; }

        /// <summary>
        /// JSON اختیاری (مثلاً دلیل رد)
        /// </summary>
        public string? Metadata { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        public bool IsDeleted { get; set; }

        public virtual User User { get; set; } = null!;
    }
}
