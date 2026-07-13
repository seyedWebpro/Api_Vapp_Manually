namespace Api_Vapp.Configuration
{
    /// <summary>
    /// بارگذاری API Key ربات از env / appsettings با اولویت استاندارد.
    /// </summary>
    public static class NumberScraperApiKeyConfiguration
    {
        public static void Apply(IConfiguration configuration, NumberScraperApiSettings settings)
        {
            if (!string.IsNullOrWhiteSpace(settings.ApiKey))
                return;

            var fromEnv = Environment.GetEnvironmentVariable("NUMBER_SCRAPER_API_KEY")
                ?? Environment.GetEnvironmentVariable("NumberScraperApi__ApiKey");

            if (!string.IsNullOrWhiteSpace(fromEnv))
            {
                settings.ApiKey = fromEnv.Trim();
            }
        }
    }
}
