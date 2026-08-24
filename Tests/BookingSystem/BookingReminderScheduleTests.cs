using Api_Vapp.Constants;
using Api_Vapp.Models;
using Api_Vapp.Utilities;
using Xunit;

namespace Api_Vapp.Tests.BookingSystem;

public class BookingReminderScheduleTests
{
    [Fact]
    public void BuildSchedule_FutureOffset_WillSendLater()
    {
        var now = DateTime.UtcNow;
        var start = now.AddMinutes(90);
        var result = BookingReminderOffsetsHelper.BuildSchedule(
            start,
            now,
            remindersEnabled: true,
            BookingAppointmentStatuses.Confirmed,
            new[] { 60 },
            new HashSet<int>());

        Assert.True(result.WillSend);
        Assert.Equal(new[] { 60 }, result.PendingOffsetsMinutes);
        Assert.Equal(start.AddMinutes(-60), result.NextReminderAtUtc);
        Assert.Null(result.SkipReasonCode);
    }

    [Fact]
    public void BuildSchedule_CatchUp_NextReminderIsNow()
    {
        var now = DateTime.UtcNow;
        var start = now.AddMinutes(30);
        var result = BookingReminderOffsetsHelper.BuildSchedule(
            start,
            now,
            remindersEnabled: true,
            BookingAppointmentStatuses.Confirmed,
            new[] { 60 },
            new HashSet<int>());

        Assert.True(result.WillSend);
        Assert.Equal(now, result.NextReminderAtUtc);
    }

    [Fact]
    public void BuildSchedule_PastSlot_DoesNotSend()
    {
        var now = DateTime.UtcNow;
        var result = BookingReminderOffsetsHelper.BuildSchedule(
            now.AddMinutes(-10),
            now,
            remindersEnabled: true,
            BookingAppointmentStatuses.Confirmed,
            new[] { 60 },
            new HashSet<int>());

        Assert.False(result.WillSend);
        Assert.Equal(BookingReminderSkipReasons.Past, result.SkipReasonCode);
    }

    [Fact]
    public void BuildSchedule_Disabled_DoesNotSend()
    {
        var now = DateTime.UtcNow;
        var result = BookingReminderOffsetsHelper.BuildSchedule(
            now.AddHours(2),
            now,
            remindersEnabled: false,
            BookingAppointmentStatuses.Confirmed,
            new[] { 60 },
            new HashSet<int>());

        Assert.False(result.WillSend);
        Assert.False(result.RemindersEnabled);
        Assert.Equal(BookingReminderSkipReasons.Disabled, result.SkipReasonCode);
    }

    [Fact]
    public void BuildSchedule_Pending_DoesNotSend()
    {
        var now = DateTime.UtcNow;
        var result = BookingReminderOffsetsHelper.BuildSchedule(
            now.AddHours(2),
            now,
            remindersEnabled: true,
            BookingAppointmentStatuses.Pending,
            new[] { 60 },
            new HashSet<int>());

        Assert.False(result.WillSend);
        Assert.Equal(BookingReminderSkipReasons.NotConfirmed, result.SkipReasonCode);
    }

    [Fact]
    public void ResolveSentOffsets_LegacySentAtWithoutCsv_MarksAllSent()
    {
        var offsets = new[] { 60, 1440 };
        var sent = BookingReminderOffsetsHelper.ResolveSentOffsets(
            csv: null,
            reminderSentAt: DateTime.UtcNow,
            offsets);

        Assert.Contains(60, sent);
        Assert.Contains(1440, sent);
    }
}
