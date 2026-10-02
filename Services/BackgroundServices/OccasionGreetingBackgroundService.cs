using Api_Vapp.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Api_Vapp.Services.BackgroundServices
{
    /// <summary>
    /// ارسال تبریک/تسلیت مناسبتی از جدول مناسبت‌های کاربر — هر ۱ دقیقه
    /// </summary>
    public class OccasionGreetingBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<OccasionGreetingBackgroundService> _logger;
        private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(1);

        public OccasionGreetingBackgroundService(
            IServiceProvider serviceProvider,
            ILogger<OccasionGreetingBackgroundService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Occasion Greeting Background Service started");
            await Task.Delay(TimeSpan.FromSeconds(40), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var dispatcher = scope.ServiceProvider.GetRequiredService<IOccasionGreetingDispatchService>();
                    await dispatcher.DispatchDueGreetingsAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error dispatching occasion greetings");
                }

                await Task.Delay(_checkInterval, stoppingToken);
            }

            _logger.LogInformation("Occasion Greeting Background Service stopped");
        }
    }
}
