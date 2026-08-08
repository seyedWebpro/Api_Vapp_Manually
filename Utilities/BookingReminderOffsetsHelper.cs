using System.Text.Json;

namespace Api_Vapp.Utilities
{
    /// <summary>
    /// نرمال‌سازی و سریالایز لیست زمان‌های یادآوری نوبت (دقیقه قبل از StartUtc).
    /// </summary>
    public static class BookingReminderOffsetsHelper
    {
        public const int MinOffsetMinutes = 1;
        public const int MaxOffsetMinutes = 43200; // 30 روز
        public const int MaxOffsetsPerService = 4;
        public const int DefaultOffsetMinutes = 60;

        /// <summary>گزینه‌های پیشنهادی UI (الزامی نیست؛ بک‌اند هر مقدار معتبر ۱..۴۳۲۰۰ را می‌پذیرد)</summary>
        public static readonly int[] SuggestedOffsetsMinutes = [60, 120, 1440, 2880];

        public static List<int> Normalize(IEnumerable<int>? offsets, int? legacySingle = null)
        {
            var set = new SortedSet<int>();
            if (offsets != null)
            {
                foreach (var o in offsets)
                {
                    if (o >= MinOffsetMinutes && o <= MaxOffsetMinutes)
                    {
                        set.Add(o);
                    }
                }
            }

            if (set.Count == 0 &&
                legacySingle.HasValue &&
                legacySingle.Value >= MinOffsetMinutes &&
                legacySingle.Value <= MaxOffsetMinutes)
            {
                set.Add(legacySingle.Value);
            }

            if (set.Count == 0)
            {
                set.Add(DefaultOffsetMinutes);
            }

            return set.Take(MaxOffsetsPerService).ToList();
        }

        public static string ToJson(IReadOnlyList<int> offsets) =>
            JsonSerializer.Serialize(Normalize(offsets));

        public static List<int> FromJson(string? json, int legacyFallback = DefaultOffsetMinutes)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return Normalize(null, legacyFallback);
            }

            try
            {
                var parsed = JsonSerializer.Deserialize<List<int>>(json);
                return Normalize(parsed, legacyFallback);
            }
            catch (JsonException)
            {
                return Normalize(null, legacyFallback);
            }
        }

        public static int ResolveLegacySingle(IReadOnlyList<int> offsets) =>
            offsets.Count == 0 ? DefaultOffsetMinutes : offsets.Max();

        public static HashSet<int> ParseSentOffsets(string? csv)
        {
            var set = new HashSet<int>();
            if (string.IsNullOrWhiteSpace(csv))
            {
                return set;
            }

            foreach (var part in csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (int.TryParse(part, out var minutes) && minutes > 0)
                {
                    set.Add(minutes);
                }
            }

            return set;
        }

        public static string FormatSentOffsets(IEnumerable<int> sent) =>
            string.Join(",", sent.Distinct().OrderBy(x => x));

        public static string BuildMessage(
            string businessTitle,
            string serviceTitle,
            string startLocalFormatted) =>
            $"یادآوری نوبت\n" +
            $"{businessTitle}\n" +
            $"خدمت: {serviceTitle}\n" +
            $"زمان: {startLocalFormatted}";

        public static string BuildMessageTemplate() =>
            "یادآوری نوبت\n{businessTitle}\nخدمت: {serviceTitle}\nزمان: {startLocal}\nلغو11";
    }
}
