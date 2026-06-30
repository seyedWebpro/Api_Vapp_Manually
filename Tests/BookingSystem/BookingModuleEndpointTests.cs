using Api_Vapp.Constants;
using Api_Vapp.DTOs.BookingSystem;
using Api_Vapp.DTOs.Common;
using Api_Vapp.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Api_Vapp.Tests.BookingSystem;

/// <summary>
/// پوشش endpointهای BookingSystem و BookingPublic برای بررسی Swagger/فرانت.
/// هر تست: statusCode مورد انتظار + بدون 500 + پیام فارسی کنترل‌شده.
/// </summary>
public class BookingModuleEndpointTests : IAsyncLifetime
{
    private BookingSystemTestContext _ctx = null!;

    public async Task InitializeAsync() => _ctx = await BookingSystemTestContext.CreateAsync();

    public Task DisposeAsync()
    {
        _ctx.Dispose();
        return Task.CompletedTask;
    }

    // ─── GET /api/BookingSystem/activity-types ─────────────────────

    [Fact]
    public async Task Endpoint_GetActivityTypes_Returns200()
    {
        var result = await _ctx.Service.GetActivityTypesAsync();
        BookingApiAssertions.AssertSuccess(result);
        Assert.NotEmpty(result.Data!);
    }

    // ─── GET /api/BookingSystem/notebooks ──────────────────────────

    [Fact]
    public async Task Endpoint_GetNotebooks_Returns200WithOwnerNotebook()
    {
        var result = await _ctx.Service.GetNotebooksAsync(_ctx.OwnerUserId);
        BookingApiAssertions.AssertSuccess(result);
        Assert.Contains(result.Data!, n => n.Id == _ctx.NotebookId);
    }

    // ─── POST validate-step1 ─────────────────────────────────────────

    [Fact]
    public async Task Endpoint_ValidateStep1_HappyPath_Returns200WithDraftId()
    {
        var result = await _ctx.Service.ValidateStep1Async(_ctx.OwnerUserId, _ctx.BuildStep1Dto());
        BookingApiAssertions.AssertSuccess(result);
        Assert.NotNull(result.Data!.DraftId);
    }

    [Fact]
    public async Task Endpoint_ValidateStep1_InvalidActivityType_Returns400()
    {
        var result = await _ctx.Service.ValidateStep1Async(
            _ctx.OwnerUserId,
            _ctx.BuildStep1Dto(s => s.ActivityType = "invalid_type"));
        BookingApiAssertions.AssertFailure(result, 400);
    }

    [Fact]
    public async Task Endpoint_ValidateStep1_InvalidSlug_Returns400()
    {
        var result = await _ctx.Service.ValidateStep1Async(
            _ctx.OwnerUserId,
            _ctx.BuildStep1Dto(s => s.CustomSlug = "slug with spaces"));
        BookingApiAssertions.AssertFailure(result, 400);
    }

    [Fact]
    public async Task Endpoint_ValidateStep1_DuplicateTitle_Returns400()
    {
        var title = $"تکراری {Guid.NewGuid():N}"[..20];
        await _ctx.CreateConfirmedSystemAsync(s => s.Title = title);

        var result = await _ctx.Service.ValidateStep1Async(
            _ctx.OwnerUserId,
            _ctx.BuildStep1Dto(s => s.Title = title));
        BookingApiAssertions.AssertFailure(result, 400);
    }

    // ─── POST validate-step2/3/4 ─────────────────────────────────────

    [Fact]
    public async Task Endpoint_ValidateStep2_MissingDraftId_Returns400()
    {
        var result = await _ctx.Service.ValidateStep2Async(_ctx.OwnerUserId, new BookingStep2Dto
        {
            DraftId = "",
            Services = new List<BookingServiceDraftDto>
            {
                new() { ServiceTempId = "x", Title = "t", DurationMinutes = 30 }
            }
        });
        BookingApiAssertions.AssertFailure(result, 400);
    }

