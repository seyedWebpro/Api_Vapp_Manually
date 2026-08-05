using Api_Vapp.Constants;
using Api_Vapp.DTOs.BookingSystem;
using Api_Vapp.Models;
using Api_Vapp.Utilities;
using Xunit;

namespace Api_Vapp.Tests.BookingSystem;

public class BookingSystemServiceTests : IAsyncLifetime
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
    public async Task ValidateStep1_ValidRequest_Returns200WithDraftId()
    {
        var result = await _ctx.Service.ValidateStep1Async(_ctx.OwnerUserId, _ctx.BuildStep1Dto());

        BookingApiAssertions.AssertSuccess(result);
        Assert.NotNull(result.Data?.DraftId);
    }

    [Fact]
    public async Task ValidateStep1_EmptyTitle_Returns400()
    {
        var result = await _ctx.Service.ValidateStep1Async(
            _ctx.OwnerUserId,
            _ctx.BuildStep1Dto(s => s.Title = "   "));

        BookingApiAssertions.AssertFailure(result, 400);
    }

    [Fact]
    public async Task ValidateStep1_SaveToPhonebookWithoutNotebook_Returns400()
    {
        var result = await _ctx.Service.ValidateStep1Async(
            _ctx.OwnerUserId,
            _ctx.BuildStep1Dto(s =>
            {
                s.SaveToPhonebook = true;
                s.NotebookIds = new List<int>();
            }));

        BookingApiAssertions.AssertFailure(result, 400);
    }

    [Fact]
    public async Task ValidateStep2_NoServices_Returns400()
    {
        var step1 = await _ctx.Service.ValidateStep1Async(_ctx.OwnerUserId, _ctx.BuildStep1Dto());
        var draftId = step1.Data!.DraftId!;

        var result = await _ctx.Service.ValidateStep2Async(_ctx.OwnerUserId, new BookingStep2Dto
        {
            DraftId = draftId,
            Services = new List<BookingServiceDraftDto>()
        });

        BookingApiAssertions.AssertFailure(result, 400);
    }

    [Fact]
    public async Task Confirm_FullWizard_Returns201WithPublicUrl()
    {
        var (systemId, publicUrl) = await _ctx.CreateConfirmedSystemAsync();

        Assert.True(systemId > 0);
        Assert.StartsWith("https://app.com/book/", publicUrl);

        var getResult = await _ctx.Service.GetByIdAsync(systemId, _ctx.OwnerUserId);
        BookingApiAssertions.AssertSuccess(getResult);
        Assert.Single(getResult.Data!.Services);
        Assert.Equal(10, getResult.Data.Services[0].BufferMinutesBetweenAppointments);
        Assert.Equal(1440, getResult.Data.Services[0].ReminderOffsetMinutes);
    }

    [Fact]
    public async Task Confirm_WithCustomSlug_Returns201()
    {
        var slug = $"salon-{Guid.NewGuid():N}"[..20];
        var (systemId, publicUrl) = await _ctx.CreateConfirmedSystemAsync(s =>
        {
            s.CustomSlug = slug;
        });

        Assert.Contains(slug, publicUrl);

        var getResult = await _ctx.Service.GetByIdAsync(systemId, _ctx.OwnerUserId);
        BookingApiAssertions.AssertSuccess(getResult);
        Assert.Equal(slug, getResult.Data!.Slug);
    }

    [Fact]
    public async Task GetSystems_Returns200()
    {
        await _ctx.CreateConfirmedSystemAsync();

        var result = await _ctx.Service.GetSystemsAsync(_ctx.OwnerUserId, 1, 10, null);

        BookingApiAssertions.AssertSuccess(result);
        Assert.NotEmpty(result.Data!.Systems);
    }

    [Fact]
    public async Task ToggleStatus_Returns200()
    {
        var (systemId, _) = await _ctx.CreateConfirmedSystemAsync();

        var result = await _ctx.Service.ToggleStatusAsync(systemId, _ctx.OwnerUserId);

        BookingApiAssertions.AssertSuccess(result);
        Assert.False(result.Data!.IsActive);
    }

    [Fact]
    public async Task Delete_Returns200()
    {
        var (systemId, _) = await _ctx.CreateConfirmedSystemAsync();

        var result = await _ctx.Service.DeleteAsync(systemId, _ctx.OwnerUserId);

        BookingApiAssertions.AssertSuccess(result);
    }

    [Fact]
    public async Task GetActivityTypes_Returns200()
    {
        var result = await _ctx.Service.GetActivityTypesAsync();

        BookingApiAssertions.AssertSuccess(result);
        Assert.Contains(result.Data!, t => t.Code == BookingActivityTypes.BeautySalon);
    }

    [Fact]
    public async Task AddService_WithPriceAndOptionalDeposit_Returns201WithoutServiceCost()
    {
        var (systemId, _) = await _ctx.CreateConfirmedSystemAsync();

        var result = await _ctx.Service.AddServiceAsync(systemId, _ctx.OwnerUserId, new AddBookingServiceDto
        {
            Title = "رنگ مو",
            DurationMinutes = 90,
            HasCost = true,
            Price = 800_000m,
            DepositAmount = 200_000m,
            BufferMinutesBetweenAppointments = 10,
            ReminderOffsetMinutes = 60,
            WeeklyDays = BookingSystemTestContext.DefaultWeeklySchedule()
        });

        BookingApiAssertions.AssertSuccess(result, 201);
        Assert.True(result.Data!.HasCost);
        Assert.Equal(800_000m, result.Data.Price);
        Assert.Equal(200_000m, result.Data.DepositAmount);
    }

    [Fact]
    public async Task ValidateStep2_DepositGreaterThanPrice_Returns400()
    {
        var step1 = await _ctx.Service.ValidateStep1Async(_ctx.OwnerUserId, _ctx.BuildStep1Dto());
        var draftId = step1.Data!.DraftId!;

        var result = await _ctx.Service.ValidateStep2Async(_ctx.OwnerUserId, new BookingStep2Dto
        {
            DraftId = draftId,
            Services = new List<BookingServiceDraftDto>
            {
                new()
                {
                    ServiceTempId = Guid.NewGuid().ToString("N"),
                    Title = "خدمت تست",
                    DurationMinutes = 45,
                    HasCost = true,
                    Price = 100_000m,
                    DepositAmount = 150_000m
                }
            }
        });

        BookingApiAssertions.AssertFailure(result, 400);
        Assert.Contains(result.Errors!, e => e.Contains("بیعانه"));
    }

    [Fact]
    public async Task Confirm_ServiceHasOnlyPrice_NotServiceCost()
    {
        var (systemId, _) = await _ctx.CreateConfirmedSystemAsync();
        var getResult = await _ctx.Service.GetByIdAsync(systemId, _ctx.OwnerUserId);

        BookingApiAssertions.AssertSuccess(getResult);
        var service = Assert.Single(getResult.Data!.Services);
        Assert.Equal(500_000m, service.Price);
        Assert.Null(service.DepositAmount);
    }
}
