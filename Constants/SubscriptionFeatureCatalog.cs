namespace Api_Vapp.Constants
{
    public sealed record SubscriptionFeatureDefinition(
        string Code,
        string Name,
        string Description,
        int SortOrder);

    /// <summary>
    /// کاتالوگ امکانات سیستمی — منبع حقیقت برای seed و پنل ادمین.
    /// </summary>
    public static class SubscriptionFeatureCatalog
    {
        public static IReadOnlyList<SubscriptionFeatureDefinition> All { get; } =
        [
            new(SubscriptionFeatureCodes.NumberSeeker, "شماره‌جو", "جستجو و یافتن شماره تماس", 1),
            new(SubscriptionFeatureCodes.Phonebook, "دفترچه تلفن", "مدیریت مخاطبین و گروه‌ها", 2),
            new(SubscriptionFeatureCodes.Messaging, "ارسال پیام", "ارسال پیامک به مشتریان", 3),
            new(SubscriptionFeatureCodes.FormBuilder, "فرم‌ساز", "ساخت فرم‌های سفارشی برای جذب مشتری", 4),
            new(SubscriptionFeatureCodes.OnlineBooking, "رزرو آنلاین", "رزرو آنلاین خدمات توسط مشتریان", 5),
            new(SubscriptionFeatureCodes.FreeQuickSend, "ارسال سریع رایگان", "ارسال پیام سریع بدون هزینه اضافه", 6),
            new(SubscriptionFeatureCodes.BusinessCard, "کارت ویزیت", "ساخت و مدیریت کارت ویزیت دیجیتال", 7),
            new(SubscriptionFeatureCodes.MessageAutomation, "اتوماسیون پیام", "پیام‌های خودکار بر اساس رویداد و مناسبت", 8),
            new(SubscriptionFeatureCodes.BulkCampaign, "کمپین پیام انبوه", "ارسال کمپین پیامک به گروه‌های مخاطب", 9),
            new(SubscriptionFeatureCodes.CashbackWallet, "کیف پول کش‌بک", "مدیریت و پرداخت کش‌بک به مشتریان", 10),
            new(SubscriptionFeatureCodes.PrioritySupport, "پشتیبانی اولویت‌دار", "پاسخگویی سریع‌تر تیم پشتیبانی", 11),
            new(SubscriptionFeatureCodes.AdvancedAnalytics, "گزارش‌گیری پیشرفته", "گزارش‌های تحلیلی از پیام‌ها و مشتریان", 12),
        ];
    }
}
