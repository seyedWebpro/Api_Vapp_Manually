namespace Api_Vapp.Constants
{
    public static class AppVersionPlatforms
    {
        public const string Android = "android";
        public const string Ios = "ios";

        public static readonly string[] All = [Android, Ios];

        public static bool IsValid(string? platform) =>
            !string.IsNullOrWhiteSpace(platform)
            && All.Contains(platform.Trim().ToLowerInvariant());

        public static string Normalize(string platform) =>
            platform.Trim().ToLowerInvariant();
    }
}
