using System.Text.Json;
using Api_Vapp.Constants;
using Api_Vapp.Models;

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

        public static HashSet<int> ResolveSentOffsets(
            string? csv,
            DateTime? reminderSentAt,
            IReadOnlyList<int> offsets)
        {
            var sent = ParseSentOffsets(csv);
            if (sent.Count == 0 &&
                reminderSentAt.HasValue &&
                string.IsNullOrWhiteSpace(csv))
            {
                foreach (var offset in offsets)
                {
                    sent.Add(offset);
                }
            }

            return sent;
        }

        /// <summary>
        /// محاسبه می‌کند آیا جاب یادآوری هنوز برای این نوبت SMS می‌فرستد (همان قوانین ProcessReminders).
        /// </summary>
        public static BookingReminderSchedule BuildSchedule(
            DateTime startUtc,
            DateTime nowUtc,
            bool remindersEnabled,
            string? status,
            IReadOnlyList<int> offsets,
            IReadOnlySet<int> alreadySent)
        {
            var offsetList = offsets.Count == 0 ? Normalize(null) : offsets.ToList();
            var pending = offsetList.Where(o => !alreadySent.Contains(o)).OrderBy(o => o).ToList();

            if (!remindersEnabled)
            {
                return Skip(
                    remindersEnabled: false,
                    offsetList,
                    pending,
                    BookingReminderSkipReasons.Disabled,
                    "ارسال پیامک یادآوری برای این نوبت غیرفعال است");
            }

            if (string.Equals(status, BookingAppointmentStatuses.Cancelled, StringComparison.Ordinal))
            {
                return Skip(
                    remindersEnabled: true,
                    offsetList,
                    pending,
                    BookingReminderSkipReasons.Cancelled,
                    "نوبت لغو شده است و پیامک یادآوری ارسال نمی‌شود");
            }

            if (startUtc <= nowUtc)
            {
                return Skip(
                    remindersEnabled: true,
                    offsetList,
                    pending,
                    BookingReminderSkipReasons.Past,
                    "زمان نوبت گذشته است و پیامک یادآوری ارسال نمی‌شود");
            }

            if (!string.Equals(status, BookingAppointmentStatuses.Confirmed, StringComparison.Ordinal))
            {
                return Skip(
                    remindersEnabled: true,
                    offsetList,
                    pending,
                    BookingReminderSkipReasons.NotConfirmed,
                    "تا تأیید نوبت، پیامک یادآوری ارسال نمی‌شود");
            }

            if (pending.Count == 0)
            {
                return Skip(
                    remindersEnabled: true,
                    offsetList,
                    pending,
                    BookingReminderSkipReasons.AlreadySent,
                    "پیامک یادآوری این نوبت قبلاً ارسال شده است");
            }

            DateTime? nextAt = null;
            foreach (var offset in pending)
            {
                var sendAt = startUtc.AddMinutes(-offset);
                if (sendAt < nowUtc)
                {
                    sendAt = nowUtc;
                }

                if (!nextAt.HasValue || sendAt < nextAt.Value)
                {
                    nextAt = sendAt;
                }
            }

            return new BookingReminderSchedule(
                RemindersEnabled: true,
                WillSend: true,
                OffsetsMinutes: offsetList,
                PendingOffsetsMinutes: pending,
                NextReminderAtUtc: nextAt,
                SkipReasonCode: null,
                SkipReason: null);
        }

        private static BookingReminderSchedule Skip(
            bool remindersEnabled,
            IReadOnlyList<int> offsets,
            IReadOnlyList<int> pending,
            string code,
            string message) =>
            new(
                remindersEnabled,
                false,
                offsets,
                pending,
                null,
                code,
                message);
    }

    public sealed record BookingReminderSchedule(
        bool RemindersEnabled,
        bool WillSend,
        IReadOnlyList<int> OffsetsMinutes,
        IReadOnlyList<int> PendingOffsetsMinutes,
        DateTime? NextReminderAtUtc,
        string? SkipReasonCode,
        string? SkipReason);
}
