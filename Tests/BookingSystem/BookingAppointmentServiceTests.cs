using Api_Vapp.DTOs.BookingSystem;
using Api_Vapp.DTOs.Common;
using Api_Vapp.Models;
using Xunit;

namespace Api_Vapp.Tests.BookingSystem;

public class BookingAppointmentServiceTests : IAsyncLifetime
{
    private BookingSystemTestContext _ctx = null!;

    public async Task InitializeAsync()
    {
        _ctx = await BookingSystemTestContext.CreateAsync();
    }

    public Task DisposeAsync()
    {
        _ctx.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task GetPublicSystem_ValidSlug_Returns200()
    {
        var (_, _) = await _ctx.CreateConfirmedSystemAsync(s => s.CustomSlug = $"pub-{Guid.NewGuid():N}"[..15]);
        var slug = (await _ctx.SystemService.GetSystemsAsync(_ctx.OwnerUserId, 1, 10, null)).Data!.Systems.First().Slug;

        var result = await _ctx.AppointmentService.GetPublicSystemAsync(slug);

        BookingApiAssertions.AssertSuccess(result);
        Assert.NotEmpty(result.Data!.Services);
    }

    [Fact]
    public async Task GetAvailableSlots_FutureDate_Returns200()
    {
        var (systemId, _) = await _ctx.CreateConfirmedSystemAsync();
        var system = await _ctx.SystemService.GetByIdAsync(systemId, _ctx.OwnerUserId);
        var serviceId = system.Data!.Services.First().Id;
        var slug = system.Data.Slug;
        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7));

        var result = await _ctx.AppointmentService.GetAvailableSlotsAsync(slug, serviceId, date);

