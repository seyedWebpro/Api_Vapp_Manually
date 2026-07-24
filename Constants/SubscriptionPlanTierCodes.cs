namespace Api_Vapp.Constants
{
    public static class SubscriptionPlanTierCodes
    {
        public const string Free = "free";
        public const string Plus = "plus";
        public const string Gold = "gold";

        /// <summary>
        /// کدهای سیستمی — قابل تغییر نیستند؛ پلن رایگان قابل حذف/غیرفعال‌سازی نیست.
        /// </summary>
        public static readonly IReadOnlySet<string> SystemTiers = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Free,
            Plus,
            Gold
        };

        public static string Normalize(string? tierCode) =>
            (tierCode ?? string.Empty).Trim().ToLowerInvariant();

        public static bool IsSystemTier(string? tierCode)
        {
            var normalized = Normalize(tierCode);
            return normalized.Length > 0 && SystemTiers.Contains(normalized);
        }

        public static bool IsFree(string? tierCode) =>
            string.Equals(Normalize(tierCode), Free, StringComparison.Ordinal);
    }

    public sealed record DefaultSubscriptionPlanDefinition(
        string TierCode,
        string Name,
        string Description,
        decimal Price,
        int SortOrder,
        IReadOnlyList<string> FeatureCodes);

    /// <summary>
    /// پلن‌های پیش‌فرض سیستمی — نام/قیمت/امکانات قابل ویرایش؛ کد سطح قفل است.
    /// </summary>
    public static class SubscriptionPlanCatalog
    {
        private static readonly string[] FreeFeatures =
        [
            SubscriptionFeatureCodes.NumberSeeker,
            SubscriptionFeatureCodes.Phonebook,
            SubscriptionFeatureCodes.Messaging,
        ];

        private static readonly string[] PlusFeatures =
        [
            SubscriptionFeatureCodes.NumberSeeker,
            SubscriptionFeatureCodes.Phonebook,
            SubscriptionFeatureCodes.Messaging,
            SubscriptionFeatureCodes.FormBuilder,
            SubscriptionFeatureCodes.OnlineBooking,
        ];

        public static IReadOnlyList<DefaultSubscriptionPlanDefinition> DefaultPlans { get; } =
        [
            new(
                SubscriptionPlanTierCodes.Free,
                "پلن رایگان",
                "برای شروع و مدیریت اولیه مشتریان",
                0,
                1,
                FreeFeatures),
            new(
                SubscriptionPlanTierCodes.Plus,
                "پلن پلاس",
                "ابزارهای حرفه‌ای برای جذب و مدیریت مشتریان",
                355_000,
                2,
                PlusFeatures),
            new(
                SubscriptionPlanTierCodes.Gold,
                "پلن طلایی",
                "تمام امکانات پیشرفته برای رشد کسب‌وکار",
                550_000,
                3,
                SubscriptionFeatureCodes.All.ToList()),
        ];
    }
}
