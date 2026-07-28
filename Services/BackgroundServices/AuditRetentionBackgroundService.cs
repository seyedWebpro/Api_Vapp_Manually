using Api_Vapp.Configuration;
using Api_Vapp.Constants;
using Api_Vapp.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Api_Vapp.Services.BackgroundServices
{
    /// <summary>
    /// پاکسازی دوره‌ای AdminAuditLogs قدیمی‌تر از RetentionDays (حداقل MinRetentionDays).
    /// </summary>
    public sealed class AuditRetentionBackgroundService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IOptionsMonitor<AuditOptions> _options;
        private readonly ILogger<AuditRetentionBackgroundService> _logger;

        public AuditRetentionBackgroundService(
            IServiceScopeFactory scopeFactory,
            IOptionsMonitor<AuditOptions> options,
            ILogger<AuditRetentionBackgroundService> logger)
        {
            _scopeFactory = scopeFactory;
            _options = options;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // تأخیر اولیه تا API بالا بیاید
            await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                var opts = _options.CurrentValue;
                try
                {
                    await RunRetentionAsync(opts, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Audit retention job failed");
                }

                var hours = Math.Clamp(opts.RetentionIntervalHours, 1, 168);
                await Task.Delay(TimeSpan.FromHours(hours), stoppingToken);
            }
        }

        private async Task RunRetentionAsync(AuditOptions opts, CancellationToken ct)
        {
            var retentionDays = Math.Max(opts.RetentionDays, opts.MinRetentionDays);
            if (retentionDays < opts.MinRetentionDays)
                retentionDays = opts.MinRetentionDays;

            var cutoff = DateTime.UtcNow.AddDays(-retentionDays);
            var batchSize = Math.Clamp(opts.RetentionBatchSize, 100, 20000);
            var totalDeleted = 0;

            _logger.LogInformation(
                "Audit retention started. CutoffUtc={Cutoff} RetentionDays={Days} BatchSize={Batch}",
                cutoff, retentionDays, batchSize);

            while (!ct.IsCancellationRequested)
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<Api_Context>();

                var ids = await db.AdminAuditLogs.AsNoTracking()
                    .Where(x => x.CreatedAt < cutoff)
                    .OrderBy(x => x.Id)
                    .Select(x => x.Id)
                    .Take(batchSize)
                    .ToListAsync(ct);

                if (ids.Count == 0)
                    break;

                var deleted = await db.AdminAuditLogs
                    .Where(x => ids.Contains(x.Id))
                    .ExecuteDeleteAsync(ct);

                totalDeleted += deleted;
                _logger.LogInformation("Audit retention batch deleted {Count} rows", deleted);

                if (deleted < batchSize)
                    break;
            }

            _logger.LogInformation("Audit retention finished. TotalDeleted={Total}", totalDeleted);
        }
    }

    /// <summary>
    /// هشدار اسپایک Auth.AdminLoginFailed در پنجره زمانی کوتاه.
    /// خروجی: Serilog Warning با قالب ثابت برای grep/آلارم مانیتورینگ.
    /// </summary>
    public sealed class AuditAdminLoginFailAlertBackgroundService : BackgroundService
    {
        public const string AlertLogTemplate =
            "AUDIT_ALERT type=AdminLoginFailSpike count={Count} threshold={Threshold} windowMinutes={WindowMinutes} fromUtc={FromUtc} toUtc={ToUtc}";

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IOptionsMonitor<AuditOptions> _options;
        private readonly ILogger<AuditAdminLoginFailAlertBackgroundService> _logger;
        private DateTime? _lastAlertAt;

        public AuditAdminLoginFailAlertBackgroundService(
            IServiceScopeFactory scopeFactory,
            IOptionsMonitor<AuditOptions> options,
            ILogger<AuditAdminLoginFailAlertBackgroundService> logger)
        {
            _scopeFactory = scopeFactory;
            _options = options;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                var opts = _options.CurrentValue;
                try
                {
                    if (opts.AdminLoginFailAlertEnabled)
                        await CheckAsync(opts, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Admin login fail alert check failed");
                }

                var minutes = Math.Clamp(opts.AdminLoginFailCheckIntervalMinutes, 1, 60);
                await Task.Delay(TimeSpan.FromMinutes(minutes), stoppingToken);
            }
        }

        private async Task CheckAsync(AuditOptions opts, CancellationToken ct)
        {
            var window = Math.Clamp(opts.AdminLoginFailWindowMinutes, 5, 120);
            var threshold = Math.Max(1, opts.AdminLoginFailThreshold);
            var toUtc = DateTime.UtcNow;
            var fromUtc = toUtc.AddMinutes(-window);

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<Api_Context>();

            var count = await db.AdminAuditLogs.AsNoTracking()
                .Where(x =>
                    x.Action == AuditActions.AdminLoginFailed
                    && x.CreatedAt >= fromUtc
                    && x.CreatedAt <= toUtc)
                .CountAsync(ct);

            if (count < threshold)
                return;

            // جلوگیری از اسپم آلرت هر چک — حداقل یک پنجره فاصله
            if (_lastAlertAt.HasValue && (toUtc - _lastAlertAt.Value).TotalMinutes < window)
                return;

            _lastAlertAt = toUtc;
            _logger.LogWarning(
                AlertLogTemplate,
                count,
                threshold,
                window,
                fromUtc,
                toUtc);
        }
    }
}
