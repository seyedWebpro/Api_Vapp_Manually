using Api_Vapp.Constants;
using Api_Vapp.Data;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;
using Api_Vapp.Services.Admin;
using Api_Vapp.Services.Audit;
using Api_Vapp.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Api_Vapp.Tests.Admin;

/// <summary>
/// پس از ویرایش، آیتم باید با UpdatedAt بالاتر از CreatedAt قدیمی در ابتدای صف تأیید بیاید.
/// </summary>
public class AdminApprovalQueueOrderingTests
{
    [Fact]
    public async Task TemplatePending_EditedOlderItem_AppearsFirst()
    {
        await using var ctx = await CreateContextAsync();
        var user = await SeedUserAsync(ctx);

        var older = new MessageTemplate
        {
            UserId = user.Id,
            Name = "قالب قدیمی ویرایش‌شده",
            Content = "متن قدیمی که تازه ویرایش شد",
            ApprovalStatus = AdminApprovalStatuses.Pending,
            CreatedAt = DateTime.UtcNow.AddDays(-10),
            UpdatedAt = DateTime.UtcNow,
        };
        var newer = new MessageTemplate
        {
            UserId = user.Id,
            Name = "قالب تازه‌ساخته",
            Content = "متن تازه بدون ویرایش",
            ApprovalStatus = AdminApprovalStatuses.Pending,
            CreatedAt = DateTime.UtcNow.AddHours(-1),
            UpdatedAt = null,
        };
        ctx.MessageTemplates.AddRange(older, newer);
        await ctx.SaveChangesAsync();

        var service = CreateTemplateService(ctx);
        var result = await service.GetPendingAsync(page: 1, pageSize: 20);

        Assert.True(result.Success);
        Assert.Equal(2, result.Data!.TotalCount);
        Assert.Equal("قالب قدیمی ویرایش‌شده", result.Data.Items[0].Name);
        Assert.Equal("قالب تازه‌ساخته", result.Data.Items[1].Name);
        Assert.NotNull(result.Data.Items[0].UpdatedAt);
    }

    [Fact]
    public async Task MessagePending_UpsertedOlderRequest_AppearsFirst()
    {
        await using var ctx = await CreateContextAsync();
        var user = await SeedUserAsync(ctx);

        var olderEdited = new SmsApprovalRequest
        {
            UserId = user.Id,
            RequestType = SmsApprovalRequestTypes.Campaign,
            ContentPreview = "کمپین ویرایش‌شده",
            TitlePreview = "کمپین ویرایش‌شده",
            RecipientsCount = 5,
            Status = AdminApprovalStatuses.Pending,
            CreatedAt = DateTime.UtcNow.AddDays(-5),
            UpdatedAt = DateTime.UtcNow,
        };
        var newerCreated = new SmsApprovalRequest
        {
            UserId = user.Id,
            RequestType = SmsApprovalRequestTypes.DirectMessage,
            ContentPreview = "پیام تازه‌ثبت‌شده",
            TitlePreview = "پیام تازه",
            RecipientsCount = 1,
            Status = AdminApprovalStatuses.Pending,
            CreatedAt = DateTime.UtcNow.AddMinutes(-10),
            UpdatedAt = null,
        };
        ctx.SmsApprovalRequests.AddRange(olderEdited, newerCreated);
        await ctx.SaveChangesAsync();

        var service = CreateMessageService(ctx);
        var result = await service.GetPendingAsync(page: 1, pageSize: 20);

        Assert.True(result.Success);
        Assert.Equal(2, result.Data!.TotalCount);
        Assert.Equal("کمپین ویرایش‌شده", result.Data.Items[0].TitlePreview);
        Assert.Equal("پیام تازه", result.Data.Items[1].TitlePreview);
        Assert.NotNull(result.Data.Items[0].UpdatedAt);
    }

    [Fact]
    public async Task QuickSendPending_ResetToPending_AppearsFirst()
    {
        await using var ctx = await CreateContextAsync();
        var user = await SeedUserAsync(ctx);

        var olderEdited = new global::Api_Vapp.Models.UserForm
        {
            UserId = user.Id,
            Title = "فرم ویرایش‌شده",
            Slug = "edited-form-order",
            SmsCaption = "کپشن ویرایش‌شده",
            Status = UserFormStatus.Published,
            ApprovalStatus = AdminApprovalStatuses.Pending,
            IsActive = true,
            CreatedAt = DateTime.UtcNow.AddDays(-7),
            UpdatedAt = DateTime.UtcNow,
            PublishedAt = DateTime.UtcNow.AddDays(-7),
        };
        var newerCreated = new global::Api_Vapp.Models.UserForm
        {
            UserId = user.Id,
            Title = "فرم تازه‌منتشر",
            Slug = "new-form-order",
            SmsCaption = "کپشن تازه",
            Status = UserFormStatus.Published,
            ApprovalStatus = AdminApprovalStatuses.Pending,
            IsActive = true,
            CreatedAt = DateTime.UtcNow.AddHours(-2),
            UpdatedAt = null,
            PublishedAt = DateTime.UtcNow.AddHours(-2),
        };
        ctx.UserForms.AddRange(olderEdited, newerCreated);
        await ctx.SaveChangesAsync();

        var service = CreateQuickSendService(ctx);
        var result = await service.GetPendingAsync(page: 1, pageSize: 20);

        Assert.True(result.Success);
        Assert.Equal(2, result.Data!.TotalCount);
        Assert.Equal("فرم ویرایش‌شده", result.Data.Items[0].Title);
        Assert.Equal("فرم تازه‌منتشر", result.Data.Items[1].Title);
        Assert.NotNull(result.Data.Items[0].UpdatedAt);
    }

    private static AdminTemplateApprovalService CreateTemplateService(Api_Context ctx) =>
        new(
            ctx,
            new NoOpAuditService(),
            new NoOpUserAppNotifier(),
            NullLogger<AdminTemplateApprovalService>.Instance);

    private static AdminMessageApprovalService CreateMessageService(Api_Context ctx) =>
        new(
            ctx,
            messageService: null!,
            referralProgramService: null!,
            audit: new NoOpAuditService(),
            logger: NullLogger<AdminMessageApprovalService>.Instance,
            appNotifier: new NoOpUserAppNotifier());

    private static AdminQuickSendApprovalService CreateQuickSendService(Api_Context ctx) =>
        new(
            ctx,
            new NoOpAuditService(),
            new NoOpUserAppNotifier(),
            NullLogger<AdminQuickSendApprovalService>.Instance,
            Options.Create(new BusinessCardOptions()),
            Options.Create(new BookingSystemOptions()),
            Options.Create(new FormBuilderOptions()),
            Options.Create(new LuckyWheelOptions()),
            new MemoryCache(new MemoryCacheOptions()));

    private static async Task<Api_Context> CreateContextAsync()
    {
        var options = new DbContextOptionsBuilder<Api_Context>()
            .UseInMemoryDatabase($"admin-approval-order-{Guid.NewGuid():N}")
            .Options;
        var ctx = new Api_Context(options);
        await ctx.Database.EnsureCreatedAsync();
        return ctx;
    }

    private static async Task<User> SeedUserAsync(Api_Context ctx)
    {
        var user = new User
        {
            PhoneNumber = "09120001122",
            FullName = "کاربر تست صف",
            CreatedAt = DateTime.UtcNow,
        };
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();
        return user;
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
