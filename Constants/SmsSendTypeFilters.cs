namespace Api_Vapp.Constants
{
    /// <summary>
    /// فیلتر نوع ارسال برای UI گزارش پیامک (مطابق مودال فیلترها)
    /// </summary>
    public static class SmsSendTypeFilters
    {
        public const string All = "All";
        public const string Campaign = "Campaign";
        public const string Cashback = "Cashback";
        public const string Reward = "Reward";

        public static readonly IReadOnlyDictionary<string, string> PersianLabels = new Dictionary<string, string>
        {
            [All] = "همه",
            [Campaign] = "کمپین پیامکی",
            [Cashback] = "کش‌بک",
            [Reward] = "پاداش"
        };

        public static bool IsValid(string? sendTypeFilter) =>
            string.IsNullOrWhiteSpace(sendTypeFilter) ||
            sendTypeFilter.Equals(All, StringComparison.OrdinalIgnoreCase) ||
            sendTypeFilter.Equals(Campaign, StringComparison.OrdinalIgnoreCase) ||
            sendTypeFilter.Equals(Cashback, StringComparison.OrdinalIgnoreCase) ||
            sendTypeFilter.Equals(Reward, StringComparison.OrdinalIgnoreCase);

        public static IReadOnlyList<string> ResolveSourceModules(string? sendTypeFilter)
        {
            if (string.IsNullOrWhiteSpace(sendTypeFilter) ||
                sendTypeFilter.Equals(All, StringComparison.OrdinalIgnoreCase))
            {
                return Array.Empty<string>();
            }

            if (sendTypeFilter.Equals(Campaign, StringComparison.OrdinalIgnoreCase))
            {
                return new[]
                {
                    SmsSourceModules.MessageCampaign,
                    SmsSourceModules.MessageDirect,
                    SmsSourceModules.AutomatedMessage
                };
            }

            if (sendTypeFilter.Equals(Cashback, StringComparison.OrdinalIgnoreCase))
            {
                return new[]
                {
                    SmsSourceModules.Cashback,
                    SmsSourceModules.CashbackScheduled
                };
            }

            if (sendTypeFilter.Equals(Reward, StringComparison.OrdinalIgnoreCase))
            {
                return new[] { SmsSourceModules.ReferralProgram };
            }

            return Array.Empty<string>();
        }

        public static string GetPersianLabel(string sendType) =>
            PersianLabels.TryGetValue(sendType, out var label) ? label : sendType;
    }
}
