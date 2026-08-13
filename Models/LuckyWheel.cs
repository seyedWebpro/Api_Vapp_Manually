namespace Api_Vapp.Models
{
    /// <summary>
    /// گردونه شانس ساخته‌شده توسط کاربر
    /// </summary>
    public class LuckyWheel : IQuickSendApprovable
    {
        public int Id { get; set; }

        public int UserId { get; set; }

        public string Title { get; set; } = string.Empty;

        public string? Description { get; set; }

        /// <summary>
        /// شناسه URL عمومی — پس از publish تنظیم می‌شود
        /// </summary>
        public string? Slug { get; set; }

        public LuckyWheelStatus Status { get; set; } = LuckyWheelStatus.Draft;

        public bool SaveToPhonebook { get; set; }

        /// <summary>
        /// برای گردونه‌های منتشرشده — غیرفعال = لینک عمومی کار نمی‌کند
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

        public virtual ICollection<LuckyWheelItem> Items { get; set; } = new List<LuckyWheelItem>();

        public virtual ICollection<LuckyWheelNotebook> Notebooks { get; set; } = new List<LuckyWheelNotebook>();
    }
}
