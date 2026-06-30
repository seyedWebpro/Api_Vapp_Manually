namespace Api_Vapp.Models
{
    /// <summary>
    /// برنامه هفتگی یک خدمت — ساعت کاری هر روز (UTC)
    /// </summary>
    public class BookingServiceDaySchedule
    {
        public int Id { get; set; }

        public int BookingServiceItemId { get; set; }

        /// <summary>
        /// 0=Sunday … 6=Saturday (DayOfWeek استاندارد .NET)
        /// </summary>
        public DayOfWeek DayOfWeek { get; set; }

        public bool IsOpen { get; set; }

        /// <summary>
        /// ساعت شروع کاری — UTC
        /// </summary>
        public TimeSpan? StartTimeUtc { get; set; }

        /// <summary>
        /// ساعت پایان کاری — UTC
        /// </summary>
        public TimeSpan? EndTimeUtc { get; set; }

        public virtual BookingServiceItem BookingServiceItem { get; set; } = null!;
    }
}
