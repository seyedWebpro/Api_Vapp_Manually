using Api_Vapp.Data;
using Api_Vapp.DTOs.Message;
using Api_Vapp.Services;
using Api_Vapp.Tests.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Api_Vapp.Tests.SmsPricing;

/// <summary>
/// تخمین کاربرمحور باید همان موتور SmsPartsCalculator را با متن خالی هم برگرداند.
/// </summary>
public class SmsPartsEstimateForUserTests
{
    [Fact]
    public async Task EstimatePartsForUser_EmptyContent_MatchesCalculatorWithOptOut()
    {
        await using var host = SmsPricingTestHost.Create();
        var result = await host.Service.EstimatePartsForUserAsync(new SmsPartsEstimateRequestDto
        {
            Content = "",
            RecipientsCount = 1
        });

        Assert.True(result.Success);
        Assert.NotNull(result.Data);

        var expected = SmsPartsCalculator.Analyze("", SmsPartsRules.Defaults, throwOnMaxPages: false);
        Assert.Equal(expected.WeightedCharacterCount, result.Data!.WeightedCharacterCount);
        Assert.Equal(expected.PartsCount, result.Data.PartsCount);
        Assert.Equal(expected.OptOutApplied, result.Data.OptOutApplied);
        Assert.False(result.Data.ExceedsMaxPages);
    }

    [Fact]
    public async Task EstimatePartsForUser_PersianShort_MatchesCalculator()
    {
        await using var host = SmsPricingTestHost.Create();
        const string content = "سلام";

        var result = await host.Service.EstimatePartsForUserAsync(new SmsPartsEstimateRequestDto
        {
            Content = content,
            RecipientsCount = 2
        });

        Assert.True(result.Success);
        Assert.NotNull(result.Data);

        var expected = SmsPartsCalculator.Analyze(content, SmsPartsRules.Defaults, throwOnMaxPages: false);
        Assert.Equal(expected.WeightedCharacterCount, result.Data!.WeightedCharacterCount);
        Assert.Equal(expected.PartsCount, result.Data.PartsCount);
        Assert.Equal(expected.IsPersian, result.Data.IsPersian);
        Assert.True(result.Data.OptOutApplied);
        Assert.Equal(2, result.Data.RecipientsCount);
        Assert.Equal(
            SmsPartsCalculator.CalculateCost(expected.PartsCount, result.Data.CostPerPart, 2),
            result.Data.EstimatedTotalCost);
    }
}

file sealed class SmsPricingTestHost : IAsyncDisposable
{
    public required SmsPricingService Service { get; init; }
    public required Api_Context Db { get; init; }

    public static SmsPricingTestHost Create()
    {
        var options = new DbContextOptionsBuilder<Api_Context>()
            .UseInMemoryDatabase($"sms-parts-estimate-{Guid.NewGuid():N}")
            .Options;
        var db = new Api_Context(options);
        var cache = new MemoryCache(new MemoryCacheOptions());
        var config = new ConfigurationBuilder().Build();
        var env = new FakeHostEnvironment();
        var service = new SmsPricingService(
            db,
            cache,
            config,
            env,
            new NoOpAuditService(),
            NullLogger<SmsPricingService>.Instance);

        return new SmsPricingTestHost { Service = service, Db = db };
    }

    public async ValueTask DisposeAsync() => await Db.DisposeAsync();
}

file sealed class FakeHostEnvironment : IHostEnvironment
{
    public string EnvironmentName { get; set; } = "Development";
    public string ApplicationName { get; set; } = "Api_Vapp.Tests";
    public string ContentRootPath { get; set; } = ".";
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}
