namespace Api_Vapp.Models
{
    /// <summary>
    /// روز تعطیل یا استثنا برای یک خدمت
    /// </summary>
    public class BookingScheduleException
    {
        public int Id { get; set; }

        public int BookingServiceItemId { get; set; }

        /// <summary>
        /// تاریخ استثنا — UTC (فقط تاریخ)
        /// </summary>
        public DateTime ExceptionDateUtc { get; set; }

        /// <summary>
        /// Holiday | Leave
        /// </summary>
        public string Type { get; set; } = BookingScheduleExceptionTypes.Holiday;

        public string? Label { get; set; }

        public bool IsDeleted { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public virtual BookingServiceItem BookingServiceItem { get; set; } = null!;
    }

    public static class BookingScheduleExceptionTypes
    {
        public const string Holiday = "Holiday";
        public const string Leave = "Leave";
    }
}
