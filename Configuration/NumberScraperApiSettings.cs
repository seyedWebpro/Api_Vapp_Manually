namespace Api_Vapp.Configuration
{
    /// <summary>
    /// تنظیمات اتصال به سرویس Python Number Scraper (FastAPI).
    /// </summary>
    public class NumberScraperApiSettings
    {
        public const string SectionName = "NumberScraperApi";

        public bool Enabled { get; set; } = true;

        /// <summary>مثال: http://localhost:8000</summary>
        public string BaseUrl { get; set; } = "http://localhost:8000";

        /// <summary>کلید مشترک با API_KEY در .env ربات — هدر X-API-Key</summary>
        public string ApiKey { get; set; } = string.Empty;

        /// <summary>Timeout هر درخواست HTTP به ربات (ثانیه)</summary>
        public int TimeoutSeconds { get; set; } = 120;

        public RetryPolicySettings RetryPolicy { get; set; } = new();

        public CircuitBreakerSettings CircuitBreaker { get; set; } = new();
    }

    public class RetryPolicySettings
    {
        public int MaxRetries { get; set; } = 3;
        public int RetryDelayMs { get; set; } = 1000;
        public bool UseExponentialBackoff { get; set; } = true;
    }

    public class CircuitBreakerSettings
    {
        public int FailuresBeforeBreaking { get; set; } = 5;
        public int DurationOfBreakSeconds { get; set; } = 30;
    }
}
