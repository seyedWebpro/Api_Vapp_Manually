using Api_Vapp.DTOs.NumberSeeker;
using Api_Vapp.Interfaces;
using Microsoft.Extensions.Logging;

namespace Api_Vapp.Services
{
    /// <summary>
    /// جایگزین وقتی NumberScraperApi:Enabled=false
    /// </summary>
    internal sealed class DisabledNumberScraperClient : INumberScraperClient
    {
        private readonly ILogger<DisabledNumberScraperClient> _logger;

        public DisabledNumberScraperClient(ILogger<DisabledNumberScraperClient> logger)
        {
            _logger = logger;
        }

        public bool IsEnabled => false;

        public Task<NumberSeekerTaskCreatedDto> StartScrapeAsync(
            StartNumberSeekerScrapeDto request,
            CancellationToken cancellationToken = default)
        {
            _logger.LogWarning("Number scraper client is disabled");
            throw new InvalidOperationException("سرویس شماره‌جو غیرفعال است.");
        }

        public Task<NumberSeekerTaskStatusDto> GetTaskStatusAsync(
            string taskId,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("سرویس شماره‌جو غیرفعال است.");

        public Task<NumberSeekerCancelResultDto> CancelTaskAsync(
            string taskId,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("سرویس شماره‌جو غیرفعال است.");

        public Task<NumberSeekerHealthDto> GetHealthAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new NumberSeekerHealthDto
            {
                Status = "disabled",
                ScraperReachable = false,
                ApiKeyValid = false,
                IntegrationReady = false,
                Timestamp = DateTime.UtcNow.ToString("O")
            });
        }

        public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<ScraperPlatformTokenListRaw> GetPlatformTokensAsync(
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("SCRAPER_DISABLED");

        public Task<ScraperTokenAlertsRaw> GetPlatformTokenAlertsAsync(
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("SCRAPER_DISABLED");

        public Task<ScraperTokenSavedRaw> SaveDivarTokenAsync(
            string token,
            string? refreshToken,
            string? frontToken,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("SCRAPER_DISABLED");

        public Task<ScraperTokenSavedRaw> SaveSheypoorTokenAsync(
            string accessToken,
            string? refreshToken,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("SCRAPER_DISABLED");

        public Task<ScraperTokenMaintenanceRaw> RunTokenMaintenanceAsync(
            bool forceSheypoorRefresh = false,
            bool forceDivarRefresh = false,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("SCRAPER_DISABLED");
    }
}
