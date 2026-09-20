using Api_Vapp.Interfaces;

namespace Api_Vapp.Services.BackgroundServices
{
    public class ProfessionalCampaignBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ProfessionalCampaignBackgroundService> _logger;

        public ProfessionalCampaignBackgroundService(
            IServiceProvider serviceProvider,
            ILogger<ProfessionalCampaignBackgroundService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var service = scope.ServiceProvider.GetRequiredService<IProfessionalCampaignService>();
                    await service.ProcessDueStepsAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing professional campaign steps");
                }

                // دقت اجرا حداکثر حدود یک ثانیه است؛ claim اتمیک سرویس از ارسال دوباره جلوگیری می‌کند.
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
            }
        }
    }
}
