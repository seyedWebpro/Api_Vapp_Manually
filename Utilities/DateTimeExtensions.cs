namespace Api_Vapp.Utilities
{
    /// <summary>
    /// Extension methods برای کار با DateTime و UTC
    /// تمامی تاریخ و زمان‌ها در این سیستم باید به UTC ذخیره شوند
    /// </summary>
    public static class DateTimeExtensions
    {
        /// <summary>
        /// اطمینان از UTC بودن DateTime
        /// اگر DateTime به صورت Local یا Unspecified باشد، به UTC تبدیل می‌شود
        /// </summary>
        /// <param name="dateTime">تاریخ و زمان ورودی</param>
        /// <returns>تاریخ و زمان به UTC</returns>
        public static DateTime EnsureUtc(this DateTime dateTime)
        {
            return dateTime.Kind switch
            {
                DateTimeKind.Utc => dateTime,
                DateTimeKind.Local => dateTime.ToUniversalTime(),
                // Unspecified - فرض می‌کنیم که از فرانت به صورت UTC ارسال شده
                _ => DateTime.SpecifyKind(dateTime, DateTimeKind.Utc)
            };
        }

        /// <summary>
        /// اطمینان از UTC بودن DateTime? (nullable)
        /// </summary>
        /// <param name="dateTime">تاریخ و زمان ورودی (nullable)</param>
        /// <returns>تاریخ و زمان به UTC یا null</returns>
        public static DateTime? EnsureUtc(this DateTime? dateTime)
        {
            return dateTime?.EnsureUtc();
        }

        /// <summary>
        /// تبدیل تاریخ تولد به UTC
        /// برای تاریخ تولد، فقط تاریخ مهم است و زمان صفر در نظر گرفته می‌شود
        /// </summary>
        /// <param name="dateOfBirth">تاریخ تولد</param>
        /// <returns>تاریخ تولد به UTC با زمان 00:00:00</returns>
        public static DateTime EnsureDateOnlyUtc(this DateTime dateOfBirth)
        {
            // فقط تاریخ را نگه می‌داریم و زمان را صفر می‌کنیم
            var dateOnly = dateOfBirth.Date;
            return DateTime.SpecifyKind(dateOnly, DateTimeKind.Utc);
        }

        /// <summary>
        /// تبدیل تاریخ تولد nullable به UTC
        /// </summary>
        /// <param name="dateOfBirth">تاریخ تولد (nullable)</param>
        /// <returns>تاریخ تولد به UTC یا null</returns>
        public static DateTime? EnsureDateOnlyUtc(this DateTime? dateOfBirth)
        {
            return dateOfBirth?.EnsureDateOnlyUtc();
        }

        /// <summary>
        /// بررسی اینکه آیا امروز (UTC) تولد این مخاطب است
        /// مقایسه فقط بر اساس ماه و روز انجام می‌شود
        /// </summary>
        /// <param name="dateOfBirth">تاریخ تولد</param>
        /// <param name="todayUtc">تاریخ امروز به UTC</param>
        /// <returns>true اگر امروز تولد باشد</returns>
        public static bool IsBirthdayToday(this DateTime dateOfBirth, DateTime todayUtc)
        {
            return dateOfBirth.Month == todayUtc.Month && dateOfBirth.Day == todayUtc.Day;
        }

        /// <summary>
        /// بررسی اینکه آیا امروز (UTC) تولد این مخاطب است
        /// </summary>
        /// <param name="dateOfBirth">تاریخ تولد (nullable)</param>
        /// <param name="todayUtc">تاریخ امروز به UTC</param>
        /// <returns>true اگر امروز تولد باشد، false اگر تاریخ تولد null باشد</returns>
        public static bool IsBirthdayToday(this DateTime? dateOfBirth, DateTime todayUtc)
        {
            return dateOfBirth.HasValue && dateOfBirth.Value.IsBirthdayToday(todayUtc);
        }

        /// <summary>
        /// ایجاد DateTime از تاریخ UTC و TimeSpan
        /// مناسب برای زمان‌بندی ارسال پیام‌ها
        /// </summary>
        /// <param name="dateUtc">تاریخ به UTC</param>
        /// <param name="time">زمان (ساعت و دقیقه)</param>
        /// <returns>DateTime کامل به UTC</returns>
        public static DateTime CombineWithTime(this DateTime dateUtc, TimeSpan time)
        {
            // اطمینان از اینکه فقط تاریخ استفاده شود
            var dateOnly = dateUtc.Date;
            var combined = dateOnly.Add(time);
            return DateTime.SpecifyKind(combined, DateTimeKind.Utc);
        }

        /// <summary>
        /// بررسی اینکه آیا زمان فعلی در بازه زمانی مجاز برای ارسال است
        /// </summary>
        /// <param name="scheduledTimeUtc">زمان برنامه‌ریزی شده به UTC</param>
        /// <param name="nowUtc">زمان فعلی به UTC</param>
        /// <param name="toleranceMinutes">تلرانس به دقیقه (پیش‌فرض 5 دقیقه)</param>
        /// <returns>true اگر در بازه زمانی مجاز باشد</returns>
        public static bool IsWithinScheduleWindow(this DateTime scheduledTimeUtc, DateTime nowUtc, int toleranceMinutes = 5)
        {
            var timeDifference = Math.Abs((nowUtc - scheduledTimeUtc).TotalMinutes);
            return timeDifference <= toleranceMinutes;
        }
    }
}



