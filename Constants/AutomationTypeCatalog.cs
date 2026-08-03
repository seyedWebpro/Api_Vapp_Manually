namespace Api_Vapp.Constants
{
    public sealed record AutomationTypeDefinitionSeed(
        string Code,
        string Name,
        string Description,
        string Icon,
        int SortOrder);

    /// <summary>
    /// کاتالوگ انواع پیام خودکار سیستمی — منبع حقیقت برای seed و پنل ادمین.
    /// </summary>
    public static class AutomationTypeCatalog
    {
        public static IReadOnlyList<AutomationTypeDefinitionSeed> All { get; } =
        [
            new(AutomationTypeCodes.Birthday, "تبریک تولد", "ارسال پیام خودکار در روز تولد مشتریان", "🎂", 1),
            new(AutomationTypeCodes.CashbackExpiry, "یادآوری انقضای کش بک", "۲ روز قبل از پایان اعتبار کش بک برای مشتری پیام ارسال می‌شود", "💰", 2),
            new(AutomationTypeCodes.Welcome, "پیام خوش آمدگویی", "پس از اولین ثبت شماره مشتری، پیام خوش آمدگویی ارسال می‌شود", "👋", 3),
            new(AutomationTypeCodes.PurchaseReminder, "یادآوری خرید", "اگر مشتری ۳۰ روز خرید نداشته باشد، پیام ارسال می‌شود", "🛒", 4),
            new(AutomationTypeCodes.SpecialOccasion, "مناسبت های خاص", "ارسال پیام در مناسبت‌های مخصوص سال", "🎉", 5),
            new(AutomationTypeCodes.Custom, "اتوماسیون سفارشی", "شرط، زمان و پیام را خودتان مشخص کنید", "⚡", 6),
        ];
    }
}
