namespace Api_Vapp.Models
{
    /// <summary>
    /// سیستم رزرو نوبت — اطلاعات کسب‌وکار و لینک عمومی
    /// </summary>
    public class BookingSystem
    {
        public int Id { get; set; }

        public int UserId { get; set; }

        public string Title { get; set; } = string.Empty;

        public string ActivityType { get; set; } = string.Empty;

        public string? Description { get; set; }

        /// <summary>
        /// شناسه URL عمومی — یکتا در کل سیستم
        /// </summary>
        public string Slug { get; set; } = string.Empty;

        public BookingSystemStatus Status { get; set; } = BookingSystemStatus.Published;

        public bool SaveToPhonebook { get; set; }

        public bool IsActive { get; set; } = true;

        public bool IsDeleted { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        public DateTime? PublishedAt { get; set; }

        public virtual User User { get; set; } = null!;

        public virtual ICollection<BookingSystemNotebook> Notebooks { get; set; } = new List<BookingSystemNotebook>();

        public virtual ICollection<BookingServiceItem> Services { get; set; } = new List<BookingServiceItem>();
    }

    public enum BookingSystemStatus
    {
        Draft = 0,
        Published = 1
    }
}
