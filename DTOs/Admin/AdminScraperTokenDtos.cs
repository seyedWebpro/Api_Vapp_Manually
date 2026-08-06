using System.ComponentModel.DataAnnotations;

namespace Api_Vapp.DTOs.Admin
{
    public class AdminScraperTokenStatusDto
    {
        public string Platform { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public bool Configured { get; set; }
        public bool Ready { get; set; }
        public bool? IsExpired { get; set; }
        public bool? RefreshExpired { get; set; }
        public string? ExpiresAt { get; set; }
        public int? DaysRemaining { get; set; }
        public string? MaskedToken { get; set; }
        public string AlertLevel { get; set; } = "none";
        public bool AutoRefreshSupported { get; set; }
        public List<AdminScraperTokenDetailDto> Tokens { get; set; } = new();
    }

    public class AdminScraperTokenDetailDto
    {
        public string Name { get; set; } = string.Empty;
        public bool Configured { get; set; }
        public string? Masked { get; set; }
        public string? ExpiresAt { get; set; }
        public bool? IsExpired { get; set; }
        public int? DaysRemaining { get; set; }
    }

    public class AdminScraperTokenAlertDto
    {
        public string Platform { get; set; } = string.Empty;
        public string Level { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }

    public class AdminScraperTokensOverviewDto
    {
        public bool ScraperReachable { get; set; }
        public bool ScraperEnabled { get; set; }
        public List<AdminScraperTokenStatusDto> Platforms { get; set; } = new();
        public List<AdminScraperTokenAlertDto> Alerts { get; set; } = new();
        public string? Hint { get; set; }
    }

    public class SaveDivarTokenDto
    {
        [Required(ErrorMessage = "توکن دیوار الزامی است")]
        [MinLength(10, ErrorMessage = "توکن دیوار معتبر نیست")]
        public string Token { get; set; } = string.Empty;

        public string? RefreshToken { get; set; }

        public string? FrontToken { get; set; }
    }

    public class SaveSheypoorTokenDto
    {
        [Required(ErrorMessage = "توکن شیپور الزامی است")]
        [MinLength(10, ErrorMessage = "توکن شیپور معتبر نیست")]
        public string AccessToken { get; set; } = string.Empty;

        public string? RefreshToken { get; set; }
    }

    public class AdminScraperTokenSaveResultDto
    {
        public string Platform { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public AdminScraperTokenStatusDto? Status { get; set; }
    }

    public class AdminScraperTokenMaintenanceDto
    {
        public List<string> Refreshed { get; set; } = new();
        public List<AdminScraperTokenAlertDto> Alerts { get; set; } = new();
        public List<AdminScraperTokenStatusDto> Platforms { get; set; } = new();
        public string Message { get; set; } = string.Empty;
    }
}
