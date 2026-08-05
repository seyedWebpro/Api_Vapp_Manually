namespace Api_Vapp.Constants
{
    /// <summary>
    /// کلیدهای ثابت اسلات بنرهای اپ — منطق نمایش در Flutter به این کلیدها وابسته است.
    /// </summary>
    public static class AppBannerKeys
    {
        /// <summary>بنر صفحه اصلی (جایگزین assets/example/banner_home.png)</summary>
        public const string Home = "home";

        /// <summary>بنر گردونه شانس در صفحه ابزارها</summary>
        public const string ToolsWheel = "tools_wheel";

        public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Home,
            ToolsWheel
        };

        public static bool IsKnown(string key) =>
            !string.IsNullOrWhiteSpace(key) && All.Contains(key.Trim());
    }

    /// <summary>انواع لینک بنر اپ.</summary>
    public static class AppBannerLinkTypes
    {
        public const string None = "none";
        public const string AppRoute = "app_route";
        public const string ExternalUrl = "external_url";

        public static bool IsValid(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            var normalized = value.Trim().ToLowerInvariant();
            return normalized is None or AppRoute or ExternalUrl;
        }
    }

    public sealed record AppBannerSeed(
        string Key,
        string Title,
        string Description,
        string LinkType,
        string? LinkUrl,
        int SortOrder);

    /// <summary>
    /// کاتالوگ بنرهای سیستمی اپ — منبع حقیقت برای seed.
    /// </summary>
    public static class AppBannerCatalog
    {
        public static IReadOnlyList<AppBannerSeed> All { get; } =
        [
            new(
                AppBannerKeys.Home,
                "بنر صفحه اصلی",
                "بنر پایین صفحه خانه — در صورت خالی بودن تصویر، اپ می‌تواند fallback محلی نشان دهد.",
                AppBannerLinkTypes.None,
                null,
                1),
            new(
                AppBannerKeys.ToolsWheel,
                "بنر گردونه شانس (ابزارها)",
                "بنر صفحه ابزارها که به ساخت گردونه شانس لینک می‌شود.",
                AppBannerLinkTypes.AppRoute,
                "/CreateWheelOfFortune",
                2),
        ];
    }

    public static class AppBannerCacheKeys
    {
        public const string ActiveList = "app_banners:active";
    }
}
