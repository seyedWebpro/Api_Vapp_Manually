using System.Globalization;

namespace Api_Vapp.Utilities
{
    /// <summary>
    /// نگاشت وضعیت/منبع/زمان برای UI موبایل شماره‌جو.
    /// </summary>
    public static class NumberSeekerUiMapper
    {
        public const int PhonesPreviewLimit = 20;

        public static string GetSourceDisplayName(string? source) =>
            (source ?? string.Empty).Trim().ToLowerInvariant() switch
            {
                "divar" => "دیوار",
                "sheypoor" => "شیپور",
                "nshan" => "نشان",
                "balad" => "بلد",
                "googlemaps" => "گوگل مپ",
                _ => source?.Trim() ?? string.Empty
            };

        public static string GetStatusDisplayName(string? status) =>
            (status ?? string.Empty).Trim().ToLowerInvariant() switch
            {
                "completed" => "تکمیل شد",
                "partial" => "ناقص",
                "failed" => "ناموفق",
                "cancelled" => "لغو شد",
                "running" => "در حال جستجو",
                "pending" => "در صف",
                _ => status?.Trim() ?? string.Empty
            };

        /// <summary>success | warning | danger | info — برای رنگ badge موبایل</summary>
        public static string GetStatusTone(string? status) =>
            (status ?? string.Empty).Trim().ToLowerInvariant() switch
            {
                "completed" => "success",
                "partial" => "warning",
                "failed" or "cancelled" => "danger",
                _ => "info"
            };

        public static bool IsTerminal(string? status)
        {
            var s = (status ?? string.Empty).Trim().ToLowerInvariant();
            return s is "completed" or "partial" or "failed" or "cancelled";
        }

        public static bool IsImportable(string? status)
        {
            var s = (status ?? string.Empty).Trim().ToLowerInvariant();
            return s is "completed" or "partial";
        }

        public static string BuildSubtitle(string? city, string? category)
        {
            var c = city?.Trim() ?? string.Empty;
            var cat = category?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(c)) return cat;
            if (string.IsNullOrEmpty(cat)) return c;
            return $"{c} - {cat}";
        }

        public static string ToPersianDate(DateTime utcOrLocal)
        {
            var dt = utcOrLocal.Kind == DateTimeKind.Utc
                ? utcOrLocal.ToLocalTime()
                : utcOrLocal;
            var pc = new PersianCalendar();
            return $"{pc.GetYear(dt):0000}/{pc.GetMonth(dt):00}/{pc.GetDayOfMonth(dt):00}";
        }

        public static string BuildProgressLabel(int currentCount, int targetCount) =>
            $"{currentCount} از {targetCount} شماره";

        public static string BuildResultTitle(string? status) =>
            (status ?? string.Empty).Trim().ToLowerInvariant() switch
            {
                "completed" => "جست و جو با موفقیت تکمیل شد",
                "partial" => "جست و جو ناقص تمام شد",
                "failed" => "جست و جو ناموفق بود",
                "cancelled" => "جست و جو لغو شد",
                "running" => "در حال جست و جو",
                "pending" => "در صف انتظار",
                _ => "وضعیت جست و جو"
            };

        public static string BuildResultCountLabel(int count) =>
            $"{count} شماره یافت شد";

        public static List<string> TakePreview(IReadOnlyList<string>? phones, int limit = PhonesPreviewLimit)
        {
            if (phones == null || phones.Count == 0)
                return new List<string>();
            return phones.Take(Math.Max(1, limit)).ToList();
        }

        public static (int? Seconds, string? Text) EstimateRemaining(
            string? status,
            int currentCount,
            int targetCount,
            double? elapsedSeconds,
            int? queuePosition)
        {
            if (IsTerminal(status))
                return (0, null);

            if (queuePosition is > 0)
            {
                var queuedSeconds = queuePosition.Value * 45;
                return (queuedSeconds, FormatRemainingText(queuedSeconds));
            }

            if (currentCount <= 0 || elapsedSeconds is null or <= 0 || targetCount <= currentCount)
            {
                var fallback = Math.Max(30, (targetCount - Math.Max(0, currentCount)) * 3);
                return (fallback, FormatRemainingText(fallback));
            }

            var rate = currentCount / elapsedSeconds.Value;
            if (rate <= 0.0001)
                return (null, null);

            var remainingCount = targetCount - currentCount;
            var seconds = (int)Math.Ceiling(remainingCount / rate);
            seconds = Math.Clamp(seconds, 5, 3600);
            return (seconds, FormatRemainingText(seconds));
        }

        public static string FormatRemainingText(int seconds)
        {
            if (seconds < 60)
                return $"حدود {seconds} ثانیه دیگر";

            var minutes = (int)Math.Ceiling(seconds / 60.0);
            if (minutes < 60)
                return $"حدود {minutes} دقیقه دیگر";

            var hours = (int)Math.Ceiling(minutes / 60.0);
            return $"حدود {hours} ساعت دیگر";
        }
    }
}
