using Api_Vapp.Data;
using Api_Vapp.DTOs.Admin;
using Api_Vapp.Models;
using Api_Vapp.Services.Admin;
using Api_Vapp.Tests.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Api_Vapp.Tests.Admin;

public class AppNewsTickerServiceTests : IAsyncLifetime
{
    private Api_Context _context = null!;
    private MemoryCache _cache = null!;
    private AppNewsTickerService _service = null!;

    public async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<Api_Context>()
            .UseInMemoryDatabase($"news-ticker-{Guid.NewGuid():N}")
            .Options;

        _context = new Api_Context(options);
        _cache = new MemoryCache(new MemoryCacheOptions { SizeLimit = 1024 });
        _service = new AppNewsTickerService(
            new Api_Vapp.Repositories.AppNewsTickerRepository(_context),
            _cache,
            new NoOpAuditService(),
            NullLogger<AppNewsTickerService>.Instance);

        await _context.Database.EnsureCreatedAsync();
    }

    [Fact]
    public async Task CreateAsync_PreservesLongUnicodeAndEmojiText()
    {
        var longText = string.Join(" ", Enumerable.Repeat("خبر مهم فارسی 🚀🎉", 50));

        var result = await _service.CreateAsync(new CreateAppNewsTickerMessageDto
        {
            Text = longText,
            SortOrder = 3,
            IsActive = true
        });

        Assert.True(result.Success);
        Assert.Equal(longText, result.Data!.Text);
        Assert.Equal(longText, (await _context.AppNewsTickerMessages.SingleAsync()).Text);
    }

    [Fact]
    public async Task GetActiveAsync_ReturnsOnlyActiveNonDeletedMessagesInDisplayOrder()
    {
        _context.AppNewsTickerMessages.AddRange(
            Message("دوم", 20, isActive: true),
            Message("اول", 10, isActive: true),
            Message("غیرفعال", 1, isActive: false),
            Message("حذف‌شده", 2, isActive: true, isDeleted: true));
        await _context.SaveChangesAsync();

        var result = await _service.GetActiveAsync();

        Assert.True(result.Success);
        Assert.Equal(new[] { "اول", "دوم" }, result.Data!.Select(item => item.Text));
    }

    [Fact]
    public async Task Mutations_InvalidateActiveListCache()
    {
        var initial = await _service.GetActiveAsync();
        Assert.Empty(initial.Data!);

        var created = await _service.CreateAsync(new CreateAppNewsTickerMessageDto
        {
            Text = "پیام تازه ✅",
            SortOrder = 1,
            IsActive = true
        });
        Assert.Single((await _service.GetActiveAsync()).Data!);

        await _service.UpdateAsync(created.Data!.Id, new UpdateAppNewsTickerMessageDto
        {
            Text = created.Data.Text,
            SortOrder = created.Data.SortOrder,
            IsActive = false
        });
        Assert.Empty((await _service.GetActiveAsync()).Data!);
    }

    private static AppNewsTickerMessage Message(
        string text,
        int sortOrder,
        bool isActive,
        bool isDeleted = false) => new()
    {
        Text = text,
        SortOrder = sortOrder,
        IsActive = isActive,
        IsDeleted = isDeleted,
        CreatedAt = DateTime.UtcNow
    };

    public async Task DisposeAsync()
    {
        _cache.Dispose();
        await _context.DisposeAsync();
    }
}
