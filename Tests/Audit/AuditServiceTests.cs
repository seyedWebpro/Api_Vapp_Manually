using Api_Vapp.Constants;
using Api_Vapp.Data;
using Api_Vapp.DTOs.Audit;
using Api_Vapp.Models;
using Api_Vapp.Services.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Api_Vapp.Tests.Audit;

public class AuditServiceTests
{
    [Fact]
    public async Task WriteAsync_Persists_BeforeAfter_And_ContextFields()
    {
        await using var harness = await AuditTestHarness.CreateAsync();

        await harness.AuditService.WriteAsync(new AuditEntry
        {
            Category = AuditCategories.Subscription,
            Action = AuditActions.SubscriptionPlanPriceUpdated,
            EntityType = AuditEntityTypes.SubscriptionPlan,
            EntityId = "42",
            ActorUserId = 7,
            Before = new { price = 10_000_000m, hasDiscount = false },
            After = new { price = 10_890_000m, hasDiscount = false },
            Metadata = new { note = "admin manual edit" }
        });

        var row = await harness.Db.AdminAuditLogs.AsNoTracking().SingleAsync();
        Assert.Equal(AuditActions.SubscriptionPlanPriceUpdated, row.Action);
        Assert.Equal("42", row.EntityId);
        Assert.Equal(7, row.ActorUserId);
        Assert.Contains("10000000", row.OldValue);
        Assert.Contains("10890000", row.NewValue);
        Assert.Contains("admin manual edit", row.Metadata);
        Assert.Equal("corr-test", row.CorrelationId);
        Assert.Equal("127.0.0.1", row.IpAddress);
        Assert.True(row.Succeeded);
    }

    [Fact]
    public async Task WriteAsync_InvalidEntry_IsIgnored()
    {
        await using var harness = await AuditTestHarness.CreateAsync();

        await harness.AuditService.WriteAsync(new AuditEntry
        {
            Category = " ",
            Action = AuditActions.PaymentVerified,
            EntityType = AuditEntityTypes.Payment
        });

        Assert.Equal(0, await harness.Db.AdminAuditLogs.CountAsync());
    }

    [Fact]
    public async Task SearchAsync_Filters_By_Entity()
    {
        await using var harness = await AuditTestHarness.CreateAsync();

        await harness.AuditService.WriteAsync(new AuditEntry
        {
            Category = AuditCategories.Subscription,
            Action = AuditActions.SubscriptionPlanUpdated,
            EntityType = AuditEntityTypes.SubscriptionPlan,
            EntityId = "10"
        });
        await harness.AuditService.WriteAsync(new AuditEntry
        {
            Category = AuditCategories.Payment,
            Action = AuditActions.PaymentVerified,
            EntityType = AuditEntityTypes.Payment,
            EntityId = "99"
        });

        var result = await harness.QueryService.SearchAsync(new AuditSearchRequestDto
        {
            EntityType = AuditEntityTypes.SubscriptionPlan,
            EntityId = "10"
        });

        Assert.True(result.Success);
        Assert.Equal(1, result.Data!.TotalCount);
        Assert.Equal("10", result.Data.Items[0].EntityId);
        Assert.True(result.Data.Items[0].CreatedAtTehran > DateTime.MinValue);
    }

    [Fact]
    public async Task WriteAsync_DoesNotThrow_When_Db_Unavailable()
    {
        var services = new ServiceCollection();
        services.AddDbContextFactory<Api_Context>(o =>
            o.UseSqlServer("Server=invalid,1433;Database=Nope;User Id=sa;Password=x;TrustServerCertificate=True;Connect Timeout=1"));
        var sp = services.BuildServiceProvider();

        var audit = new AuditService(
            sp.GetRequiredService<IDbContextFactory<Api_Context>>(),
            new FixedAuditContext(),
            NullLogger<AuditService>.Instance);

        var ex = await Record.ExceptionAsync(() => audit.WriteAsync(new AuditEntry
        {
            Category = AuditCategories.System,
            Action = "System.Ping",
            EntityType = "System",
            EntityId = "1"
        }));

        Assert.Null(ex);
    }

    private sealed class FixedAuditContext : Interfaces.IAuditContext
    {
        public string? CorrelationId => "corr-test";
        public int? ActorUserId => 1;
        public string? IpAddress => "127.0.0.1";
        public string? UserAgent => "unit-test";
        public string? RequestPath => "/api/test";
        public string? HttpMethod => "POST";
        public string Source => AuditSources.Http;
    }

    private sealed class AuditTestHarness : IAsyncDisposable
    {
        public required Api_Context Db { get; init; }
        public required AuditService AuditService { get; init; }
        public required AuditQueryService QueryService { get; init; }
        private ServiceProvider? _provider;

        public static async Task<AuditTestHarness> CreateAsync()
        {
            var services = new ServiceCollection();
            var dbName = "AuditTests_" + Guid.NewGuid().ToString("N");
            services.AddDbContextFactory<Api_Context>(o => o.UseInMemoryDatabase(dbName));
            services.AddDbContext<Api_Context>(o => o.UseInMemoryDatabase(dbName));
            var provider = services.BuildServiceProvider();
            var db = provider.GetRequiredService<Api_Context>();
            await db.Database.EnsureCreatedAsync();

            var context = new FixedAuditContext();
            var audit = new AuditService(
                provider.GetRequiredService<IDbContextFactory<Api_Context>>(),
                context,
                NullLogger<AuditService>.Instance);

            return new AuditTestHarness
            {
                Db = db,
                AuditService = audit,
                QueryService = new AuditQueryService(db),
                _provider = provider
            };
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            if (_provider != null)
                await _provider.DisposeAsync();
        }
    }
}