    [Fact]
    public async Task Endpoint_ValidateStep3_MissingServiceSchedule_Returns400()
    {
        var step1 = await _ctx.Service.ValidateStep1Async(_ctx.OwnerUserId, _ctx.BuildStep1Dto());
        var draftId = step1.Data!.DraftId!;
        var serviceTempId = Guid.NewGuid().ToString("N");

        await _ctx.Service.ValidateStep2Async(_ctx.OwnerUserId, new BookingStep2Dto
        {
            DraftId = draftId,
            Services = new List<BookingServiceDraftDto>
            {
                new() { ServiceTempId = serviceTempId, Title = "t", DurationMinutes = 30 }
            }
        });

        var result = await _ctx.Service.ValidateStep3Async(_ctx.OwnerUserId, new BookingStep3Dto
        {
            DraftId = draftId,
            ServiceSchedules = new List<BookingServiceScheduleDraftDto>()
        });
        BookingApiAssertions.AssertFailure(result, 400);
    }

    [Fact]
    public async Task Endpoint_ValidateStep4_MissingSettings_Returns400()
    {
        var (draftId, serviceTempId) = await _ctx.RunWizardThroughStep4Async();
        _ = serviceTempId;

        var result = await _ctx.Service.ValidateStep4Async(_ctx.OwnerUserId, new BookingStep4Dto
        {
            DraftId = draftId,
            ServiceSettings = new List<BookingServiceReminderDraftDto>()
        });
        BookingApiAssertions.AssertFailure(result, 400);
    }

    // ─── GET summary + POST confirm ──────────────────────────────────

    [Fact]
    public async Task Endpoint_GetSummary_CompleteDraft_Returns200()
    {
        var (draftId, _) = await _ctx.RunWizardThroughStep4Async();
        var result = await _ctx.Service.GetSummaryAsync(_ctx.OwnerUserId, draftId);
        BookingApiAssertions.AssertSuccess(result);
        Assert.NotNull(result.Data!.PublicUrlPreview);
    }

    [Fact]
    public async Task Endpoint_Confirm_InvalidDraftId_Returns400()
    {
        var result = await _ctx.Service.ConfirmAsync(_ctx.OwnerUserId, new ConfirmBookingSystemDto
        {
            DraftId = "non-existent-draft"
        });
        BookingApiAssertions.AssertFailure(result, 400);
    }

    [Fact]
    public async Task Endpoint_Confirm_IncompleteDraft_Returns400()
    {
        var step1 = await _ctx.Service.ValidateStep1Async(_ctx.OwnerUserId, _ctx.BuildStep1Dto());
        var result = await _ctx.Service.ConfirmAsync(_ctx.OwnerUserId, new ConfirmBookingSystemDto
        {
            DraftId = step1.Data!.DraftId!
        });
        BookingApiAssertions.AssertFailure(result, 400);
    }

    [Fact]
    public async Task Endpoint_Confirm_FullWizard_Returns201()
    {
        var result = await _ctx.Service.ConfirmAsync(_ctx.OwnerUserId, new ConfirmBookingSystemDto
        {
            DraftId = (await _ctx.RunWizardThroughStep4Async()).DraftId
        });
        BookingApiAssertions.AssertSuccess(result, 201);
        Assert.StartsWith("https://app.com/book/", result.Data!.System.PublicUrl);
    }

    // ─── GET / + GET /{id} ───────────────────────────────────────────

    [Fact]
    public async Task Endpoint_GetSystems_Returns200WithPagination()
    {
        await _ctx.CreateConfirmedSystemAsync();
        var result = await _ctx.Service.GetSystemsAsync(_ctx.OwnerUserId, 1, 10, null);
        BookingApiAssertions.AssertSuccess(result);
        Assert.True(result.Data!.TotalCount >= 1);
    }

    [Fact]
    public async Task Endpoint_GetById_NotFound_Returns404()
    {
        var result = await _ctx.Service.GetByIdAsync(99999999, _ctx.OwnerUserId);
        BookingApiAssertions.AssertFailure(result, 404);
    }

    [Fact]
    public async Task Endpoint_GetById_OtherUser_Returns404()
    {
        var (systemId, _) = await _ctx.CreateConfirmedSystemAsync();
        var result = await _ctx.Service.GetByIdAsync(systemId, _ctx.OtherUserId);
        BookingApiAssertions.AssertFailure(result, 404);
    }

