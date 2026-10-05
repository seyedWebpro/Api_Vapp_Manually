namespace Api_Vapp.Utilities
{
    /// <summary>
    /// زمان‌بندی کمپین حرفه‌ای — همه زمان‌های ذخیره‌شده UTC هستند.
    /// ورودی کلاینت بدون offset صریح (مثل DateTime.toIso8601String موبایل) ساعت دیوار تهران فرض می‌شود.
    /// </summary>
    public static class ProfessionalCampaignSchedule
    {
        private static readonly TimeZoneInfo TehranTimeZone = ResolveTehranTimeZone();

        public static DateTime ToUtc(DateTimeOffset value)
        {
            // System.Text.Json برای "2026-10-05T14:56:00" بدون Z/offset → Offset=00:00.
            // اپ ایران این را به‌عنوان ساعت محلی می‌فرستد؛ تفسیر به‌عنوان UTC باعث تأخیر ~۳٫۵ ساعته می‌شود.
            if (value.Offset == TimeSpan.Zero)
            {
                var tehranLocal = DateTime.SpecifyKind(value.DateTime, DateTimeKind.Unspecified);
                return TimeZoneInfo.ConvertTimeToUtc(tehranLocal, TehranTimeZone);
            }

            var utc = value.UtcDateTime;
            return utc.Kind == DateTimeKind.Utc
                ? utc
                : DateTime.SpecifyKind(utc, DateTimeKind.Utc);
        }

        public static IReadOnlyList<DateTime> BuildProjectedUtc(
            DateTime startUtc,
            IEnumerable<int> delaysAfterPreviousMinutes)
        {
            if (startUtc.Kind != DateTimeKind.Utc)
                throw new ArgumentException("زمان شروع باید UTC باشد", nameof(startUtc));

            var cursor = startUtc;
            var result = new List<DateTime>();
            foreach (var delay in delaysAfterPreviousMinutes)
            {
                if (delay < 0)
                    throw new ArgumentOutOfRangeException(nameof(delaysAfterPreviousMinutes));
                cursor = cursor.AddMinutes(delay);
                result.Add(cursor);
            }
            return result;
        }

        public static DateTime GetNextUtc(DateTime actualSentAtUtc, int delayAfterPreviousMinutes)
        {
            if (actualSentAtUtc.Kind != DateTimeKind.Utc)
                throw new ArgumentException("زمان ارسال باید UTC باشد", nameof(actualSentAtUtc));
            if (delayAfterPreviousMinutes < 0)
                throw new ArgumentOutOfRangeException(nameof(delayAfterPreviousMinutes));
            return actualSentAtUtc.AddMinutes(delayAfterPreviousMinutes);
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
}
