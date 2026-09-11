using System.Globalization;
using Api_Vapp.Constants;

namespace Api_Vapp.Utilities
{
    /// <summary>
    /// تطبیق دقیق مناسبت‌های سالانه با روز تقویمی تهران (جلالی / میلادی / قمری).
    /// زمان ارسال هم بر اساس ساعت محلی تهران تفسیر می‌شود.
    /// </summary>
    public static class OccasionCalendarHelper
    {
        private static readonly TimeZoneInfo TehranTimeZone = ResolveTehranTimeZone();
        private static readonly PersianCalendar PersianCalendar = new();
        private static readonly HijriCalendar HijriCalendar = new();

        public readonly record struct CalendarDayParts(
            DateOnly TehranDate,
            int GregorianYear,
            int GregorianMonth,
            int GregorianDay,
            int JalaliYear,
            int JalaliMonth,
            int JalaliDay,
            int HijriYear,
            int HijriMonth,
            int HijriDay);

        public static DateTime NowUtc(DateTime? utcNow = null) => ToUtc(utcNow ?? DateTime.UtcNow);

        public static DateTime ToTehran(DateTime utcNow)
        {
            var utc = ToUtc(utcNow);
            return TimeZoneInfo.ConvertTimeFromUtc(utc, TehranTimeZone);
        }

        public static CalendarDayParts GetTodayParts(DateTime? utcNow = null)
        {
            var tehran = ToTehran(NowUtc(utcNow));
            var tehranDate = DateOnly.FromDateTime(tehran);

            return new CalendarDayParts(
                TehranDate: tehranDate,
                GregorianYear: tehran.Year,
                GregorianMonth: tehran.Month,
                GregorianDay: tehran.Day,
                JalaliYear: PersianCalendar.GetYear(tehran),
                JalaliMonth: PersianCalendar.GetMonth(tehran),
                JalaliDay: PersianCalendar.GetDayOfMonth(tehran),
                HijriYear: HijriCalendar.GetYear(tehran),
                HijriMonth: HijriCalendar.GetMonth(tehran),
                HijriDay: HijriCalendar.GetDayOfMonth(tehran));
        }

        /// <summary>
        /// آیا ماه/روز مناسبت با «امروز تهران» در تقویم مشخص‌شده یکی است؟
        /// </summary>
        public static bool IsOccasionToday(
            string calendarType,
            int month,
            int day,
            CalendarDayParts today)
        {
            if (month < 1 || day < 1)
                return false;

            var normalized = OccasionCalendarTypes.Normalize(calendarType);

            return normalized switch
            {
                OccasionCalendarTypes.Gregorian =>
                    MatchesMonthDay(month, day, today.GregorianMonth, today.GregorianDay, today.GregorianYear, isGregorian: true),
                OccasionCalendarTypes.Hijri =>
                    MatchesMonthDay(month, day, today.HijriMonth, today.HijriDay, today.HijriYear, isGregorian: false, hijri: true),
                _ =>
                    MatchesMonthDay(month, day, today.JalaliMonth, today.JalaliDay, today.JalaliYear, isGregorian: false, jalali: true)
            };
        }

        /// <summary>
        /// آیا زمان ارسال (ساعت تهران) فرا رسیده یا گذشته (catch-up تا پایان روز تهران)؟
        /// </summary>
        public static bool HasReachedScheduledTimeTehran(TimeSpan? scheduledTehranTime, DateTime? utcNow = null)
        {
            if (!scheduledTehranTime.HasValue)
                return true;

            var nowUtc = NowUtc(utcNow);
            var tehranNow = ToTehran(nowUtc);
            var scheduledTehran = new DateTime(
                tehranNow.Year,
                tehranNow.Month,
                tehranNow.Day,
                scheduledTehranTime.Value.Hours,
                scheduledTehranTime.Value.Minutes,
                scheduledTehranTime.Value.Seconds,
                DateTimeKind.Unspecified);

            var scheduledUtc = TimeZoneInfo.ConvertTimeToUtc(scheduledTehran, TehranTimeZone);
            return nowUtc >= scheduledUtc;
        }

        /// <summary>
        /// بازه UTC معادل روز تقویمی تهران برای dedupe اجرا.
        /// </summary>
        public static (DateTime StartUtc, DateTime EndUtc) GetTehranDayUtcRange(DateOnly tehranDate)
        {
            var localMidnight = DateTime.SpecifyKind(
                tehranDate.ToDateTime(TimeOnly.MinValue),
                DateTimeKind.Unspecified);
            var startUtc = TimeZoneInfo.ConvertTimeToUtc(localMidnight, TehranTimeZone);
            return (startUtc, startUtc.AddDays(1));
        }

