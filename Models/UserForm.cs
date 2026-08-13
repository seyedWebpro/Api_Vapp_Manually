namespace Api_Vapp.Models
{
    /// <summary>
    /// فرم ساخته‌شده توسط کاربر (فرم‌ساز)
    /// </summary>
    public class UserForm : IQuickSendApprovable
    {
        public int Id { get; set; }

        public int UserId { get; set; }

        public string Title { get; set; } = string.Empty;

        public string? Description { get; set; }

        /// <summary>
        /// شناسه URL عمومی — پس از publish تنظیم می‌شود
        /// </summary>
        public string? Slug { get; set; }

        /// <summary>
        /// کلید قالب از سمت کلاینت (مثلاً recruitment) — آماده برای مرحله ۳
        /// </summary>
        public string? TemplateKey { get; set; }

        /// <summary>
        /// شناسه قالب سمت سرور — فعلاً null (مرحله ۳)
        /// </summary>
        public int? TemplateId { get; set; }

        public UserFormStatus Status { get; set; } = UserFormStatus.Draft;

        public bool SaveToPhonebook { get; set; }

        /// <summary>
        /// برای فرم‌های منتشرشده — غیرفعال = لینk عمومی کار نمی‌کند
        /// </summary>
        public bool IsActive { get; set; } = true;

        public bool IsDeleted { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        public DateTime? PublishedAt { get; set; }

        /// <summary>وضعیت تأیید ادمین برای ارسال سریع (Pending / Approved / Rejected)</summary>
        public string ApprovalStatus { get; set; } = "Pending";

        public DateTime? ApprovedAt { get; set; }

        public int? ApprovedByUserId { get; set; }

        public string? RejectionReason { get; set; }

        public virtual User User { get; set; } = null!;

        public virtual ICollection<UserFormField> Fields { get; set; } = new List<UserFormField>();

        public virtual ICollection<UserFormNotebook> Notebooks { get; set; } = new List<UserFormNotebook>();
    }
}
