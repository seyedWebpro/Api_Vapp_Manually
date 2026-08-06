using Api_Vapp.Constants;
using Api_Vapp.Data;
using Api_Vapp.DTOs.Admin;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;
using Api_Vapp.Services;
using Api_Vapp.Services.Admin;
using Api_Vapp.Services.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Api_Vapp.Tests.Notification;

public class InAppNotificationApprovalTests
{
    [Fact]
    public async Task TemplateApprove_Creates_InApp_Notification()
    {
        await using var h = await Harness.CreateAsync();

        var template = new MessageTemplate
        {
            UserId = h.User.Id,
            Name = "قالب تست",
            Content = "سلام مشتری عزیز",
            ApprovalStatus = AdminApprovalStatuses.Pending,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        h.Db.MessageTemplates.Add(template);
        await h.Db.SaveChangesAsync();

        var result = await h.TemplateApprovals.ApproveAsync(template.Id, adminUserId: 99);
        Assert.True(result.Success);

        var n = await h.Db.InAppNotifications.AsNoTracking()
            .SingleAsync(x => x.UserId == h.User.Id);

        Assert.Equal(InAppNotificationTypes.TemplateApproved, n.Type);
        Assert.Contains("تأیید", n.Title);
        Assert.False(n.IsRead);
        Assert.Equal(template.Id, n.RelatedEntityId);
        Assert.Equal(1, h.Push.NotifyCalls);
    }

    [Fact]
    public async Task TemplateReject_Creates_InApp_Notification_With_Reason()
    {
        await using var h = await Harness.CreateAsync();

        var template = new MessageTemplate
        {
            UserId = h.User.Id,
            Name = "قالب رد",
            Content = "متن نامناسب",
            ApprovalStatus = AdminApprovalStatuses.Pending,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        h.Db.MessageTemplates.Add(template);
        await h.Db.SaveChangesAsync();

        var result = await h.TemplateApprovals.RejectAsync(
            template.Id,
            adminUserId: 99,
            new RejectApprovalDto { Reason = "محتوا تبلیغاتی غیرمجاز است" });

        Assert.True(result.Success);

        var n = await h.Db.InAppNotifications.AsNoTracking()
            .SingleAsync(x => x.UserId == h.User.Id);

        Assert.Equal(InAppNotificationTypes.TemplateRejected, n.Type);
        Assert.Contains("محتوا تبلیغاتی غیرمجاز است", n.Body);
        Assert.Contains("Rejected", n.Metadata);
        Assert.Equal(1, h.Push.NotifyCalls);
    }

    [Fact]
    public async Task List_UnreadCount_MarkRead_Work()
    {
        await using var h = await Harness.CreateAsync();

        await h.InApp.CreateSafeAsync(
            h.User.Id,
            "عنوان",
            "متن",
            InAppNotificationTypes.MessageApproved);

        var unread = await h.InApp.GetUnreadCountAsync(h.User.Id);
        Assert.True(unread.Success);
        Assert.Equal(1, unread.Data!.Count);

        var list = await h.InApp.GetMyNotificationsAsync(h.User.Id);
        Assert.True(list.Success);
        Assert.Single(list.Data!.Items);

        var mark = await h.InApp.MarkAsReadAsync(h.User.Id, list.Data.Items[0].Id);
        Assert.True(mark.Success);

        unread = await h.InApp.GetUnreadCountAsync(h.User.Id);
        Assert.Equal(0, unread.Data!.Count);
    }

    [Fact]
    public async Task Unauthorized_Mark_Returns_NotFound()
    {
        await using var h = await Harness.CreateAsync();

        await h.InApp.CreateSafeAsync(
            h.User.Id,
            "عنوان",
            "متن",
            InAppNotificationTypes.MessageRejected);

        var id = await h.Db.InAppNotifications.Select(n => n.Id).SingleAsync();
        var result = await h.InApp.MarkAsReadAsync(userId: 99999, id);
        Assert.False(result.Success);
        Assert.Equal(404, result.StatusCode);
    }

    private sealed class Harness : IAsyncDisposable
    {
        public required Api_Context Db { get; init; }
        public required User User { get; init; }
        public required InAppNotificationService InApp { get; init; }
        public required AdminTemplateApprovalService TemplateApprovals { get; init; }
        public required RecordingPushNotifier Push { get; init; }

        public static async Task<Harness> CreateAsync()
        {
            var options = new DbContextOptionsBuilder<Api_Context>()
                .UseInMemoryDatabase($"notif-{Guid.NewGuid():N}")
                .Options;

            var db = new Api_Context(options);
            await db.Database.EnsureCreatedAsync();

            var user = new User
            {
                PhoneNumber = "09120000000",
                FullName = "کاربر تست",
                CreatedAt = DateTime.UtcNow
            };
            db.Users.Add(user);
            await db.SaveChangesAsync();

            var inApp = new InAppNotificationService(db, NullLogger<InAppNotificationService>.Instance);
            var push = new RecordingPushNotifier();
            var appNotifier = new UserAppNotifier(inApp, push, NullLogger<UserAppNotifier>.Instance);
            var templates = new AdminTemplateApprovalService(
                db,
                new NoOpAuditService(),
                appNotifier,
                NullLogger<AdminTemplateApprovalService>.Instance);

            return new Harness
            {
                Db = db,
                User = user,
                InApp = inApp,
                TemplateApprovals = templates,
                Push = push
            };
        }

        public async ValueTask DisposeAsync() => await Db.DisposeAsync();
    }

    private sealed class RecordingPushNotifier : IUserPushNotifier
    {
        public int NotifyCalls { get; private set; }

        public Task NotifyAsync(
            int userId,
            NotificationCategory category,
            string title,
            string body,
            CancellationToken cancellationToken = default)
        {
            NotifyCalls++;
            return Task.CompletedTask;
        }

        public Task<int> NotifyBroadcastAsync(
            NotificationCategory category,
            string title,
            string body,
            CancellationToken cancellationToken = default) => Task.FromResult(0);
    }

    private sealed class NoOpAuditService : IAuditService
    {
        public Task WriteAsync(AuditEntry entry, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task WriteRangeAsync(IEnumerable<AuditEntry> entries, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
