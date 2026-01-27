using Api_Vapp.Data;
using Api_Vapp.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Api_Vapp.Services.BackgroundServices
{
    /// <summary>
    /// Background Service برای ارسال خودکار کمپین‌های زمان‌بندی شده
    /// هر 5 دقیقه یکبار کمپین‌های زمان‌بندی شده را بررسی و ارسال می‌کند
    /// </summary>
    public class ScheduledCampaignBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ScheduledCampaignBackgroundService> _logger;
        private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(5); // هر 5 دقیقه یکبار

        public ScheduledCampaignBackgroundService(
            IServiceProvider serviceProvider,
            ILogger<ScheduledCampaignBackgroundService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Scheduled Campaign Background Service started");

            // تأخیر اولیه برای اطمینان از آماده بودن سیستم
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessScheduledCampaignsAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing scheduled campaigns");
                }

                // انتظار تا چک بعدی
                await Task.Delay(_checkInterval, stoppingToken);
            }

            _logger.LogInformation("Scheduled Campaign Background Service stopped");
        }

        private async Task ProcessScheduledCampaignsAsync(CancellationToken cancellationToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<Api_Context>();
            var messageService = scope.ServiceProvider.GetRequiredService<IMessageService>();

            var now = DateTime.UtcNow;

            // دریافت تمام کمپین‌هایی که باید ارسال شوند (فقط خواندنی - بدون Tracking)
            var scheduledCampaigns = await context.MessageCampaigns
                .AsNoTracking()
                .Where(c => !c.IsDeleted
                    && c.IsActive
                    && c.SendType == "Scheduled"
                    && c.Status == "Pending"
                    && c.ScheduledAt.HasValue
                    && c.ScheduledAt.Value <= now)
                .Select(c => new
                {
                    c.Id,
                    c.UserId,
                    c.ScheduledAt
                })
                .ToListAsync(cancellationToken);

            _logger.LogInformation("Found {Count} scheduled campaigns to send", scheduledCampaigns.Count);

            foreach (var c in scheduledCampaigns)
            {
                try
                {
                    _logger.LogInformation("Processing scheduled campaign {CampaignId} for user {UserId}", 
                        c.Id, c.UserId);

                    var result = await messageService.ConfirmAndSendCampaignAsync(c.Id, c.UserId);
                    
                    if (result.Success)
                    {
                        _logger.LogInformation("Scheduled campaign {CampaignId} sent successfully", c.Id);
                    }
                    else
                    {
                        _logger.LogWarning("Failed to send scheduled campaign {CampaignId}: {Message}", 
                            c.Id, result.Message);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error sending scheduled campaign {CampaignId} for user {UserId}", 
                        c.Id, c.UserId);
                }
            }
        }
    }
}



