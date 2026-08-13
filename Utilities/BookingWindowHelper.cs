using Api_Vapp.Models;

namespace Api_Vapp.Utilities
{
    /// <summary>
    /// محاسبه بازه مجاز رزرو عمومی (از امروز UTC به تعداد روز).
    /// </summary>
    public static class BookingWindowHelper
    {
        public const int MinDays = 1;
        public const int MaxDays = 365;
        public const int DefaultDays = 7;

        public static readonly int[] SuggestedWindowDays = { 7, 14, 30, 60, 90 };

        public static int ResolveEffectiveDays(int? configuredDays, int globalDefaultDays)
        {
            if (configuredDays.HasValue &&
                configuredDays.Value >= MinDays &&
                configuredDays.Value <= MaxDays)
            {
                return configuredDays.Value;
            }

            return globalDefaultDays > 0 ? globalDefaultDays : DefaultDays;
        }

        public static int ResolveEffectiveDays(BookingSystem system, BookingSystemOptions options) =>
            ResolveEffectiveDays(system.BookingWindowDays, options.PublicBookingWindowDays);

        public static DateOnly GetStartDateUtc() => DateOnly.FromDateTime(DateTime.UtcNow);

        public static DateOnly GetEndDateUtc(int effectiveWindowDays) =>
            GetStartDateUtc().AddDays(effectiveWindowDays);

        public static bool IsWithinWindow(DateOnly date, int effectiveWindowDays)
        {
            var startDate = GetStartDateUtc();
            var endDate = GetEndDateUtc(effectiveWindowDays);
            return date >= startDate && date <= endDate;
        }

        public static string BuildErrorMessage(int effectiveWindowDays) =>
            $"رزرو فقط تا {effectiveWindowDays} روز آینده امکان‌پذیر است";

        public static string? ValidateConfiguredDays(int? days)
        {
            if (!days.HasValue)
            {
                return null;
            }

            if (days.Value < MinDays || days.Value > MaxDays)
            {
                return $"بازه رزرو باید بین {MinDays} تا {MaxDays} روز باشد";
            }

            return null;
        }
    }
}
