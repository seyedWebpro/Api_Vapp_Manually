using Api_Vapp.Data;
using Api_Vapp.DTOs.Admin;
using Api_Vapp.DTOs.Common;
using Api_Vapp.Services.Admin;
using Api_Vapp.Tests.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Api_Vapp.Tests.Admin;

public class ForbiddenWordServiceTests : IAsyncLifetime
{
    private Api_Context _context = null!;
    private MemoryCache _cache = null!;
    private ForbiddenWordService _service = null!;

    public async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<Api_Context>()
            .UseInMemoryDatabase($"forbidden-words-{Guid.NewGuid():N}")
            .Options;

        _context = new Api_Context(options);
        _cache = new MemoryCache(new MemoryCacheOptions { SizeLimit = 1024 });
        _service = new ForbiddenWordService(
            new Api_Vapp.Repositories.ForbiddenWordRepository(_context),
            _cache,
            new NoOpAuditService(),
            NullLogger<ForbiddenWordService>.Instance);

        await _context.Database.EnsureCreatedAsync();
    }

    public Task DisposeAsync()
    {
        _cache.Dispose();
        _context.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task CreateAsync_SupportsBulkAndSkipsDuplicates()
    {
        var first = await _service.CreateAsync(new CreateForbiddenWordDto
        {
            Word = "قمار\nشرط بندی",
            IsActive = true
        });

        Assert.True(first.Success);
        Assert.Equal(2, first.Data!.CreatedCount);

        var second = await _service.CreateAsync(new CreateForbiddenWordDto
        {
            Word = "قمار،کازینو",
            IsActive = true
        });

        Assert.True(second.Success);
        Assert.Equal(1, second.Data!.CreatedCount);
        Assert.Equal(1, second.Data.SkippedDuplicateCount);
    }

    [Fact]
    public async Task TryBlockIfContainsAsync_BlocksMatchedText()
    {
        await _service.CreateAsync(new CreateForbiddenWordDto { Word = "قمار", IsActive = true });

        var blocked = await _service.TryBlockIfContainsAsync<object>("متن قمار ممنوع");
        Assert.NotNull(blocked);
        Assert.False(blocked!.Success);
        Assert.Equal(ErrorCodes.FilteredWord, blocked.ErrorCode);
        Assert.Contains("قمار", blocked.Errors!);
    }

    [Fact]
    public async Task TryBlockIfContainsAsync_AllowsCleanText()
    {
        await _service.CreateAsync(new CreateForbiddenWordDto { Word = "قمار", IsActive = true });

        var blocked = await _service.TryBlockIfContainsAsync<object>("پیام عادی salam");
        Assert.Null(blocked);
    }

    [Fact]
    public async Task DeleteAsync_SoftDeletesAndInvalidatesCache()
    {
        var created = await _service.CreateAsync(new CreateForbiddenWordDto { Word = "ممنوع", IsActive = true });
        var id = created.Data!.Created[0].Id;

        var deleted = await _service.DeleteAsync(id);
        Assert.True(deleted.Success);

        var active = await _service.GetActiveWordsAsync();
        Assert.DoesNotContain("ممنوع", active.Data!);
    }
}
