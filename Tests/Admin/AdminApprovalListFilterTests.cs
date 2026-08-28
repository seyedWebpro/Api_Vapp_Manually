using Api_Vapp.Constants;
using Api_Vapp.Data;
using Api_Vapp.DTOs.Admin;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;
using Api_Vapp.Services.Admin;
using Api_Vapp.Services.Audit;
using Api_Vapp.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Api_Vapp.Tests.Admin;

public class AdminApprovalListFilterTests
{
    [Fact]
    public async Task TemplateFilters_SearchByContent_ReturnsMatchingTemplate()
    {
        await using var ctx = await SeedContextAsync();

        var query = ctx.MessageTemplates.AsNoTracking()
            .Include(t => t.User)
            .Where(t => !t.IsDeleted);

        var filtered = AdminApprovalListFilters.ApplyTemplateFilters(query, "هلدینگ", null);
        var items = await filtered.ToListAsync();

        Assert.Single(items);
        Assert.Equal("قالب ۲", items[0].Name);
    }

    [Fact]
    public async Task TemplateFilters_UserSearchByName_ReturnsUserTemplates()
    {
        await using var ctx = await SeedContextAsync();

        var query = ctx.MessageTemplates.AsNoTracking()
            .Include(t => t.User)
            .Where(t => !t.IsDeleted);

        var filtered = AdminApprovalListFilters.ApplyTemplateFilters(query, null, "علیرضا");
        var items = await filtered.ToListAsync();

        Assert.Equal(2, items.Count);
        Assert.All(items, t => Assert.Equal("علیرضا سعیدی", t.User.FullName));
    }

    [Fact]
    public async Task TemplateFilters_UserSearchById_ReturnsOnlyThatUser()
    {
        await using var ctx = await SeedContextAsync();
        var otherUserId = await ctx.Users.Where(u => u.FullName == "کاربر دیگر").Select(u => u.Id).SingleAsync();

        var query = ctx.MessageTemplates.AsNoTracking()
            .Include(t => t.User)
            .Where(t => !t.IsDeleted);

        var filtered = AdminApprovalListFilters.ApplyTemplateFilters(query, null, otherUserId.ToString());
        var items = await filtered.ToListAsync();

        Assert.Single(items);
        Assert.Equal("قالب دیگر", items[0].Name);
    }

    [Fact]
    public async Task MessageFilters_SearchByTitle_ReturnsMatchingRequest()
    {
        await using var ctx = await SeedContextAsync();

        var query = ctx.SmsApprovalRequests.AsNoTracking()
            .Include(r => r.User)
            .Where(r => !r.IsDeleted);

        var filtered = AdminApprovalListFilters.ApplyMessageApprovalFilters(query, "کمپین تابستان", null);
        var items = await filtered.ToListAsync();

        Assert.Single(items);
        Assert.Contains("کمپین تابستان", items[0].TitlePreview);
    }

    [Fact]
    public async Task MessageFilters_UserSearchByPhone_ReturnsMatchingRequest()
    {
        await using var ctx = await SeedContextAsync();

        var query = ctx.SmsApprovalRequests.AsNoTracking()
            .Include(r => r.User)
            .Where(r => !r.IsDeleted);

        var filtered = AdminApprovalListFilters.ApplyMessageApprovalFilters(query, null, "09121112222");
        var items = await filtered.ToListAsync();

        Assert.Single(items);
        Assert.Equal("کمپین تابستان", items[0].TitlePreview);
    }

    [Fact]
    public async Task TemplateFilters_UserSearchByShortNumericId_DoesNotMatchPhoneDigits()
    {
        await using var ctx = await SeedContextAsync();
        var userBId = await ctx.Users.Where(u => u.FullName == "کاربر دیگر").Select(u => u.Id).SingleAsync();

        var query = ctx.MessageTemplates.AsNoTracking()
            .Include(t => t.User)
            .Where(t => !t.IsDeleted);

        var filtered = AdminApprovalListFilters.ApplyTemplateFilters(query, null, userBId.ToString());
        var items = await filtered.ToListAsync();

        Assert.Single(items);
        Assert.Equal(userBId, items[0].UserId);
    }