        BookingApiAssertions.AssertSuccess(result);
        Assert.NotEmpty(result.Data!.Slots);
    }

    [Fact]
    public async Task GetAvailableSlots_DateOutsidePublicWindow_Returns400()
    {
        var (systemId, _) = await _ctx.CreateConfirmedSystemAsync();
        var system = await _ctx.SystemService.GetByIdAsync(systemId, _ctx.OwnerUserId);
        var serviceId = system.Data!.Services.First().Id;
        var slug = system.Data.Slug;
        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(31));

        var result = await _ctx.AppointmentService.GetAvailableSlotsAsync(slug, serviceId, date);

        BookingApiAssertions.AssertFailure(result, 400);
        Assert.Equal(ErrorCodes.InvalidInput, result.ErrorCode);
        Assert.Contains("30", result.Message);
    }

    [Fact]
    public async Task CreatePublicBooking_ValidSlot_Returns201()
    {
        var (systemId, _) = await _ctx.CreateConfirmedSystemAsync();
        var system = await _ctx.SystemService.GetByIdAsync(systemId, _ctx.OwnerUserId);
        var serviceId = system.Data!.Services.First().Id;
        var slug = system.Data.Slug;
        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7));

        var slots = await _ctx.AppointmentService.GetAvailableSlotsAsync(slug, serviceId, date);
        var startUtc = slots.Data!.Slots.First().StartUtc;

        var result = await _ctx.AppointmentService.CreatePublicBookingAsync(slug, new CreatePublicBookingDto
        {
            ServiceId = serviceId,
            StartUtc = startUtc,
            CustomerFullName = "علی تست",
            CustomerMobile = "09123456789"
        });

        BookingApiAssertions.AssertSuccess(result, 201);
        Assert.Equal(BookingAppointmentStatuses.Pending, result.Data!.Appointment.Status);
    }

    [Fact]
    public async Task CreatePublicBooking_DateOutsidePublicWindow_Returns400()
    {
        var (systemId, _) = await _ctx.CreateConfirmedSystemAsync();
        var system = await _ctx.SystemService.GetByIdAsync(systemId, _ctx.OwnerUserId);
        var serviceId = system.Data!.Services.First().Id;
        var slug = system.Data.Slug;
        var outOfWindowStartUtc = DateTime.UtcNow.Date.AddDays(31).AddHours(8);

        var result = await _ctx.AppointmentService.CreatePublicBookingAsync(slug, new CreatePublicBookingDto
        {
            ServiceId = serviceId,
            StartUtc = outOfWindowStartUtc,
            CustomerFullName = "علی تست",
            CustomerMobile = "09123456789"
        });

        BookingApiAssertions.AssertFailure(result, 400);
        Assert.Equal(ErrorCodes.InvalidInput, result.ErrorCode);
        Assert.Contains("30", result.Message);
    }

    [Fact]
    public async Task LookupPublicBookingStatus_Valid_ReturnsPending()
    {
        var (systemId, _) = await _ctx.CreateConfirmedSystemAsync();
        var system = await _ctx.SystemService.GetByIdAsync(systemId, _ctx.OwnerUserId);
        var serviceId = system.Data!.Services.First().Id;
        var slug = system.Data.Slug;
        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7));
        var startUtc = (await _ctx.AppointmentService.GetAvailableSlotsAsync(slug, serviceId, date)).Data!.Slots.First().StartUtc;

        var created = await _ctx.AppointmentService.CreatePublicBookingAsync(slug, new CreatePublicBookingDto
        {
            ServiceId = serviceId,
            StartUtc = startUtc,
            CustomerFullName = "علی تست",
            CustomerMobile = "09123456789"
        });

        var lookup = await _ctx.AppointmentService.LookupPublicBookingStatusAsync(slug, new LookupPublicBookingDto
        {
            AppointmentNumber = created.Data!.Appointment.AppointmentNumber,
            CustomerMobile = "09123456789"
        });

        BookingApiAssertions.AssertSuccess(lookup);
        Assert.Equal(BookingAppointmentStatuses.Pending, lookup.Data!.Status);
        Assert.Equal("در انتظار تأیید", lookup.Data.StatusTitle);
        Assert.Equal("0912***6789", lookup.Data.CustomerMobileMasked);
    }

    [Fact]
    public async Task LookupPublicBookingStatus_WrongMobile_Returns404()
    {
        var (systemId, _) = await _ctx.CreateConfirmedSystemAsync();
        var appointmentId = await BookSampleAsync(systemId);
        var slug = (await _ctx.SystemService.GetByIdAsync(systemId, _ctx.OwnerUserId)).Data!.Slug;

        var lookup = await _ctx.AppointmentService.LookupPublicBookingStatusAsync(slug, new LookupPublicBookingDto
        {
            AppointmentNumber = appointmentId,
            CustomerMobile = "09120000000"
        });

        BookingApiAssertions.AssertFailure(lookup, 404);
    }

    [Fact]
    public async Task CreatePublicBooking_DuplicateSlot_Returns400()
    {
        var (systemId, _) = await _ctx.CreateConfirmedSystemAsync();
        var system = await _ctx.SystemService.GetByIdAsync(systemId, _ctx.OwnerUserId);
        var serviceId = system.Data!.Services.First().Id;
        var slug = system.Data.Slug;
        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7));
        var startUtc = (await _ctx.AppointmentService.GetAvailableSlotsAsync(slug, serviceId, date)).Data!.Slots.First().StartUtc;

        var dto = new CreatePublicBookingDto
        {
            ServiceId = serviceId,
            StartUtc = startUtc,
            CustomerFullName = "اول",
            CustomerMobile = "09121111111"
        };

        await _ctx.AppointmentService.CreatePublicBookingAsync(slug, dto);
        var second = await _ctx.AppointmentService.CreatePublicBookingAsync(slug, dto);

        BookingApiAssertions.AssertFailure(second, 400);
    }

    [Fact]
    public async Task GetAppointments_Owner_Returns200()
    {
        var (systemId, _) = await _ctx.CreateConfirmedSystemAsync();
        await BookSampleAsync(systemId);

        var result = await _ctx.AppointmentService.GetAppointmentsAsync(
            systemId, _ctx.OwnerUserId, 1, 10, null, null, null, null);

        BookingApiAssertions.AssertSuccess(result);
        Assert.NotEmpty(result.Data!.Appointments);
    }

    [Fact]
    public async Task CancelAppointment_Confirmed_Returns200()
    {
        var (systemId, _) = await _ctx.CreateConfirmedSystemAsync();
        var appointmentId = await BookSampleAsync(systemId);

        var result = await _ctx.AppointmentService.CancelAppointmentAsync(
            systemId, appointmentId, _ctx.OwnerUserId, new CancelBookingAppointmentDto { Reason = "تست" });

        BookingApiAssertions.AssertSuccess(result);
        Assert.Equal(BookingAppointmentStatuses.Cancelled, result.Data!.Status);
    }

    [Fact]
    public async Task CancelledAppointment_SlotBecomesAvailableAgain()
    {
        var (systemId, _) = await _ctx.CreateConfirmedSystemAsync();
        var system = await _ctx.SystemService.GetByIdAsync(systemId, _ctx.OwnerUserId);
        var serviceId = system.Data!.Services.First().Id;
        var slug = system.Data.Slug;
        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7));
        var startUtc = (await _ctx.AppointmentService.GetAvailableSlotsAsync(slug, serviceId, date)).Data!.Slots.First().StartUtc;

        var book = await _ctx.AppointmentService.CreatePublicBookingAsync(slug, new CreatePublicBookingDto
        {
            ServiceId = serviceId,
            StartUtc = startUtc,
            CustomerFullName = "مشتری",
            CustomerMobile = "09123333333"
        });
        var appointmentId = book.Data!.Appointment.Id;

        await _ctx.AppointmentService.CancelAppointmentAsync(
            systemId, appointmentId, _ctx.OwnerUserId, null);

        var slotsAfterCancel = await _ctx.AppointmentService.GetAvailableSlotsAsync(slug, serviceId, date);
        Assert.Contains(slotsAfterCancel.Data!.Slots, s => s.StartUtc == startUtc);
    }

    [Fact]
    public async Task UpdateAppointment_ChangeNameAndTime_Returns200()
    {
        var (systemId, _) = await _ctx.CreateConfirmedSystemAsync();
        var system = await _ctx.SystemService.GetByIdAsync(systemId, _ctx.OwnerUserId);
        var serviceId = system.Data!.Services.First().Id;
        var slug = system.Data.Slug;
        List<BookingTimeSlotDto> slots = new();
        for (var i = 1; i <= 14; i++)
        {
            var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(i));
            slots = (await _ctx.AppointmentService.GetAvailableSlotsAsync(slug, serviceId, date)).Data!.Slots;
            if (slots.Count >= 2)
            {
                break;
            }
        }

        Assert.True(slots.Count >= 2);

        var manual = await _ctx.AppointmentService.CreateManualBookingAsync(systemId, _ctx.OwnerUserId, new CreateManualBookingDto
        {
            ServiceId = serviceId,
            StartUtc = slots[0].StartUtc,
            CustomerFullName = "قبل از ویرایش",
            CustomerMobile = "09125555555"
        });
        BookingApiAssertions.AssertSuccess(manual, 201);

        var result = await _ctx.AppointmentService.UpdateAppointmentAsync(
            systemId,
            manual.Data!.Id,
            _ctx.OwnerUserId,
            new UpdateBookingAppointmentDto
            {
                CustomerFullName = "kkk",
                CustomerMobile = "09917032705",
                CustomerNote = null,
                ServiceId = serviceId,
                StartUtc = slots[1].StartUtc
            });

        BookingApiAssertions.AssertSuccess(result);
        Assert.Equal("kkk", result.Data!.CustomerFullName);
        Assert.Equal("09917032705", result.Data.CustomerMobile);
        Assert.Equal(slots[1].StartUtc, result.Data.StartUtc);
        Assert.Equal(serviceId, result.Data.ServiceId);
        Assert.False(string.IsNullOrEmpty(result.Data.ServiceTitle));
    }

    [Fact]
    public async Task ConfirmAppointment_Pending_ReturnsConfirmed()
    {
        var (systemId, _) = await _ctx.CreateConfirmedSystemAsync();
        var appointmentId = await BookSampleAsync(systemId);

        var result = await _ctx.AppointmentService.ConfirmAppointmentAsync(
            systemId, appointmentId, _ctx.OwnerUserId);

        BookingApiAssertions.AssertSuccess(result);
        Assert.Equal(BookingAppointmentStatuses.Confirmed, result.Data!.Status);
    }

    [Fact]
    public async Task GetDashboard_ReturnsStats()
    {
        var (systemId, _) = await _ctx.CreateConfirmedSystemAsync();
        await BookSampleAsync(systemId);

        var result = await _ctx.AppointmentService.GetDashboardAsync(systemId, _ctx.OwnerUserId);

        BookingApiAssertions.AssertSuccess(result);
        Assert.Equal(systemId, result.Data!.SystemId);
    }

    [Fact]
    public async Task CreateManualBooking_ReturnsConfirmed()
    {
        var (systemId, _) = await _ctx.CreateConfirmedSystemAsync();
        var system = await _ctx.SystemService.GetByIdAsync(systemId, _ctx.OwnerUserId);
        var serviceId = system.Data!.Services.First().Id;
        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(8));
        var slug = system.Data.Slug;
        var startUtc = (await _ctx.AppointmentService.GetAvailableSlotsAsync(slug, serviceId, date)).Data!.Slots.First().StartUtc;

        var result = await _ctx.AppointmentService.CreateManualBookingAsync(systemId, _ctx.OwnerUserId, new CreateManualBookingDto
        {
            ServiceId = serviceId,
            StartUtc = startUtc,
            CustomerFullName = "رزرو دستی",
            CustomerMobile = "09124444444",
            CustomerNote = "توضیح"
        });

        BookingApiAssertions.AssertSuccess(result, 201);
        Assert.Equal(BookingAppointmentStatuses.Confirmed, result.Data!.Status);
        Assert.Equal("توضیح", result.Data.CustomerNote);
    }

    [Fact]
    public async Task SaveDayAvailability_BlocksSlot()
    {
        var (systemId, _) = await _ctx.CreateConfirmedSystemAsync();
        var system = await _ctx.SystemService.GetByIdAsync(systemId, _ctx.OwnerUserId);
        var serviceId = system.Data!.Services.First().Id;
        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(9));
        var availability = await _ctx.AppointmentService.GetDayAvailabilityAsync(systemId, _ctx.OwnerUserId, date, serviceId);
        var slot = availability.Data!.Slots.First(s => s.Status == BookingManagedSlotStatuses.Empty);

        var save = await _ctx.AppointmentService.SaveDayAvailabilityAsync(systemId, _ctx.OwnerUserId, new SaveBookingDayAvailabilityDto
        {
            Date = date,
            ServiceId = serviceId,
            Slots = new List<BookingSlotToggleDto>
            {
                new() { StartUtc = slot.StartUtc, IsEnabled = false }
            }
        });

        BookingApiAssertions.AssertSuccess(save);
        Assert.Contains(save.Data!.Slots, s => s.StartUtc == slot.StartUtc && s.Status == BookingManagedSlotStatuses.Blocked);

        var publicSlots = await _ctx.AppointmentService.GetAvailableSlotsAsync(system.Data.Slug, serviceId, date);
        Assert.DoesNotContain(publicSlots.Data!.Slots, s => s.StartUtc == slot.StartUtc);
    }

    private async Task<int> BookSampleAsync(int systemId)
    {
        var system = await _ctx.SystemService.GetByIdAsync(systemId, _ctx.OwnerUserId);
        var serviceId = system.Data!.Services.First().Id;
        var slug = system.Data.Slug;
        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7));
        var startUtc = (await _ctx.AppointmentService.GetAvailableSlotsAsync(slug, serviceId, date)).Data!.Slots.First().StartUtc;

        var book = await _ctx.AppointmentService.CreatePublicBookingAsync(slug, new CreatePublicBookingDto
        {
            ServiceId = serviceId,
            StartUtc = startUtc,
            CustomerFullName = "مشتری",
            CustomerMobile = "09122222222"
        });

        return book.Data!.Appointment.Id;
    }
}
