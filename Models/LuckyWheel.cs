namespace Api_Vapp.Models
{
    /// <summary>
    /// گردونه شانس ساخته‌شده توسط کاربر
    /// </summary>
    public class LuckyWheel
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

        public virtual User User { get; set; } = null!;

        public virtual ICollection<LuckyWheelItem> Items { get; set; } = new List<LuckyWheelItem>();

        public virtual ICollection<LuckyWheelNotebook> Notebooks { get; set; } = new List<LuckyWheelNotebook>();
    }
}
