namespace Api_Vapp.Models
{
    /// <summary>
    /// خدمت قابل رزرو در یک سیستم رزرو
    /// </summary>
    public class BookingServiceItem
    {
        public int Id { get; set; }

        public int BookingSystemId { get; set; }

        public string Title { get; set; } = string.Empty;

        public int DurationMinutes { get; set; }

        public bool HasCost { get; set; }

        /// <summary>
        /// قیمت خدمت — وقتی HasCost=true الزامی است
        /// </summary>
        public decimal? Price { get; set; }

        /// <summary>
        /// بیعانه — اختیاری
        /// </summary>
        public decimal? DepositAmount { get; set; }

        /// <summary>
        /// فاصله بین نوبت‌ها (دقیقه) — زمان استراحت پس از پایان هر نوبت
        /// </summary>
        public int BufferMinutesBetweenAppointments { get; set; }

        /// <summary>
        /// حداکثر رزرو روزانه — null یعنی بدون محدودیت
        /// </summary>
        public int? MaxDailyReservations { get; set; }

        /// <summary>
        /// حداکثر فاصله یادآوری (دقیقه) — برای سازگاری و فیلتر کوئری؛ با Max(ReminderOffsets) همگام می‌شود.
        /// </summary>
        public int ReminderOffsetMinutes { get; set; }

        /// <summary>
        /// لیست زمان‌های یادآوری به‌صورت JSON — مثلاً [60,1440] یعنی ۱ ساعت و ۱ روز قبل.
        /// </summary>
        public string ReminderOffsetsJson { get; set; } = "[60]";

        public int SortOrder { get; set; }

        public bool IsDeleted { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        public virtual BookingSystem BookingSystem { get; set; } = null!;

        public virtual ICollection<BookingServiceDaySchedule> DaySchedules { get; set; } = new List<BookingServiceDaySchedule>();

        public virtual ICollection<BookingScheduleException> ScheduleExceptions { get; set; } = new List<BookingScheduleException>();
    }
}
