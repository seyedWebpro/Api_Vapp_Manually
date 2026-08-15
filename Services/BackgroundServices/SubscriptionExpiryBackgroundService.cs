using Api_Vapp.Constants;
using Api_Vapp.Data;
using Api_Vapp.Interfaces;
using Api_Vapp.Services.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Api_Vapp.Services.BackgroundServices
{
    /// <summary>
    /// اشتراک‌های Active که ExpiresAt گذشته را به Expired تبدیل و در AdminAuditLogs ثبت می‌کند.
    /// </summary>
    public sealed class SubscriptionExpiryBackgroundService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<SubscriptionExpiryBackgroundService> _logger;
        private static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);
        private static readonly TimeSpan StartupDelay = TimeSpan.FromMinutes(1);

        public SubscriptionExpiryBackgroundService(
            IServiceScopeFactory scopeFactory,
            ILogger<SubscriptionExpiryBackgroundService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.Delay(StartupDelay, stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ExpireDueSubscriptionsAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Subscription expiry job failed");
                }

                await Task.Delay(Interval, stoppingToken);
            }
        }

        private async Task ExpireDueSubscriptionsAsync(CancellationToken ct)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<Api_Context>();
            var audit = scope.ServiceProvider.GetRequiredService<IAuditService>();

            var now = DateTime.UtcNow;
            var due = await db.UserSubscriptions
                .AsTracking()
                .Where(us => !us.IsDeleted
                    && us.Status == "Active"
                    && us.ExpiresAt <= now)
                .OrderBy(us => us.Id)
                .Take(200)
                .ToListAsync(ct);

            if (due.Count == 0)
                return;

            foreach (var sub in due)
            {
                sub.Status = "Expired";
                sub.UpdatedAt = now;
            }

            await db.SaveChangesAsync(ct);

            var entries = due.Select(sub => new AuditEntry
            {
                Category = AuditCategories.Subscription,
                Action = AuditActions.SubscriptionExpired,
                EntityType = AuditEntityTypes.UserSubscription,
                EntityId = sub.Id.ToString(),
                ActorUserId = null,
                TargetUserId = sub.UserId,
                Source = "Background",
                After = new
                {
                    userId = sub.UserId,
                    subscriptionPlanId = sub.SubscriptionPlanId,
                    expiresAt = sub.ExpiresAt,
                    status = "Expired"
                }
            }).ToList();

            await audit.WriteRangeAsync(entries, ct);

            _logger.LogInformation(
                "Marked {Count} user subscriptions as Expired (batch)",
                due.Count);
        }
    }
}
