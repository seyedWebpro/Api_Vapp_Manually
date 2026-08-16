namespace Api_Vapp.Configuration
{
    /// <summary>
    /// بارگذاری توکن زحل از env / appsettings با اولویت استاندارد.
    /// </summary>
    public static class ZohalApiTokenConfiguration
    {
        public static void Apply(IConfiguration configuration, ZohalApiSettings settings)
        {
            if (!string.IsNullOrWhiteSpace(settings.ApiToken))
                return;

            var fromEnv = Environment.GetEnvironmentVariable("ZOHAL_API_TOKEN")
                ?? Environment.GetEnvironmentVariable("Zohal__ApiToken");

            if (!string.IsNullOrWhiteSpace(fromEnv))
            {
                settings.ApiToken = fromEnv.Trim();
            }
        }
    }
}
