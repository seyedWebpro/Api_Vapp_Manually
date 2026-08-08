using Api_Vapp.Constants;
using Api_Vapp.Models;
using Api_Vapp.Utilities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Api_Vapp.Tests.BookingSystem;

/// <summary>
/// تست منطق ارسال یادآوری نوبت (ProcessRemindersAsync) — چند offset + opt-out.
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
        var (systemId, serviceId) = await SeedPublishedSystemAsync(new[] { 60 });
        var appointment = await SeedConfirmedAppointmentAsync(
            systemId,
            serviceId,
            startUtc: DateTime.UtcNow.AddMinutes(30),
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
        Assert.Contains("60", reloaded.ReminderSentOffsetsCsv);
    }

    [Fact]
    public async Task ProcessReminders_MultipleOffsets_SendsEachDueOffsetSeparately()
    {
        var (systemId, serviceId) = await SeedPublishedSystemAsync(new[] { 60, 1440 });
        var appointment = await SeedConfirmedAppointmentAsync(
            systemId,
            serviceId,
            startUtc: DateTime.UtcNow.AddMinutes(30), // هر دو due (catch-up)
            mobile: "09392615526");

        _ctx.SmsBilling.Sent.Clear();
        await _ctx.AppointmentService.ProcessRemindersAsync();

        Assert.Equal(2, _ctx.SmsBilling.Sent.Count);
        var reloaded = await _ctx.Context.BookingAppointments.AsNoTracking()
            .FirstAsync(a => a.Id == appointment.Id);
        var sentOffsets = BookingReminderOffsetsHelper.ParseSentOffsets(reloaded.ReminderSentOffsetsCsv);
        Assert.Contains(60, sentOffsets);
        Assert.Contains(1440, sentOffsets);
    }

    [Fact]
    public async Task ProcessReminders_WhenNotYetDue_DoesNotSend()
    {
        var (systemId, serviceId) = await SeedPublishedSystemAsync(new[] { 60 });
        var appointment = await SeedConfirmedAppointmentAsync(
            systemId,
            serviceId,
            startUtc: DateTime.UtcNow.AddMinutes(90),
            mobile: "09920374397");

        _ctx.SmsBilling.Sent.Clear();
        await _ctx.AppointmentService.ProcessRemindersAsync();

        Assert.Empty(_ctx.SmsBilling.Sent);
        var reloaded = await _ctx.Context.BookingAppointments.AsNoTracking()
            .FirstAsync(a => a.Id == appointment.Id);
        Assert.Null(reloaded.ReminderSentAt);
    }

    [Fact]
    public async Task ProcessReminders_CustomerOptOut_DoesNotSend()
    {
        var (systemId, serviceId) = await SeedPublishedSystemAsync(new[] { 60 });
        var appointment = await SeedConfirmedAppointmentAsync(
            systemId,
            serviceId,
            startUtc: DateTime.UtcNow.AddMinutes(20),
            mobile: "09392615526",
            remindersEnabled: false);

        _ctx.SmsBilling.Sent.Clear();
        await _ctx.AppointmentService.ProcessRemindersAsync();

        Assert.Empty(_ctx.SmsBilling.Sent);
        Assert.False(appointment.RemindersEnabled);
    }

    [Fact]
    public async Task ProcessReminders_InsufficientWallet_DoesNotMarkSent_AndRetriesLater()
    {
        var (systemId, serviceId) = await SeedPublishedSystemAsync(new[] { 60 });
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
        _ctx.SmsBilling.Sent.Clear();
        await _ctx.AppointmentService.ProcessRemindersAsync();

        Assert.Contains(_ctx.SmsBilling.Sent, s => s.EntityId == appointment.Id);
        var done = await _ctx.Context.BookingAppointments.AsNoTracking()
            .FirstAsync(a => a.Id == appointment.Id);
        Assert.NotNull(done.ReminderSentAt);
    }

    [Fact]
    public async Task ProcessReminders_PendingAppointment_DoesNotSend()
    {
        var (systemId, serviceId) = await SeedPublishedSystemAsync(new[] { 60 });
        await SeedConfirmedAppointmentAsync(
            systemId,
            serviceId,
            startUtc: DateTime.UtcNow.AddMinutes(30),
            mobile: "09392615526",
            status: BookingAppointmentStatuses.Pending);

        _ctx.SmsBilling.Sent.Clear();
        await _ctx.AppointmentService.ProcessRemindersAsync();

        Assert.Empty(_ctx.SmsBilling.Sent);
    }

    [Fact]
    public void ReminderOffsetsHelper_Normalize_DedupesAndCaps()
    {
        var result = BookingReminderOffsetsHelper.Normalize(new[] { 60, 60, 1440, 120, 999999, 0, -1 });
        Assert.Equal(new[] { 60, 120, 1440 }, result);
        Assert.Equal(1440, BookingReminderOffsetsHelper.ResolveLegacySingle(result));
    }

    private async Task<(int SystemId, int ServiceId)> SeedPublishedSystemAsync(int[] offsets)
    {
        var (systemId, _) = await _ctx.CreateConfirmedSystemAsync();
        var service = await _ctx.Context.BookingServiceItems
            .FirstAsync(s => s.BookingSystemId == systemId && !s.IsDeleted);
        var normalized = BookingReminderOffsetsHelper.Normalize(offsets);
        service.ReminderOffsetsJson = BookingReminderOffsetsHelper.ToJson(normalized);
        service.ReminderOffsetMinutes = BookingReminderOffsetsHelper.ResolveLegacySingle(normalized);
        await _ctx.Context.SaveChangesAsync();
        return (systemId, service.Id);
    }

    private async Task<BookingAppointment> SeedConfirmedAppointmentAsync(
        int systemId,
        int serviceId,
        DateTime startUtc,
        string mobile,
        string status = BookingAppointmentStatuses.Confirmed,
        bool remindersEnabled = true)
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
            RemindersEnabled = remindersEnabled,
            CreatedAt = DateTime.UtcNow
        };
        _ctx.Context.BookingAppointments.Add(appointment);
        await _ctx.Context.SaveChangesAsync();
        return appointment;
    }
}
