using Api_Vapp.Data;
using Api_Vapp.Interfaces;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Api_Vapp.Services.Startup
{
    /// <summary>
    /// Shared readiness flag — /health is live as soon as Kestrel listens;
    /// /api/* that need DB should wait until <see cref="IsReady"/>.
    /// </summary>
    public sealed class DatabaseStartupState
    {
        private volatile bool _ready;
        private volatile string _status = "starting";
        private volatile string? _error;

        public bool IsReady => _ready;
        public string Status => _status;
        public string? Error => _error;

        public void MarkReady()
        {
            _error = null;
            _status = "ready";
            _ready = true;
        }

        public void MarkFailed(string message)
        {
            _error = message;
            _status = "failed";
            _ready = false;
        }

        public void MarkRunning(string phase)
        {
            _status = phase;
        }
    }

    /// <summary>
    /// Runs EnsureDb + Migrate + Seed AFTER the host starts accepting HTTP.
    /// This permanently fixes health=000 while migrate/seed runs or retries.
    /// StartAsync returns immediately so Kestrel can bind :8080.
    /// </summary>
    public sealed class DatabaseStartupHostedService : IHostedService
    {
        private readonly IServiceProvider _services;
        private readonly IHostApplicationLifetime _lifetime;
        private readonly DatabaseStartupState _state;
        private readonly ILogger<DatabaseStartupHostedService> _logger;
        private Task? _work;

        public DatabaseStartupHostedService(
            IServiceProvider services,
            IHostApplicationLifetime lifetime,
            DatabaseStartupState state,
            ILogger<DatabaseStartupHostedService> logger)
        {
            _services = services;
            _lifetime = lifetime;
            _state = state;
            _logger = logger;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            // IMPORTANT: do NOT await ApplicationStarted here — that deadlocks the host.
            // Return immediately so Kestrel can bind :8080; migrate runs on the thread pool.
            _work = Task.Run(() => RunAsync(_lifetime.ApplicationStopping), CancellationToken.None);
            return Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            if (_work != null)
            {
                try { await _work.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken); }
                catch { /* ignore */ }
            }
        }

        private async Task RunAsync(CancellationToken stoppingToken)
        {
            // Brief delay so Kestrel bind wins the race against first migrate attempt
            try { await Task.Delay(750, stoppingToken); }
            catch (OperationCanceledException) { return; }

            _logger.LogInformation("Database startup worker beginning (HTTP should already be listening)");
            _state.MarkRunning("migrating");

            Exception? lastError = null;
            for (var attempt = 1; attempt <= 12; attempt++)
            {
                if (stoppingToken.IsCancellationRequested)
                    return;

                try
                {
                    using var scope = _services.CreateScope();
                    var sp = scope.ServiceProvider;
                    var context = sp.GetRequiredService<Api_Context>();
                    var cs = context.Database.GetDbConnection().ConnectionString
                        ?? throw new InvalidOperationException("Database connection string is empty.");

                    EnsureSqlDatabaseExists(cs, _logger);
                    context.Database.SetCommandTimeout(600);

                    var pending = context.Database.GetPendingMigrations().ToList();
                    _logger.LogInformation(
                        "Pending migrations: {Count} (attempt {Attempt}/12)",
                        pending.Count, attempt);

                    context.Database.Migrate();
                    _logger.LogInformation("Migration completed successfully.");

                    try
                    {
                        await DatabaseSeeder.SeedAsync(context, _logger);
                        _logger.LogInformation("Database seed completed successfully.");
                    }
                    catch (Exception seedEx)
                    {
                        // Seed must never keep the API offline forever
                        _logger.LogError(seedEx, "Seed failed — API stays up; fix seed data and restart if needed");
                    }

                    try
                    {
                        var push = sp.GetRequiredService<IPushNotificationService>();
                        if (push.TryInitialize())
                            _logger.LogInformation("Firebase Admin SDK آماده است — Push فعال.");
                        else
                            _logger.LogWarning("Firebase Admin SDK آماده نیست — Push غیرفعال.");
                    }
                    catch (Exception pushEx)
                    {
                        _logger.LogError(pushEx, "Firebase init failed (non-fatal)");
                    }

                    _state.MarkReady();
                    _logger.LogInformation("Database startup ready");
                    return;
                }
                catch (Exception ex)
                {
                    lastError = ex;
                    _state.MarkRunning($"migrate-retry-{attempt}");
                    _logger.LogWarning(ex, "Database startup attempt {Attempt}/12 failed — retry in 5s", attempt);
                    try { await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken); }
                    catch (OperationCanceledException) { return; }
                }
            }

            var msg = lastError?.Message ?? "unknown";
            _state.MarkFailed(msg);
            _logger.LogError(lastError, "Database startup FAILED after retries — /health is up but AppVersion may 500: {Message}", msg);
        }

        private static void EnsureSqlDatabaseExists(string connectionString, ILogger logger)
        {
            var csb = new SqlConnectionStringBuilder(connectionString);
            var dbName = csb.InitialCatalog;
            if (string.IsNullOrWhiteSpace(dbName))
            {
                logger.LogWarning("Skip EnsureSqlDatabaseExists — Initial Catalog empty");
                return;
            }

            csb.InitialCatalog = "master";
            for (var attempt = 1; attempt <= 30; attempt++)
            {
                try
                {
                    using var conn = new SqlConnection(csb.ConnectionString);
                    conn.Open();
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText =
                        $"""
                         IF DB_ID(N'{dbName.Replace("'", "''", StringComparison.Ordinal)}') IS NULL
                         BEGIN
                             DECLARE @sql nvarchar(max) = N'CREATE DATABASE [{dbName.Replace("]", "]]", StringComparison.Ordinal)}]';
                             EXEC (@sql);
                         END
                         """;
                    cmd.ExecuteNonQuery();
                    logger.LogInformation("Database {Db} ensured (attempt {Attempt})", dbName, attempt);
                    return;
                }
                catch (Exception ex) when (attempt < 30)
                {
                    logger.LogWarning(ex, "EnsureSqlDatabaseExists attempt {Attempt}/30 failed", attempt);
                    Thread.Sleep(2000);
                }
            }

            throw new InvalidOperationException($"Could not ensure SQL database '{dbName}' after 30 attempts.");
        }
    }
}