        /// <summary>
        /// ساخت OccasionDate مرجع (میلادی نیمه‌شب UTC) از ماه/روز جلالی در سال جلالی فعلی تهران.
        /// فقط برای نمایش/سازگاری با فیلد قدیمی OccasionDate.
        /// </summary>
        public static DateTime BuildReferenceOccasionDateUtc(
            string calendarType,
            int month,
            int day,
            DateTime? utcNow = null)
        {
            var today = GetTodayParts(utcNow);
            var normalized = OccasionCalendarTypes.Normalize(calendarType);

            try
            {
                DateTime localUnspecified = normalized switch
                {
                    OccasionCalendarTypes.Gregorian =>
                        new DateTime(today.GregorianYear, ClampMonth(month), ClampDayGregorian(today.GregorianYear, month, day), 0, 0, 0, DateTimeKind.Unspecified),
                    OccasionCalendarTypes.Hijri =>
                        HijriCalendar.ToDateTime(today.HijriYear, ClampMonth(month), ClampDayHijri(today.HijriYear, month, day), 0, 0, 0, 0),
                    _ =>
                        PersianCalendar.ToDateTime(today.JalaliYear, ClampMonth(month), ClampDayJalali(today.JalaliYear, month, day), 0, 0, 0, 0)
                };

                var local = DateTime.SpecifyKind(localUnspecified, DateTimeKind.Unspecified);
                var utc = TimeZoneInfo.ConvertTimeToUtc(local, TehranTimeZone);
                return utc.EnsureDateOnlyUtc();
            }
            catch
            {
                return DateTime.SpecifyKind(new DateTime(2000, ClampMonth(month), Math.Min(Math.Max(day, 1), 28)), DateTimeKind.Utc);
            }
        }

        /// <summary>
        /// تعداد روز باقی‌مانده تا نزدیک‌ترین رخداد ماه/روز در تقویم مشخص‌شده (بر اساس تهران).
        /// </summary>
        public static int CalculateDaysRemaining(
            string calendarType,
            int month,
            int day,
            DateTime? utcNow = null)
        {
            var nowUtc = NowUtc(utcNow);
            var todayParts = GetTodayParts(nowUtc);
            if (IsOccasionToday(calendarType, month, day, todayParts))
                return 0;

            for (var offset = 1; offset <= 400; offset++)
            {
                var futureUtc = nowUtc.AddDays(offset);
                var parts = GetTodayParts(futureUtc);
                if (IsOccasionToday(calendarType, month, day, parts))
                    return offset;
            }

            return -1;
        }

        public static (int Month, int Day) ExtractMonthDayFromDate(DateTime date, string calendarType)
        {
            var utcDate = date.EnsureDateOnlyUtc();
            // تاریخ ذخیره‌شده date-only است؛ برای استخراج اجزای تقویم از همان روز به‌عنوان Unspecified تهران استفاده می‌کنیم
            var local = DateTime.SpecifyKind(utcDate.Date, DateTimeKind.Unspecified);
            var normalized = OccasionCalendarTypes.Normalize(calendarType);

            return normalized switch
            {
                OccasionCalendarTypes.Gregorian => (local.Month, local.Day),
                OccasionCalendarTypes.Hijri => (HijriCalendar.GetMonth(local), HijriCalendar.GetDayOfMonth(local)),
                _ => (PersianCalendar.GetMonth(local), PersianCalendar.GetDayOfMonth(local))
            };
        }

        private static bool MatchesMonthDay(
            int occasionMonth,
            int occasionDay,
            int todayMonth,
            int todayDay,
            int todayYear,
            bool isGregorian,
            bool jalali = false,
            bool hijri = false)
        {
            if (occasionMonth == todayMonth && occasionDay == todayDay)
                return true;

            // روز آخر ماه (مثلاً ۳۰ اسفند در سال غیرکبیسه جلالی، یا ۲۹ قمری)
            var maxDay = GetDaysInMonth(todayYear, occasionMonth, isGregorian, jalali, hijri);
            if (occasionMonth == todayMonth && occasionDay > maxDay && todayDay == maxDay)
                return true;

            return false;
        }

        private static int GetDaysInMonth(int year, int month, bool isGregorian, bool jalali, bool hijri)
        {
            month = ClampMonth(month);
            if (isGregorian)
                return DateTime.DaysInMonth(year, month);
            if (hijri)
                return HijriCalendar.GetDaysInMonth(year, month);
            if (jalali)
                return PersianCalendar.GetDaysInMonth(year, month);
            return 30;
        }

        private static int ClampMonth(int month) => Math.Clamp(month, 1, 12);

        private static int ClampDayGregorian(int year, int month, int day)
        {
            month = ClampMonth(month);
            var max = DateTime.DaysInMonth(year, month);
            return Math.Clamp(day, 1, max);
        }

        private static int ClampDayJalali(int year, int month, int day)
        {
            month = ClampMonth(month);
            var max = PersianCalendar.GetDaysInMonth(year, month);
            return Math.Clamp(day, 1, max);
        }

        private static int ClampDayHijri(int year, int month, int day)
        {
            month = ClampMonth(month);
            var max = HijriCalendar.GetDaysInMonth(year, month);
            return Math.Clamp(day, 1, max);
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
