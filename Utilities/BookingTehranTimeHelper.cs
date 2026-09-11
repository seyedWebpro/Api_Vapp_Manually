namespace Api_Vapp.Utilities
{
    /// <summary>
    /// روز تقویمی تهران برای داشبورد رزرو (آمار و برنامه امروز).
    /// </summary>
    public static class BookingTehranTimeHelper
    {
        private static readonly TimeZoneInfo TehranTimeZone = ResolveTehranTimeZone();

        public static DateOnly TodayTehran(DateTime? utcNow = null)
        {
            var utc = ToUtc(utcNow ?? DateTime.UtcNow);
            var tehran = TimeZoneInfo.ConvertTimeFromUtc(utc, TehranTimeZone);
            return DateOnly.FromDateTime(tehran);
        }

        /// <summary>
        /// بازه UTC معادل یک روز تقویمی تهران: [start, end).
        /// </summary>
        public static (DateTime StartUtc, DateTime EndUtc) GetDayUtcRange(DateOnly tehranDate)
        {
            var localMidnight = DateTime.SpecifyKind(
                tehranDate.ToDateTime(TimeOnly.MinValue),
                DateTimeKind.Unspecified);
            var startUtc = TimeZoneInfo.ConvertTimeToUtc(localMidnight, TehranTimeZone);
            return (startUtc, startUtc.AddDays(1));
        }

        private static DateTime ToUtc(DateTime value) =>
            value.Kind == DateTimeKind.Utc
                ? value
                : DateTime.SpecifyKind(value, DateTimeKind.Utc);

        private static TimeZoneInfo ResolveTehranTimeZone()
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("Asia/Tehran");
            }
            catch (TimeZoneNotFoundException)
            {
                return TimeZoneInfo.FindSystemTimeZoneById("Iran Standard Time");
            }
        }
    }
}
