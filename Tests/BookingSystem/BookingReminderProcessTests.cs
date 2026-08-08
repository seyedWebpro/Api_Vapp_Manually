using Api_Vapp.Constants;
using Api_Vapp.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Api_Vapp.Tests.BookingSystem;

/// <summary>
/// تست منطق ارسال یادآوری نوبت (ProcessRemindersAsync).
/// </summary>
public class BookingReminderProcessTests : IAsyncLifetime
{
    private BookingSystemTestContext _ctx = null!;

    public async Task InitializeAsync() => _ctx = await BookingSystemTestContext.CreateAsync();

    public Task DisposeAsync()
    {
        _ctx.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task ProcessReminders_WhenDue_SendsSmsAndMarksReminderSentAt()
    {
        var (systemId, serviceId) = await SeedPublishedSystemAsync(reminderOffsetMinutes: 60);
        var appointment = await SeedConfirmedAppointmentAsync(
            systemId,
            serviceId,
            startUtc: DateTime.UtcNow.AddMinutes(30), // reminderAt = now-30 → due
            mobile: "09392615526");

        _ctx.SmsBilling.Sent.Clear();
        await _ctx.AppointmentService.ProcessRemindersAsync();

        Assert.Single(_ctx.SmsBilling.Sent);
        var sent = _ctx.SmsBilling.Sent[0];
        Assert.Equal(SmsSourceModules.BookingReminder, sent.SourceModule);
        Assert.Equal("09392615526", sent.Mobile);
        Assert.Contains("یادآوری نوبت", sent.Message);
        Assert.Contains("لغو11", sent.Message);
        Assert.Equal(appointment.Id, sent.EntityId);

        var reloaded = await _ctx.Context.BookingAppointments.AsNoTracking()
            .FirstAsync(a => a.Id == appointment.Id);
        Assert.NotNull(reloaded.ReminderSentAt);
    }

    [Fact]
    public async Task ProcessReminders_WhenNotYetDue_DoesNotSend()
    {
        var (systemId, serviceId) = await SeedPublishedSystemAsync(reminderOffsetMinutes: 60);
        var appointment = await SeedConfirmedAppointmentAsync(
            systemId,
            serviceId,
            startUtc: DateTime.UtcNow.AddMinutes(90), // reminderAt = now+30 → not due
            mobile: "09920374397");

        _ctx.SmsBilling.Sent.Clear();
        await _ctx.AppointmentService.ProcessRemindersAsync();

        Assert.Empty(_ctx.SmsBilling.Sent);
        var reloaded = await _ctx.Context.BookingAppointments.AsNoTracking()
            .FirstAsync(a => a.Id == appointment.Id);
        Assert.Null(reloaded.ReminderSentAt);
    }

    [Fact]
    public async Task ProcessReminders_CatchUp_AfterMissedWindow_StillSends()
    {
        // قبلاً فقط پنجرهٔ ۲ دقیقه‌ای قبول می‌شد؛ الان catch-up تا قبل از StartUtc
        var (systemId, serviceId) = await SeedPublishedSystemAsync(reminderOffsetMinutes: 60);
        var appointment = await SeedConfirmedAppointmentAsync(
            systemId,
            serviceId,
            startUtc: DateTime.UtcNow.AddMinutes(10), // reminderAt = now-50 (خیلی گذشته)
            mobile: "09392615526");

        _ctx.SmsBilling.Sent.Clear();
        await _ctx.AppointmentService.ProcessRemindersAsync();

        Assert.Single(_ctx.SmsBilling.Sent);
        var reloaded = await _ctx.Context.BookingAppointments.AsNoTracking()
            .FirstAsync(a => a.Id == appointment.Id);
        Assert.NotNull(reloaded.ReminderSentAt);
    }

    [Fact]
    public async Task ProcessReminders_InsufficientWallet_DoesNotMarkSent_AndRetriesLater()
    {
        var (systemId, serviceId) = await SeedPublishedSystemAsync(reminderOffsetMinutes: 60);
        var appointment = await SeedConfirmedAppointmentAsync(
            systemId,
            serviceId,
            startUtc: DateTime.UtcNow.AddMinutes(20),
            mobile: "09920374397");

        _ctx.SmsBilling.ForceInsufficientBalance = true;
        _ctx.SmsBilling.Sent.Clear();
        await _ctx.AppointmentService.ProcessRemindersAsync();

        Assert.Empty(_ctx.SmsBilling.Sent);
        var mid = await _ctx.Context.BookingAppointments.AsNoTracking()
            .FirstAsync(a => a.Id == appointment.Id);
        Assert.Null(mid.ReminderSentAt);

        _ctx.SmsBilling.ForceInsufficientBalance = false;
        await _ctx.AppointmentService.ProcessRemindersAsync();

        Assert.Single(_ctx.SmsBilling.Sent);
        var done = await _ctx.Context.BookingAppointments.AsNoTracking()
            .FirstAsync(a => a.Id == appointment.Id);
        Assert.NotNull(done.ReminderSentAt);
    }

    [Fact]
    public async Task ProcessReminders_PendingAppointment_DoesNotSend()
    {
        var (systemId, serviceId) = await SeedPublishedSystemAsync(reminderOffsetMinutes: 60);
        var appointment = await SeedConfirmedAppointmentAsync(
            systemId,
            serviceId,
            startUtc: DateTime.UtcNow.AddMinutes(30),
            mobile: "09392615526",
            status: BookingAppointmentStatuses.Pending);

        _ctx.SmsBilling.Sent.Clear();
        await _ctx.AppointmentService.ProcessRemindersAsync();

        Assert.Empty(_ctx.SmsBilling.Sent);
        Assert.Null(appointment.ReminderSentAt);
    }

    private async Task<(int SystemId, int ServiceId)> SeedPublishedSystemAsync(int reminderOffsetMinutes)
    {
        var (systemId, _) = await _ctx.CreateConfirmedSystemAsync();
        var service = await _ctx.Context.BookingServiceItems
            .FirstAsync(s => s.BookingSystemId == systemId && !s.IsDeleted);
        service.ReminderOffsetMinutes = reminderOffsetMinutes;
        await _ctx.Context.SaveChangesAsync();
        return (systemId, service.Id);
    }

    private async Task<BookingAppointment> SeedConfirmedAppointmentAsync(
        int systemId,
        int serviceId,
        DateTime startUtc,
        string mobile,
        string status = BookingAppointmentStatuses.Confirmed)
    {
        var service = await _ctx.Context.BookingServiceItems.FirstAsync(s => s.Id == serviceId);
        var appointment = new BookingAppointment
        {
            BookingSystemId = systemId,
            BookingServiceItemId = serviceId,
            CustomerFullName = "تست یادآوری",
            CustomerMobile = mobile,
            StartUtc = DateTime.SpecifyKind(startUtc, DateTimeKind.Utc),
            EndUtc = DateTime.SpecifyKind(startUtc.AddMinutes(service.DurationMinutes), DateTimeKind.Utc),
            Status = status,
            CreatedAt = DateTime.UtcNow
        };
        _ctx.Context.BookingAppointments.Add(appointment);
        await _ctx.Context.SaveChangesAsync();
        return appointment;
    }
}
