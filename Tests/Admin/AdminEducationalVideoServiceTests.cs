using Api_Vapp.Data;
using Api_Vapp.Models;
using Api_Vapp.Services.Admin;
using Api_Vapp.Tests.Shared;
using Api_Vapp.Tests.UserForm;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Api_Vapp.Tests.Admin;

public class AdminEducationalVideoServiceTests : IAsyncLifetime
{
    private Api_Context _context = null!;
    private MemoryCache _cache = null!;
    private AdminEducationalVideoService _service = null!;

    public async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<Api_Context>()
            .UseInMemoryDatabase($"educational-video-{Guid.NewGuid():N}")
            .Options;

        _context = new Api_Context(options);
        _cache = new MemoryCache(new MemoryCacheOptions { SizeLimit = 1024 });

        _service = new AdminEducationalVideoService(
            _context,
            new NoOpAuditService(),
            new NoOpUserPushNotifier(),
            new FakeFileUploadService(),
            _cache,
            NullLogger<AdminEducationalVideoService>.Instance);

        _context.EducationalVideos.AddRange(
            new EducationalVideo
            {
                Title = "ویدیو فعال",
                VideoUrl = "https://www.aparat.com/v/test1",
                SortOrder = 0,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new EducationalVideo
            {
                Title = "در حال آپلود",
                VideoUrl = "pending",
                SortOrder = 1,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new EducationalVideo
            {
                Title = "غیرفعال",
                VideoUrl = "https://www.aparat.com/v/inactive",
                SortOrder = 2,
                IsActive = false,
                CreatedAt = DateTime.UtcNow
            });

        await _context.SaveChangesAsync();
    }

    [Fact]
    public async Task GetActiveVideosAsync_WithSizeLimitedCache_ReturnsSuccessAndCaches()
    {
        var first = await _service.GetActiveVideosAsync();
        var second = await _service.GetActiveVideosAsync();

        Assert.True(first.Success);
        Assert.Equal(200, first.StatusCode);
        Assert.Single(first.Data!);
        Assert.Equal("ویدیو فعال", first.Data![0].Title);
        Assert.Equal("https://www.aparat.com/v/test1", first.Data[0].VideoUrl);
        Assert.Equal(
            "https://www.aparat.com/video/video/embed/videohash/test1/vt/frame",
            first.Data[0].PlaybackUrl);
        Assert.Equal("aparat_embed", first.Data[0].PlaybackMode);

        Assert.True(second.Success);
        Assert.Equal(200, second.StatusCode);
        Assert.Single(second.Data!);
    }

    public async Task DisposeAsync()
    {
        _cache.Dispose();
        await _context.DisposeAsync();
    }
}
