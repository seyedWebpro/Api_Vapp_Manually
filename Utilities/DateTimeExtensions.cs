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
        public static DateTime? EnsureUtc(this DateTime? dateTime)
        {
            return dateTime?.EnsureUtc();
        }

        /// <summary>
        /// تبدیل تاریخ تقویمی (تولد / مناسبت) به UTC با زمان 00:00:00.
        /// فقط بخش تاریخ مهم است — از شیفت timezone جلوگیری می‌کند.
        /// </summary>
        public static DateTime EnsureDateOnlyUtc(this DateTime dateOfBirth)
        {
            var dateOnly = dateOfBirth.Date;
            return DateTime.SpecifyKind(dateOnly, DateTimeKind.Utc);
        }

        /// <summary>
        /// تبدیل تاریخ تقویمی nullable به UTC
        /// </summary>
        public static DateTime? EnsureDateOnlyUtc(this DateTime? dateOfBirth)
        {
            return dateOfBirth?.EnsureDateOnlyUtc();
        }

        /// <summary>
        /// بررسی اینکه آیا امروز (UTC) تولد این مخاطب است.
        /// مقایسه بر اساس ماه و روز؛ برای ۲۹ فوریه در سال غیرکبیسه → ۲۸ فوریه.
        /// </summary>
        public static bool IsBirthdayToday(this DateTime dateOfBirth, DateTime todayUtc)
        {
            var today = todayUtc.Date;

            if (dateOfBirth.Month == today.Month && dateOfBirth.Day == today.Day)
                return true;

            // Feb 29 در سال غیرکبیسه: در ۲۸ فوریه تبریک بفرست
            if (dateOfBirth.Month == 2 && dateOfBirth.Day == 29
                && today.Month == 2 && today.Day == 28
                && !DateTime.IsLeapYear(today.Year))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// بررسی اینکه آیا امروز (UTC) تولد این مخاطب است
        /// </summary>
        public static bool IsBirthdayToday(this DateTime? dateOfBirth, DateTime todayUtc)
        {
            return dateOfBirth.HasValue && dateOfBirth.Value.IsBirthdayToday(todayUtc);
        }

        /// <summary>
        /// مقایسه روز تقویمی ماه/روز (مناسبت سالانه) — مشابه تولد با پشتیبانی Feb 29.
        /// </summary>
        public static bool IsSameMonthDay(this DateTime occasionDate, DateTime todayUtc)
        {
            return occasionDate.IsBirthdayToday(todayUtc);
        }

        /// <summary>
        /// ایجاد DateTime از تاریخ UTC و TimeSpan
        /// </summary>
        public static DateTime CombineWithTime(this DateTime dateUtc, TimeSpan time)
        {
            var dateOnly = dateUtc.Date;
            var combined = dateOnly.Add(time);
            return DateTime.SpecifyKind(combined, DateTimeKind.Utc);
        }

        /// <summary>
        /// بررسی اینکه آیا زمان فعلی در بازه زمانی مجاز برای ارسال است (تلرانس دوطرفه)
        /// </summary>
        public static bool IsWithinScheduleWindow(this DateTime scheduledTimeUtc, DateTime nowUtc, int toleranceMinutes = 5)
        {
            var timeDifference = Math.Abs((nowUtc - scheduledTimeUtc).TotalMinutes);
            return timeDifference <= toleranceMinutes;
        }

        /// <summary>
        /// آیا زمان ارسال فرا رسیده یا گذشته (catch-up)؟
        /// قبل از ScheduledTime برنمی‌گردد؛ بعد از آن تا پایان روز مجاز است (با dedupe جداگانه).
        /// اگر ScheduledTime null باشد، فوری اجرا می‌شود.
        /// </summary>
        public static bool HasReachedScheduledTime(this TimeSpan? scheduledTime, DateTime todayUtc, DateTime nowUtc)
        {
            if (!scheduledTime.HasValue)
                return true;

            var scheduledUtc = todayUtc.Date.CombineWithTime(scheduledTime.Value);
            return nowUtc >= scheduledUtc;
        }

        /// <summary>
        /// ساخت تاریخ امن در سال مشخص (Feb 29 → Feb 28 در سال غیرکبیسه)
        /// </summary>
        public static DateTime SafeDateInYear(int year, int month, int day)
        {
            if (month == 2 && day == 29 && !DateTime.IsLeapYear(year))
                day = 28;

            var daysInMonth = DateTime.DaysInMonth(year, month);
            if (day > daysInMonth)
                day = daysInMonth;

            return DateTime.SpecifyKind(new DateTime(year, month, day), DateTimeKind.Utc);
        }
    }
}
