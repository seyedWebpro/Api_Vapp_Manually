namespace Api_Vapp.Constants
{
    /// <summary>
    /// نوع آیتم‌های ارسال سریع که تأیید یک‌باره ادمین دارند.
    /// </summary>
    public static class QuickSendItemTypes
    {
        public const string BusinessCard = "BusinessCard";
        public const string BookingSystem = "BookingSystem";
        public const string UserForm = "UserForm";
        public const string LuckyWheel = "LuckyWheel";
        public const string SocialMediaLink = "SocialMediaLink";
        public const string QuickAction = "QuickAction";

        public static readonly string[] All =
        [
            BusinessCard,
            BookingSystem,
            UserForm,
            LuckyWheel,
            SocialMediaLink,
            QuickAction
        ];

        public static bool IsValid(string? itemType)
        {
            if (string.IsNullOrWhiteSpace(itemType))
                return false;

            return All.Any(t => string.Equals(t, itemType.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        public static string Normalize(string itemType) =>
            All.First(t => string.Equals(t, itemType.Trim(), StringComparison.OrdinalIgnoreCase));

        public static string ToPersian(string itemType) => itemType switch
        {
            BusinessCard => "کارت ویزیت",
            BookingSystem => "رزرو نوبت",
            UserForm => "فرم",
            LuckyWheel => "گردونه شانس",
            SocialMediaLink => "لینک شبکه اجتماعی",
            QuickAction => "اقدام سریع",
            _ => "ارسال سریع"
        };

        public static string ActionUrl(string itemType) => itemType switch
        {
            BusinessCard => "/business-card",
            BookingSystem => "/booking",
            UserForm => "/forms",
            LuckyWheel => "/lucky-wheel",
            SocialMediaLink => "/social-links",
            QuickAction => "/quick-actions",
            _ => "/"
        };
    }
}
