using Api_Vapp.Constants;
using Api_Vapp.Data;
using Api_Vapp.Interfaces;
using Api_Vapp.Utilities;
using Microsoft.EntityFrameworkCore;

namespace Api_Vapp.Services.BackgroundServices
{
    /// <summary>
    /// خلاصه مالی روزانه — هر روز ساعت ۰۶:۳۰ UTC (۱۰:۰۰ تهران)
    /// </summary>
    public class FinancialReportPushBackgroundService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<FinancialReportPushBackgroundService> _logger;
        private DateTime? _lastRunDateUtc;

        public FinancialReportPushBackgroundService(
            IServiceScopeFactory scopeFactory,
            ILogger<FinancialReportPushBackgroundService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Financial Report Push Background Service started");
            await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken).ConfigureAwait(false);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var now = DateTime.UtcNow;
                    var today = now.Date;
                    var targetHour = 6;
                    var targetMinute = 30;

                    if (_lastRunDateUtc != today &&
                        (now.Hour > targetHour || (now.Hour == targetHour && now.Minute >= targetMinute)))
                    {
                        await RunDailyReportAsync(today, stoppingToken);
                        _lastRunDateUtc = today;
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "خطا در ارسال خلاصه مالی روزانه");
                }

                await Task.Delay(TimeSpan.FromMinutes(15), stoppingToken);
            }

            _logger.LogInformation("Financial Report Push Background Service stopped");
        }

        private async Task RunDailyReportAsync(DateTime todayUtc, CancellationToken cancellationToken)
        {
            var dayStart = todayUtc;
            var dayEnd = todayUtc.AddDays(1);

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<Api_Context>();
            var push = scope.ServiceProvider.GetRequiredService<IUserPushNotifier>();

            var eligibleUserIds = await db.UserNotificationSettings
                .AsNoTracking()
                .Where(s => s.PushEnabled && s.FinancialReport)
                .Select(s => s.UserId)
                .Distinct()
                .ToListAsync(cancellationToken);

            if (eligibleUserIds.Count == 0)
            {
                _logger.LogInformation("خلاصه مالی روزانه — کاربری با FinancialReport فعال نیست");
                return;
            }

            // فقط کسانی که دستگاه فعال دارند
            var withDevice = await db.UserDevices
                .AsNoTracking()
                .Where(d => d.IsActive && !d.IsDeleted && eligibleUserIds.Contains(d.UserId))
                .Select(d => d.UserId)
                .Distinct()
                .ToListAsync(cancellationToken);

            _logger.LogInformation(
                "خلاصه مالی روزانه — Eligible={Eligible}, WithDevice={WithDevice}",
                eligibleUserIds.Count, withDevice.Count);

            foreach (var userId in withDevice)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var balance = await db.Users
                    .AsNoTracking()
                    .Where(u => u.Id == userId && !u.IsDeleted)
                    .Select(u => u.WalletBalance)
                    .FirstOrDefaultAsync(cancellationToken);

                var dayTx = await db.WalletTransactions
                    .AsNoTracking()
                    .Where(t => t.UserId == userId
                                && t.CreatedAt >= dayStart
                                && t.CreatedAt < dayEnd
                                && t.Status == "Completed")
                    .Select(t => t.Amount)
                    .ToListAsync(cancellationToken);

                var credited = dayTx.Where(a => a > 0).Sum();
                var debited = dayTx.Where(a => a < 0).Sum(a => Math.Abs(a));
                var count = dayTx.Count;

                // اگر هیچ تراکنشی نبود، نوتیف نفرست (شلوغی کم)
                if (count == 0)
                    continue;

                var copy = PushNotificationCopy.FinancialDailyReport(balance, credited, debited, count);
                await push.NotifyAsync(
                    userId,
                    NotificationCategory.FinancialReport,
                    copy.Title,
                    copy.Body,
                    cancellationToken);
            }
        }
    }
}
