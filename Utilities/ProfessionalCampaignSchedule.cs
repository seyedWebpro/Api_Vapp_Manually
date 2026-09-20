namespace Api_Vapp.Utilities
{
    public static class ProfessionalCampaignSchedule
    {
        public static DateTime ToUtc(DateTimeOffset value) => value.UtcDateTime;

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
    }
}
