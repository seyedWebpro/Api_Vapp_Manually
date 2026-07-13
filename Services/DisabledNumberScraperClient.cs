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
                Timestamp = DateTime.UtcNow.ToString("O")
            });
        }

        public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(false);
    }
}