    // ─── POST update / toggle / delete ───────────────────────────────

    [Fact]
    public async Task Endpoint_Update_EmptyPayload_Returns400()
    {
        var (systemId, _) = await _ctx.CreateConfirmedSystemAsync();
        var result = await _ctx.Service.UpdateAsync(systemId, _ctx.OwnerUserId, new UpdateBookingSystemDto());
        BookingApiAssertions.AssertFailure(result, 400);
    }

    [Fact]
    public async Task Endpoint_Update_ValidTitle_Returns200()
    {
        var (systemId, _) = await _ctx.CreateConfirmedSystemAsync();
        var result = await _ctx.Service.UpdateAsync(systemId, _ctx.OwnerUserId, new UpdateBookingSystemDto
        {
            Title = $"عنوان جدید {Guid.NewGuid():N}"[..25]
        });
        BookingApiAssertions.AssertSuccess(result);
    }

    [Fact]
    public async Task Endpoint_Update_NotFound_Returns404()
    {
        var result = await _ctx.Service.UpdateAsync(99999999, _ctx.OwnerUserId, new UpdateBookingSystemDto
        {
            Title = "test"
        });
        BookingApiAssertions.AssertFailure(result, 404);
    }

    [Fact]
    public async Task Endpoint_ToggleStatus_Returns200()
    {
        var (systemId, _) = await _ctx.CreateConfirmedSystemAsync();
        var result = await _ctx.Service.ToggleStatusAsync(systemId, _ctx.OwnerUserId);
        BookingApiAssertions.AssertSuccess(result);
        Assert.False(result.Data!.IsActive);
    }

    [Fact]
    public async Task Endpoint_Delete_Returns200()
    {
        var (systemId, _) = await _ctx.CreateConfirmedSystemAsync();
        var result = await _ctx.Service.DeleteAsync(systemId, _ctx.OwnerUserId);
        BookingApiAssertions.AssertSuccess(result);
    }

    // ─── Services CRUD ───────────────────────────────────────────────

    [Fact]
    public async Task Endpoint_GetServices_Returns200()
    {
        var (systemId, _) = await _ctx.CreateConfirmedSystemAsync();
        var result = await _ctx.Service.GetServicesAsync(systemId, _ctx.OwnerUserId);
        BookingApiAssertions.AssertSuccess(result);
        Assert.NotEmpty(result.Data!);
    }

    [Fact]
    public async Task Endpoint_AddService_Returns201()
    {
        var (systemId, _) = await _ctx.CreateConfirmedSystemAsync();
        var result = await _ctx.Service.AddServiceAsync(systemId, _ctx.OwnerUserId, new AddBookingServiceDto
        {
            Title = "ماساژ",
            DurationMinutes = 45,
            BufferMinutesBetweenAppointments = 5,
            ReminderOffsetMinutes = 120,
            WeeklyDays = BookingSystemTestContext.DefaultWeeklySchedule()
        });
        BookingApiAssertions.AssertSuccess(result, 201);
    }

    [Fact]
    public async Task Endpoint_UpdateService_Returns200()
    {
        var (systemId, _) = await _ctx.CreateConfirmedSystemAsync();
        var serviceId = (await _ctx.Service.GetServicesAsync(systemId, _ctx.OwnerUserId)).Data!.First().Id;

        var result = await _ctx.Service.UpdateServiceAsync(systemId, serviceId, _ctx.OwnerUserId, new UpdateBookingServiceDto
        {
            Title = "خدمت ویرایش‌شده"
        });
        BookingApiAssertions.AssertSuccess(result);
    }

    [Fact]
    public async Task Endpoint_SaveServiceSchedule_Returns200()
    {
        var (systemId, _) = await _ctx.CreateConfirmedSystemAsync();
        var serviceId = (await _ctx.Service.GetServicesAsync(systemId, _ctx.OwnerUserId)).Data!.First().Id;

        var result = await _ctx.Service.SaveServiceScheduleAsync(systemId, serviceId, _ctx.OwnerUserId,
            new SaveBookingServiceScheduleDto { WeeklyDays = BookingSystemTestContext.DefaultWeeklySchedule() });
        BookingApiAssertions.AssertSuccess(result);
    }

