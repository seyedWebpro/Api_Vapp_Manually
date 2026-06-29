namespace Api_Vapp.Constants
{
    /// <summary>
    /// ماژول‌های مبدأ ارسال پیامک — برای گزارش‌گیری یکپارچه
    /// </summary>
    public static class SmsSourceModules
    {
        public const string MessageCampaign = "MessageCampaign";
        public const string MessageDirect = "MessageDirect";
        public const string Cashback = "Cashback";
        public const string CashbackScheduled = "CashbackScheduled";
        public const string ReferralProgram = "ReferralProgram";
        public const string AutomatedMessage = "AutomatedMessage";
        public const string Manual = "Manual";

        public static readonly IReadOnlyDictionary<string, string> PersianLabels = new Dictionary<string, string>
        {
            [MessageCampaign] = "کمپین پیامکی",
            [MessageDirect] = "ارسال مستقیم پیام",
            [Cashback] = "کش‌بک",
            [CashbackScheduled] = "کش‌بک زمان‌بندی‌شده",
            [ReferralProgram] = "برنامه پاداش",
            [AutomatedMessage] = "پیام خودکار",
            [Manual] = "ارسال دستی"
        };

        public static string GetPersianLabel(string module) =>
            PersianLabels.TryGetValue(module, out var label) ? label : module;
    }
}
