using System.Globalization;
using Api_Vapp.Models;

namespace Api_Vapp.Utilities
{
    /// <summary>
    /// بازه اعتبار برنامه پاداش بر اساس تقویم تهران (تاریخ‌ها بدون ساعت انتخاب می‌شوند).
    /// </summary>
    public static class ReferralProgramValidity
    {
        private static readonly TimeZoneInfo TehranTimeZone = ResolveTehranTimeZone();
        private static readonly PersianCalendar PersianCalendar = new();

        public const string CodeNotFoundMessage =
            "کد معرف یافت نشد. لطفاً کد شخصی مخاطب را بررسی کنید (مثال: REF123456).";

        public const string PublicCodeUsedMessage =
            "کدی که وارد کرده‌اید شناسه برنامه است، نه کد معرف شخصی. برای استعلام باید کد شخصی مخاطب را وارد کنید (مثل REF123456).";

        public static (DateTime StartUtc, DateTime? EndUtc) GetWindow(DateTime startDate, DateTime? endDate)
        {
            var startUtc = ToUtc(startDate);
            var startTehran = TimeZoneInfo.ConvertTimeFromUtc(startUtc, TehranTimeZone);
            var startOfDayTehran = DateTime.SpecifyKind(
                new DateTime(startTehran.Year, startTehran.Month, startTehran.Day, 0, 0, 0),
                DateTimeKind.Unspecified);
            var startWindowUtc = TimeZoneInfo.ConvertTimeToUtc(startOfDayTehran, TehranTimeZone);

            DateTime? endWindowUtc = null;
            if (endDate.HasValue)
            {
                var endUtc = ToUtc(endDate.Value);
                var endTehran = TimeZoneInfo.ConvertTimeFromUtc(endUtc, TehranTimeZone);
                var endOfDayTehran = DateTime.SpecifyKind(
                    new DateTime(endTehran.Year, endTehran.Month, endTehran.Day, 23, 59, 59, 999),
                    DateTimeKind.Unspecified);
                endWindowUtc = TimeZoneInfo.ConvertTimeToUtc(endOfDayTehran, TehranTimeZone);
            }

            return (startWindowUtc, endWindowUtc);
        }

        public static ReferralProgramState Evaluate(
            bool isActive,
            DateTime startDate,
            DateTime? endDate,
            string? programTitle = null,
            DateTime? nowUtc = null)
        {
            var now = ToUtc(nowUtc ?? DateTime.UtcNow);
            var window = GetWindow(startDate, endDate);
            var title = FormatProgramTitle(programTitle);
            var startText = FormatPersianDate(window.StartUtc);

            if (!isActive)
            {
                return new ReferralProgramState(
                    IsValid: false,
                    IsExpired: false,
                    IsNotStarted: false,
                    InvalidReason: $"{title} در حال حاضر غیرفعال است و امکان استفاده از این کد وجود ندارد. برای استفاده، ابتدا برنامه را فعال کنید.",
                    StatusMessage: $"{title} غیرفعال است.");
            }

            if (now < window.StartUtc)
            {
                return new ReferralProgramState(
                    IsValid: false,
                    IsExpired: false,
                    IsNotStarted: true,
                    InvalidReason:
                        $"{title} هنوز شروع نشده است. تاریخ شروع: {startText}. این کد از ابتدای همان روز قابل استفاده خواهد بود.",
                    StatusMessage: $"کد هنوز فعال نشده — شروع از {startText}");
            }

            if (window.EndUtc.HasValue && now > window.EndUtc.Value)
            {
                var endText = FormatPersianDate(window.EndUtc.Value);
                return new ReferralProgramState(
                    IsValid: false,
                    IsExpired: true,
                    IsNotStarted: false,
                    InvalidReason:
                        $"مهلت استفاده از این کد به پایان رسیده است. تاریخ پایان {title}: {endText}. این کد دیگر قابل استفاده نیست.",
                    StatusMessage: $"کد منقضی شده — پایان در {endText}");
            }

            var validDetail = window.EndUtc.HasValue
                ? $"{title} معتبر است و از تاریخ {startText} تا {FormatPersianDate(window.EndUtc.Value)} فعال است."
                : $"{title} معتبر است و از تاریخ {startText} فعال است (بدون تاریخ پایان).";

            return new ReferralProgramState(
                IsValid: true,
                IsExpired: false,
                IsNotStarted: false,
                InvalidReason: null,
                StatusMessage: validDetail);
        }

        public static ReferralProgramState Evaluate(ReferralProgram program, DateTime? nowUtc = null)
        {
            return Evaluate(program.IsActive, program.StartDate, program.EndDate, program.Title, nowUtc);
        }

        public static string FormatPersianDate(DateTime utc)
        {
            var tehran = TimeZoneInfo.ConvertTimeFromUtc(ToUtc(utc), TehranTimeZone);
            return
                $"{PersianCalendar.GetYear(tehran):0000}/{PersianCalendar.GetMonth(tehran):00}/{PersianCalendar.GetDayOfMonth(tehran):00}";
        }

        private static string FormatProgramTitle(string? programTitle)
        {
            return string.IsNullOrWhiteSpace(programTitle)
                ? "برنامه پاداش"
                : $"برنامه «{programTitle.Trim()}»";
        }

        private static DateTime ToUtc(DateTime value)
        {
            return value.Kind switch
            {
                DateTimeKind.Utc => value,
                DateTimeKind.Local => value.ToUniversalTime(),
                _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
            };
        }

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

    public readonly record struct ReferralProgramState(
        bool IsValid,
        bool IsExpired,
        bool IsNotStarted,
        string? InvalidReason,
        string StatusMessage);
}
