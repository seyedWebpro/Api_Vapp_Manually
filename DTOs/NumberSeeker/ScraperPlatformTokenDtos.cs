namespace Api_Vapp.DTOs.NumberSeeker
{
    /// <summary>پاسخ خام FastAPI برای توکن پلتفرم (snake_case)</summary>
    public class ScraperPlatformTokenStatusRaw
    {
        public string Platform { get; set; } = string.Empty;
        public bool Configured { get; set; }
        public bool Ready { get; set; }
        public bool? IsExpired { get; set; }
        public bool? RefreshExpired { get; set; }
        public string? ExpiresAt { get; set; }
        public int? DaysRemaining { get; set; }
        public string? MaskedToken { get; set; }
        public string AlertLevel { get; set; } = "none";
        public bool AutoRefreshSupported { get; set; }
        public List<ScraperTokenDetailRaw> Tokens { get; set; } = new();
    }

    public class ScraperTokenDetailRaw
    {
        public string Name { get; set; } = string.Empty;
        public bool Configured { get; set; }
        public string? Masked { get; set; }
        public string? ExpiresAt { get; set; }
        public bool? IsExpired { get; set; }
        public int? DaysRemaining { get; set; }
    }

    public class ScraperPlatformTokenListRaw
    {
        public Dictionary<string, ScraperPlatformTokenStatusRaw> Platforms { get; set; } = new();
    }

    public class ScraperTokenAlertRaw
    {
        public string Platform { get; set; } = string.Empty;
        public string Level { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }

    public class ScraperTokenAlertsRaw
    {
        public int Count { get; set; }
        public List<ScraperTokenAlertRaw> Alerts { get; set; } = new();
    }

    public class ScraperTokenSavedRaw
    {
        public string Message { get; set; } = string.Empty;
        public string Platform { get; set; } = string.Empty;
        public ScraperPlatformTokenStatusRaw? Status { get; set; }
    }

    public class ScraperTokenMaintenanceRaw
    {
        public List<string> Refreshed { get; set; } = new();
        public List<ScraperTokenAlertRaw> Alerts { get; set; } = new();
        public List<object>? Errors { get; set; }
        public Dictionary<string, ScraperPlatformTokenStatusRaw>? Platforms { get; set; }
        public string? RanAt { get; set; }
    }
}