    [Fact]
    public async Task Endpoint_AddAndDeleteScheduleException_Returns201And200()
    {
        var (systemId, _) = await _ctx.CreateConfirmedSystemAsync();
        var serviceId = (await _ctx.Service.GetServicesAsync(systemId, _ctx.OwnerUserId)).Data!.First().Id;

        var add = await _ctx.Service.AddScheduleExceptionAsync(systemId, serviceId, _ctx.OwnerUserId,
            new AddBookingScheduleExceptionDto
            {
                ExceptionDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(40)),
                Type = BookingScheduleExceptionTypes.Leave,
                Label = "مرخصی"
            });
        BookingApiAssertions.AssertSuccess(add, 201);

        var exceptionId = await _ctx.Context.BookingScheduleExceptions
            .Where(e => e.BookingServiceItemId == serviceId && !e.IsDeleted)
            .OrderByDescending(e => e.Id)
            .Select(e => e.Id)
            .FirstAsync();

        var delete = await _ctx.Service.DeleteScheduleExceptionAsync(systemId, serviceId, exceptionId, _ctx.OwnerUserId);
        BookingApiAssertions.AssertSuccess(delete);
    }

    [Fact]
    public async Task Endpoint_DeleteService_Returns200()
    {
        var (systemId, _) = await _ctx.CreateConfirmedSystemAsync();
        var add = await _ctx.Service.AddServiceAsync(systemId, _ctx.OwnerUserId, new AddBookingServiceDto
        {
            Title = "موقت",
            DurationMinutes = 15,
            BufferMinutesBetweenAppointments = 0,
            ReminderOffsetMinutes = 30,
            WeeklyDays = BookingSystemTestContext.DefaultWeeklySchedule()
        });
        var serviceId = add.Data!.Id;

        var result = await _ctx.Service.DeleteServiceAsync(systemId, serviceId, _ctx.OwnerUserId);
        BookingApiAssertions.AssertSuccess(result);
    }

    // ─── BookingPublic ───────────────────────────────────────────────

    [Fact]
    public async Task Endpoint_PublicGetSystem_ValidSlug_Returns200()
    {
        var slug = $"pub-{Guid.NewGuid():N}"[..15];
        await _ctx.CreateConfirmedSystemAsync(s => s.CustomSlug = slug);

        var result = await _ctx.AppointmentService.GetPublicSystemAsync(slug);
        BookingApiAssertions.AssertSuccess(result);
        Assert.NotEmpty(result.Data!.Services);
    }

    [Fact]
    public async Task Endpoint_PublicGetSystem_UnknownSlug_Returns404()
    {
        var result = await _ctx.AppointmentService.GetPublicSystemAsync("unknown-slug-xyz");
        BookingApiAssertions.AssertFailure(result, 404);
    }

    [Fact]
    public async Task Endpoint_PublicGetSystem_InactiveSystem_Returns404()
    {
        var (systemId, _) = await _ctx.CreateConfirmedSystemAsync();
        var system = await _ctx.Service.GetByIdAsync(systemId, _ctx.OwnerUserId);
        var slug = system.Data!.Slug;

        await _ctx.Service.ToggleStatusAsync(systemId, _ctx.OwnerUserId);

        var result = await _ctx.AppointmentService.GetPublicSystemAsync(slug);
        BookingApiAssertions.AssertFailure(result, 404);
    }

    [Fact]
    public async Task Endpoint_PublicGetSlots_PastDate_Returns400()
    {
        var (systemId, _) = await _ctx.CreateConfirmedSystemAsync();
        var system = await _ctx.Service.GetByIdAsync(systemId, _ctx.OwnerUserId);
        var serviceId = system.Data!.Services.First().Id;

        var result = await _ctx.AppointmentService.GetAvailableSlotsAsync(
            system.Data.Slug, serviceId, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)));
        BookingApiAssertions.AssertFailure(result, 400);
    }

    [Fact]
    public async Task Endpoint_PublicGetSlots_InvalidService_Returns404()
    {
        var (systemId, _) = await _ctx.CreateConfirmedSystemAsync();
        var slug = (await _ctx.Service.GetByIdAsync(systemId, _ctx.OwnerUserId)).Data!.Slug;

        var result = await _ctx.AppointmentService.GetAvailableSlotsAsync(
            slug, 99999999, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)));
        BookingApiAssertions.AssertFailure(result, 404);
    }

    [Fact]
    public async Task Endpoint_PublicBook_InvalidMobile_Returns400()
    {
        var (systemId, _) = await _ctx.CreateConfirmedSystemAsync();
        var system = await _ctx.Service.GetByIdAsync(systemId, _ctx.OwnerUserId);
        var serviceId = system.Data!.Services.First().Id;
        var slug = system.Data.Slug;
        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7));
        var startUtc = (await _ctx.AppointmentService.GetAvailableSlotsAsync(slug, serviceId, date)).Data!.Slots.First().StartUtc;

        var result = await _ctx.AppointmentService.CreatePublicBookingAsync(slug, new CreatePublicBookingDto
        {
            ServiceId = serviceId,
            StartUtc = startUtc,
            CustomerFullName = "تست",
            CustomerMobile = "123"
        });
        BookingApiAssertions.AssertFailure(result, 400);
    }

    [Fact]
    public async Task Endpoint_PublicBook_HappyPath_Returns201()
    {
        var (systemId, _) = await _ctx.CreateConfirmedSystemAsync();
        var system = await _ctx.Service.GetByIdAsync(systemId, _ctx.OwnerUserId);
        var serviceId = system.Data!.Services.First().Id;
        var slug = system.Data.Slug;
        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7));
        var startUtc = (await _ctx.AppointmentService.GetAvailableSlotsAsync(slug, serviceId, date)).Data!.Slots.First().StartUtc;

        var result = await _ctx.AppointmentService.CreatePublicBookingAsync(slug, new CreatePublicBookingDto
        {
            ServiceId = serviceId,
            StartUtc = startUtc,
            CustomerFullName = "مشتری Swagger",
            CustomerMobile = "09124445566"
        });
        BookingApiAssertions.AssertSuccess(result, 201);
    }

    [Fact]
    public async Task Endpoint_PublicBook_SaveToPhonebook_AddsContact()
    {
        var mobile = "09125556677";
        var (systemId, _) = await _ctx.CreateConfirmedSystemAsync(s =>
        {
            s.SaveToPhonebook = true;
            s.NotebookIds = new List<int> { _ctx.NotebookId };
        });

        var system = await _ctx.Service.GetByIdAsync(systemId, _ctx.OwnerUserId);
        var serviceId = system.Data!.Services.First().Id;
        var slug = system.Data.Slug;
        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7));
        var startUtc = (await _ctx.AppointmentService.GetAvailableSlotsAsync(slug, serviceId, date)).Data!.Slots.First().StartUtc;

        var book = await _ctx.AppointmentService.CreatePublicBookingAsync(slug, new CreatePublicBookingDto
        {
            ServiceId = serviceId,
            StartUtc = startUtc,
            CustomerFullName = "مخاطب جدید",
            CustomerMobile = mobile
        });
        BookingApiAssertions.AssertSuccess(book, 201);

        var exists = await _ctx.Context.Contacts.AnyAsync(c =>
            c.ContactNotebookId == _ctx.NotebookId &&
            c.MobileNumber == mobile &&
            !c.IsDeleted);
        Assert.True(exists);
    }

    // ─── Appointments (owner) ──────────────────────────────────────────

    [Fact]
    public async Task Endpoint_GetAppointments_Returns200()
    {
        var (systemId, _) = await _ctx.CreateConfirmedSystemAsync();
        await BookOneAsync(systemId);

        var result = await _ctx.AppointmentService.GetAppointmentsAsync(
            systemId, _ctx.OwnerUserId, 1, 10, BookingAppointmentStatuses.Confirmed, null, null, null);
        BookingApiAssertions.AssertSuccess(result);
        Assert.NotEmpty(result.Data!.Appointments);
    }

    [Fact]
    public async Task Endpoint_GetAppointments_OtherUser_Returns404()
    {
        var (systemId, _) = await _ctx.CreateConfirmedSystemAsync();
        var result = await _ctx.AppointmentService.GetAppointmentsAsync(
            systemId, _ctx.OtherUserId, 1, 10, null, null, null, null);
        BookingApiAssertions.AssertFailure(result, 404);
    }

    [Fact]
    public async Task Endpoint_CancelAppointment_NotFound_Returns404()
    {
        var (systemId, _) = await _ctx.CreateConfirmedSystemAsync();
        var result = await _ctx.AppointmentService.CancelAppointmentAsync(
            systemId, 99999999, _ctx.OwnerUserId, null);
        BookingApiAssertions.AssertFailure(result, 404);
    }

    [Fact]
    public async Task Endpoint_CancelAppointment_AlreadyCancelled_Returns400()
    {
        var (systemId, _) = await _ctx.CreateConfirmedSystemAsync();
        var appointmentId = await BookOneAsync(systemId);

        await _ctx.AppointmentService.CancelAppointmentAsync(systemId, appointmentId, _ctx.OwnerUserId, null);
        var second = await _ctx.AppointmentService.CancelAppointmentAsync(systemId, appointmentId, _ctx.OwnerUserId, null);
        BookingApiAssertions.AssertFailure(second, 400);
    }

    // ─── Full smoke: تمام endpointهای اصلی بدون 500 ─────────────────

    [Fact]
    public async Task SmokeTest_AllPrimaryEndpoints_No500()
    {
        var slug = $"smoke-{Guid.NewGuid():N}"[..18];
        var (systemId, _) = await _ctx.CreateConfirmedSystemAsync(s => s.CustomSlug = slug);
        var system = (await _ctx.Service.GetByIdAsync(systemId, _ctx.OwnerUserId)).Data!;
        var serviceId = system.Services.First().Id;
        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7));

        var checks = new List<ApiResponse<object>>
        {
            Cast(await _ctx.Service.GetActivityTypesAsync()),
            Cast(await _ctx.Service.GetNotebooksAsync(_ctx.OwnerUserId)),
            Cast(await _ctx.Service.GetSystemsAsync(_ctx.OwnerUserId, 1, 10, null)),
            Cast(await _ctx.Service.GetByIdAsync(systemId, _ctx.OwnerUserId)),
            Cast(await _ctx.Service.GetServicesAsync(systemId, _ctx.OwnerUserId)),
            Cast(await _ctx.Service.GetServiceScheduleAsync(systemId, serviceId, _ctx.OwnerUserId)),
            Cast(await _ctx.AppointmentService.GetPublicSystemAsync(slug)),
            Cast(await _ctx.AppointmentService.GetAvailableSlotsAsync(slug, serviceId, date))
        };

        foreach (var response in checks)
        {
            BookingApiAssertions.AssertNoServerLeak(response);
            Assert.NotEqual(500, response.StatusCode);
        }
    }

    private async Task<int> BookOneAsync(int systemId)
    {
        var system = (await _ctx.Service.GetByIdAsync(systemId, _ctx.OwnerUserId)).Data!;
        var serviceId = system.Services.First().Id;
        var slug = system.Slug;
        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7));
        var startUtc = (await _ctx.AppointmentService.GetAvailableSlotsAsync(slug, serviceId, date)).Data!.Slots.First().StartUtc;

        var book = await _ctx.AppointmentService.CreatePublicBookingAsync(slug, new CreatePublicBookingDto
        {
            ServiceId = serviceId,
            StartUtc = startUtc,
            CustomerFullName = "Smoke",
            CustomerMobile = "09126667788"
        });
        BookingApiAssertions.AssertSuccess(book, 201);
        return book.Data!.Appointment.Id;
    }

    private static ApiResponse<object> Cast<T>(ApiResponse<T> response) =>
        new()
        {
            StatusCode = response.StatusCode,
            Success = response.Success,
            Message = response.Message,
            ErrorCode = response.ErrorCode,
            Errors = response.Errors
        };
}
