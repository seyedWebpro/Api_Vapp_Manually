namespace Api_Vapp.Constants
{
    /// <summary>
    /// ماژول‌های قابل انتخاب هنگام ثبت تیکت پشتیبانی
    /// </summary>
    public static class TicketModules
    {
        public const string Subscription = "Subscription";
        public const string Messaging = "Messaging";
        public const string Phonebook = "Phonebook";
        public const string FormBuilder = "FormBuilder";
        public const string OnlineBooking = "OnlineBooking";
        public const string Cashback = "Cashback";
        public const string Payment = "Payment";
        public const string Account = "Account";
        public const string Other = "Other";

        public static readonly IReadOnlyDictionary<string, string> PersianLabels =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [Subscription] = "خرید اشتراک",
                [Messaging] = "پیامک و کمپین",
                [Phonebook] = "دفترچه مخاطبین",
                [FormBuilder] = "فرم‌ساز",
                [OnlineBooking] = "نوبت‌دهی آنلاین",
                [Cashback] = "کش‌بک",
                [Payment] = "پرداخت و کیف پول",
                [Account] = "حساب کاربری",
                [Other] = "سایر"
            };

        public static readonly IReadOnlyList<string> All = PersianLabels.Keys.ToList();

        public static bool IsKnown(string? module) =>
            !string.IsNullOrWhiteSpace(module) && PersianLabels.ContainsKey(module.Trim());

        public static string GetPersianLabel(string? module)
        {
            if (string.IsNullOrWhiteSpace(module))
                return string.Empty;

            return PersianLabels.TryGetValue(module.Trim(), out var label) ? label : module.Trim();
        }

        public static string Normalize(string? module)
        {
            if (string.IsNullOrWhiteSpace(module))
                return Other;

            var trimmed = module.Trim();
            var match = PersianLabels.Keys.FirstOrDefault(k =>
                k.Equals(trimmed, StringComparison.OrdinalIgnoreCase));

            return match ?? Other;
        }
    }
}
