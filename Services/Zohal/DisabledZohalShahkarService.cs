using Api_Vapp.Configuration;
using Api_Vapp.DTOs.Zohal;
using Api_Vapp.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Api_Vapp.Services.Zohal
{
    /// <summary>
    /// جایگزین وقتی Zohal:Enabled=false — در صورت SkipVerificationWhenDisabled=true، تطبیق رد نمی‌شود.
    /// </summary>
    internal sealed class DisabledZohalShahkarService : IZohalShahkarService
    {
        private readonly ZohalApiSettings _settings;
        private readonly ILogger<DisabledZohalShahkarService> _logger;

        public DisabledZohalShahkarService(
            IOptions<ZohalApiSettings> settings,
            ILogger<DisabledZohalShahkarService> logger)
        {
            _settings = settings.Value;
            _logger = logger;
        }

        public bool IsEnabled => false;

        public Task<ShahkarVerificationResult> VerifyAsync(
            string nationalCode,
            string mobile,
            ShahkarVerifyContext? context = null,
            CancellationToken cancellationToken = default)
        {
            if (_settings.SkipVerificationWhenDisabled)
            {
                _logger.LogWarning(
                    "Zohal Shahkar is disabled — verification skipped for mobile ending {MobileSuffix}, Source={Source}",
                    mobile.Length >= 4 ? mobile[^4..] : "****",
                    context?.Source ?? "unknown");

                return Task.FromResult(ShahkarVerificationResult.Skipped());
            }

            _logger.LogWarning("Zohal Shahkar is disabled and SkipVerificationWhenDisabled=false");
            return Task.FromResult(ShahkarVerificationResult.ServiceUnavailable());
        }
    }
}