    [Fact]
    public async Task TemplateService_GetAllAsync_Pagination_ReturnsSecondPage()
    {
        await using var ctx = await SeedContextAsync();
        var service = CreateTemplateService(ctx);

        var page1 = await service.GetAllAsync(page: 1, pageSize: 2);
        var page2 = await service.GetAllAsync(page: 2, pageSize: 2);

        Assert.True(page1.Success);
        Assert.True(page2.Success);
        Assert.Equal(3, page1.Data!.TotalCount);
        Assert.Equal(2, page1.Data.Items.Count);
        Assert.Equal(2, page1.Data.TotalPages);
        Assert.Single(page2.Data!.Items);
        Assert.Equal(2, page2.Data.PageNumber);
    }

    [Fact]
    public async Task TemplateService_GetAllAsync_CombinedFilters_NarrowsResults()
    {
        await using var ctx = await SeedContextAsync();
        var service = CreateTemplateService(ctx);

        var result = await service.GetAllAsync(
            status: AdminApprovalStatuses.Approved,
            search: "هلدینگ",
            userSearch: "علیرضا");

        Assert.True(result.Success);
        Assert.Equal(1, result.Data!.TotalCount);
        Assert.Equal("قالب ۲", result.Data.Items[0].Name);
    }

    private static AdminTemplateApprovalService CreateTemplateService(Api_Context ctx) =>
        new(
            ctx,
            new NoOpAuditService(),
            new NoOpUserAppNotifier(),
            NullLogger<AdminTemplateApprovalService>.Instance);

    private static async Task<Api_Context> SeedContextAsync()
    {
        var options = new DbContextOptionsBuilder<Api_Context>()
            .UseInMemoryDatabase($"admin-approval-filters-{Guid.NewGuid():N}")
            .Options;

        var ctx = new Api_Context(options);
        await ctx.Database.EnsureCreatedAsync();

        var userA = new User
        {
            PhoneNumber = "09121112222",
            FullName = "علیرضا سعیدی",
            CreatedAt = DateTime.UtcNow,
        };
        var userB = new User
        {
            PhoneNumber = "09123334444",
            FullName = "کاربر دیگر",
            CreatedAt = DateTime.UtcNow,
        };
        ctx.Users.AddRange(userA, userB);
        await ctx.SaveChangesAsync();

        ctx.MessageTemplates.AddRange(
            new MessageTemplate
            {
                UserId = userA.Id,
                Name = "قالب ۱",
                Content = "سلام مشتری عزیز",
                ApprovalStatus = AdminApprovalStatuses.Pending,
                CreatedAt = DateTime.UtcNow.AddMinutes(-3),
            },
            new MessageTemplate
            {
                UserId = userA.Id,
                Name = "قالب ۲",
                Content = "سلام هلدینگ ایرانیان اوقات خوشی را برای شما آرزومند است",
                ApprovalStatus = AdminApprovalStatuses.Approved,
                CreatedAt = DateTime.UtcNow.AddMinutes(-2),
            },
            new MessageTemplate
            {
                UserId = userB.Id,
                Name = "قالب دیگر",
                Content = "متن کاربر دوم",
                ApprovalStatus = AdminApprovalStatuses.Pending,
                CreatedAt = DateTime.UtcNow.AddMinutes(-1),
            });

        ctx.SmsApprovalRequests.AddRange(
            new SmsApprovalRequest
            {
                UserId = userA.Id,
                RequestType = SmsApprovalRequestTypes.Campaign,
                ContentPreview = "ارسال پیامک کمپین تابستان",
                TitlePreview = "کمپین تابستان",
                RecipientsCount = 10,
                Status = AdminApprovalStatuses.Pending,
                CreatedAt = DateTime.UtcNow.AddMinutes(-2),
            },
            new SmsApprovalRequest
            {
                UserId = userB.Id,
                RequestType = SmsApprovalRequestTypes.DirectMessage,
                ContentPreview = "پیام مستقیم کاربر دوم",
                TitlePreview = null,
                RecipientsCount = 1,
                Status = AdminApprovalStatuses.Pending,
                CreatedAt = DateTime.UtcNow.AddMinutes(-1),
            });

        await ctx.SaveChangesAsync();
        return ctx;
    }

    private sealed class NoOpAuditService : IAuditService
    {
        public Task WriteAsync(AuditEntry entry, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task WriteRangeAsync(IEnumerable<AuditEntry> entries, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class NoOpUserAppNotifier : IUserAppNotifier
    {
        public Task NotifyAsync(
            int userId,
            NotificationCategory category,
            string title,
            string body,
            string type,
            int? relatedEntityId = null,
            string? relatedEntityType = null,
            string? actionUrl = null,
            string? metadataJson = null,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
