namespace Api_Vapp.Constants
{
    /// <summary>
    /// کدهای ثابت انواع پیام خودکار — منطق اجرا به این کدها وابسته است.
    /// </summary>
    public static class AutomationTypeCodes
    {
        public const string Birthday = "Birthday";
        public const string CashbackExpiry = "CashbackExpiry";
        public const string Welcome = "Welcome";
        public const string PurchaseReminder = "PurchaseReminder";
        public const string SpecialOccasion = "SpecialOccasion";
        public const string Custom = "Custom";

        public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Birthday,
            CashbackExpiry,
            Welcome,
            PurchaseReminder,
            SpecialOccasion,
            Custom
        };

        public static bool IsKnown(string code) =>
            !string.IsNullOrWhiteSpace(code) && All.Contains(code.Trim());
    }
}
