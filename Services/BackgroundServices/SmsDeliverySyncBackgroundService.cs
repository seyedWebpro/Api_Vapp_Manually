using Api_Vapp.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Api_Vapp.Services.BackgroundServices
{
    /// <summary>
    /// بروزرسانی دوره‌ای وضعیت دلیوری پیامک‌ها از وب‌سرویس ایران‌نوین
    /// </summary>
    public class SmsDeliverySyncBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<SmsDeliverySyncBackgroundService> _logger;
        private readonly TimeSpan _checkInterval;
        private readonly int _minAgeBeforeFirstCheckMinutes;

        public SmsDeliverySyncBackgroundService(
            IServiceProvider serviceProvider,
            ILogger<SmsDeliverySyncBackgroundService> logger,
            IConfiguration configuration)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            var intervalMinutes = configuration.GetValue("Sms:DeliverySync:CheckIntervalMinutes", 15);
            _checkInterval = TimeSpan.FromMinutes(intervalMinutes);
            _minAgeBeforeFirstCheckMinutes = configuration.GetValue("Sms:DeliverySync:MinAgeBeforeFirstCheckMinutes", 60);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation(
                "=== SMS Delivery Sync Background Service started === StartedUtc: {StartedUtc:yyyy-MM-dd HH:mm:ss}, IntervalMinutes: {Interval}, MinAgeBeforeCheckMinutes: {MinAge}",
                DateTime.UtcNow, _checkInterval.TotalMinutes, _minAgeBeforeFirstCheckMinutes);

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                var cycleStartedUtc = DateTime.UtcNow;
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var trackingService = scope.ServiceProvider.GetRequiredService<ISmsDeliveryTrackingService>();
                    await trackingService.SyncPendingDeliveriesAsync(stoppingToken);

                    _logger.LogDebug(
                        "SMS delivery sync cycle finished — CycleStartedUtc: {StartedUtc:yyyy-MM-dd HH:mm:ss}, DurationMs: {DurationMs:F0}, NextRunUtc: {NextRunUtc:yyyy-MM-dd HH:mm:ss}",
                        cycleStartedUtc,
                        (DateTime.UtcNow - cycleStartedUtc).TotalMilliseconds,
                        DateTime.UtcNow.Add(_checkInterval));
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "SMS delivery sync cycle failed — CycleStartedUtc: {StartedUtc:yyyy-MM-dd HH:mm:ss}",
                        cycleStartedUtc);
                }

                await Task.Delay(_checkInterval, stoppingToken);
            }

            _logger.LogInformation(
                "=== SMS Delivery Sync Background Service stopped === StoppedUtc: {StoppedUtc:yyyy-MM-dd HH:mm:ss}",
                DateTime.UtcNow);
        }
    }
}
