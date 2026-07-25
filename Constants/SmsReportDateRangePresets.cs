namespace Api_Vapp.Constants
{
    /// <summary>
    /// پیش‌فرض‌های بازه زمانی فیلتر گزارش پیامک
    /// </summary>
    public static class SmsReportDateRangePresets
    {
        public const string Last7Days = "Last7Days";
        public const string Last30Days = "Last30Days";
        public const string Last90Days = "Last90Days";
        public const string Custom = "Custom";

        public static readonly IReadOnlyDictionary<string, string> PersianLabels = new Dictionary<string, string>
        {
            [Last7Days] = "۷ روز گذشته",
            [Last30Days] = "۳۰ روز گذشته",
            [Last90Days] = "۹۰ روز گذشته",
            [Custom] = "بازه سفارشی"
        };

        public static bool IsValid(string? preset) =>
            string.IsNullOrWhiteSpace(preset) ||
            preset.Equals(Last7Days, StringComparison.OrdinalIgnoreCase) ||
            preset.Equals(Last30Days, StringComparison.OrdinalIgnoreCase) ||
            preset.Equals(Last90Days, StringComparison.OrdinalIgnoreCase) ||
            preset.Equals(Custom, StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// تبدیل preset به FromDate/ToDate بر اساس UTC.
        /// برای Custom، ToDate تا انتهای همان روز UTC گسترش می‌یابد.
        /// </summary>
        public static (DateTime? FromDate, DateTime? ToDate) Resolve(string? preset, DateTime? fromDate, DateTime? toDate)
        {
            if (!string.IsNullOrWhiteSpace(preset) &&
                !preset.Equals(Custom, StringComparison.OrdinalIgnoreCase))
            {
                var nowUtc = DateTime.UtcNow;
                var days = preset switch
                {
                    Last7Days => 7,
                    Last30Days => 30,
                    Last90Days => 90,
                    _ => 7 // ایمن: preset نامعتبر → ۷ روز (لایه سرویس باید قبلش validate کند)
                };

                return (nowUtc.AddDays(-days), nowUtc);
            }

            return (fromDate, NormalizeToDateEndOfDay(toDate));
        }

        /// <summary>
        /// اگر فقط تاریخ (بدون ساعت معنادار) آمده باشد، تا پایان همان روز UTC را شامل می‌کند.
        /// </summary>
        public static DateTime? NormalizeToDateEndOfDay(DateTime? toDate)
        {
            if (!toDate.HasValue)
                return null;

            var value = toDate.Value;
            if (value.TimeOfDay == TimeSpan.Zero)
                return value.Date.AddDays(1).AddTicks(-1);

            return value;
        }

        public static string GetPersianLabel(string preset) =>
            PersianLabels.TryGetValue(preset, out var label) ? label : preset;
    }
}
