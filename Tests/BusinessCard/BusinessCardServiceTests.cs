using Api_Vapp.Data;
using Api_Vapp.DTOs.BusinessCard;
using Api_Vapp.DTOs.Common;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;
using Api_Vapp.Repositories;
using Api_Vapp.Services;
using Api_Vapp.Tests.Shared;
using Api_Vapp.Tests.UserForm;
using Api_Vapp.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Api_Vapp.Tests.BusinessCard;

public class BusinessCardServiceTests
{
    [Fact]
    public async Task CreateDraft_ThenPublish_ReturnsPublicUrl()
    {
        await using var ctx = await BusinessCardTestContext.CreateAsync();

        var create = await ctx.Service.CreateDraftAsync(ctx.OwnerUserId, new CreateBusinessCardDto
        {
            TemplateKey = "business",
            Title = "سالن زیبایی زهرا",
            DescriptionEnabled = true,
            ContactEnabled = true,
            ContactPhone = "09121234567"
        });

        Assert.True(create.Success);
        Assert.Equal(201, create.StatusCode);
        Assert.NotNull(create.Data);
        Assert.Equal("Draft", create.Data!.Status);

        var publish = await ctx.Service.PublishAsync(create.Data.Id, ctx.OwnerUserId, new PublishBusinessCardDto
        {
            Slug = "zahra-salon-unit"
        });

        Assert.True(publish.Success);
        Assert.Equal("Published", publish.Data!.Status);
        Assert.True(publish.Data.IsActive);
        Assert.Equal("zahra-salon-unit", publish.Data.Slug);
        Assert.Contains("zahra-salon-unit", publish.Data.PublicUrl);
    }

    [Fact]
    public async Task UpdateSections_ReplacesServiceItems()
    {
        await using var ctx = await BusinessCardTestContext.CreateAsync();

        var create = await ctx.Service.CreateDraftAsync(ctx.OwnerUserId, new CreateBusinessCardDto
        {
            Title = "کارت تست",
            ServicesEnabled = true,
            ServiceItems =
            [
                new BusinessCardServiceItemDto { Title = "قدیمی", Price = 1000, DisplayOrder = 0 }
            ]
        });

        var update = await ctx.Service.UpdateSectionsAsync(create.Data!.Id, ctx.OwnerUserId, new UpdateBusinessCardSectionsDto
        {
            ServicesEnabled = true,
            ServiceItems =
            [
                new BusinessCardServiceItemDto { Title = "فیشیال", Price = 350000, DisplayOrder = 0 },
                new BusinessCardServiceItemDto { Title = "رنگ مو", Price = 500000, DisplayOrder = 1 }
            ]
        });

        Assert.True(update.Success);
        Assert.Equal(2, update.Data!.ServiceItems.Count);
        Assert.Equal("فیشیال", update.Data.ServiceItems[0].Title);
    }

    [Fact]
    public async Task ToggleActive_OnDraft_Fails()
    {
        await using var ctx = await BusinessCardTestContext.CreateAsync();

        var create = await ctx.Service.CreateDraftAsync(ctx.OwnerUserId, new CreateBusinessCardDto
        {
            Title = "پیش‌نویس",
            DescriptionEnabled = true
        });

        var toggle = await ctx.Service.SetActiveStatusAsync(create.Data!.Id, ctx.OwnerUserId, false);

        Assert.False(toggle.Success);
        Assert.Equal(400, toggle.StatusCode);
        Assert.Equal(ErrorCodes.ValidationFailed, toggle.ErrorCode);
    }

    [Fact]
    public async Task GetById_OtherUser_ReturnsForbidden()
    {
        await using var ctx = await BusinessCardTestContext.CreateAsync();

        var create = await ctx.Service.CreateDraftAsync(ctx.OwnerUserId, new CreateBusinessCardDto
        {
            Title = "مالک",
            DescriptionEnabled = true
        });

        var result = await ctx.Service.GetByIdAsync(create.Data!.Id, ctx.OtherUserId);

        Assert.False(result.Success);
        Assert.Equal(403, result.StatusCode);
        Assert.Equal(ErrorCodes.Forbidden, result.ErrorCode);
    }

    [Fact]
    public async Task PublicService_OnlyReturnsPublishedActive()
    {
        await using var ctx = await BusinessCardTestContext.CreateAsync();

        var create = await ctx.Service.CreateDraftAsync(ctx.OwnerUserId, new CreateBusinessCardDto
        {
            Title = "عمومی",
            DescriptionEnabled = true,
            DescriptionText = "متن"
        });

        var before = await ctx.PublicService.GetPublicCardAsync("public-card-unit");
        Assert.False(before.Success);

        await ctx.Service.PublishAsync(create.Data!.Id, ctx.OwnerUserId, new PublishBusinessCardDto
        {
            Slug = "public-card-unit"
        });

        var after = await ctx.PublicService.GetPublicCardAsync("public-card-unit");
        Assert.True(after.Success);
        Assert.Equal("عمومی", after.Data!.Title);
    }

    [Fact]
    public async Task CreateDraft_InvalidSlug_Fails()
    {
        await using var ctx = await BusinessCardTestContext.CreateAsync();

        var result = await ctx.Service.CreateDraftAsync(ctx.OwnerUserId, new CreateBusinessCardDto
        {
            Title = "تست",
            Slug = "Bad Slug!!",
            DescriptionEnabled = true
        });

        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
    }
}

internal sealed class BusinessCardTestContext : IAsyncDisposable
{
    private readonly Api_Context _context;

    private BusinessCardTestContext(Api_Context context)
    {
        _context = context;
    }

    public IBusinessCardService Service { get; private set; } = null!;

    public IBusinessCardPublicService PublicService { get; private set; } = null!;

    public int OwnerUserId { get; private set; }

    public int OtherUserId { get; private set; }

    public static async Task<BusinessCardTestContext> CreateAsync()
    {
        var options = new DbContextOptionsBuilder<Api_Context>()
            .UseInMemoryDatabase($"BusinessCardTests_{Guid.NewGuid():N}")
            .Options;

        var context = new Api_Context(options);
        await context.Database.EnsureCreatedAsync();

        var owner = new User
        {
            PhoneNumber = $"09{Random.Shared.NextInt64(100000000, 999999999)}",
            CreatedAt = DateTime.UtcNow
        };
        var other = new User
        {
            PhoneNumber = $"09{Random.Shared.NextInt64(100000000, 999999999)}",
            CreatedAt = DateTime.UtcNow
        };
        context.Users.AddRange(owner, other);
        await context.SaveChangesAsync();

        var repo = new BusinessCardRepository(context);
        var fileUpload = new FakeFileUploadService();
        var optionsMonitor = Options.Create(new BusinessCardOptions
        {
            PublicBaseUrl = "https://ok-sms.ir/card"
        });

        var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new BusinessCardService(
            repo,
            context,
            optionsMonitor,
            fileUpload,
            new NoOpAuditService(),
            cache,
            NullLogger<BusinessCardService>.Instance);

        var publicService = new BusinessCardPublicService(
            repo,
            fileUpload,
            cache,
            NullLogger<BusinessCardPublicService>.Instance);

        return new BusinessCardTestContext(context)
        {
            Service = service,
            PublicService = publicService,
            OwnerUserId = owner.Id,
            OtherUserId = other.Id
        };
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
    }
}
