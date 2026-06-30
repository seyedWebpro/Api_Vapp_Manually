using Api_Vapp.DTOs.BookingSystem;
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
        Assert.Equal(BookingAppointmentStatuses.Confirmed, result.Data!.Appointment.Status);
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
