using Api_Vapp.Constants;
using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.Sms;
using Api_Vapp.Data;
using Api_Vapp.DTOs.BookingSystem;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;
using Api_Vapp.Repositories;
using Api_Vapp.Services;
using Api_Vapp.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Api_Vapp.Tests.BookingSystem;

internal sealed class BookingSystemTestContext : IDisposable
{
    private static readonly SemaphoreSlim MigrationLock = new(1, 1);
    private static bool _migrationsApplied;

    private readonly Api_Context _context;

    private BookingSystemTestContext(Api_Context context)
    {
        _context = context;
    }

    public IBookingSystemService Service { get; private set; } = null!;

    public IBookingSystemService SystemService => Service;

    public IBookingAppointmentService AppointmentService { get; private set; } = null!;

    public Api_Context Context => _context;

    public int OwnerUserId { get; private set; }

    public int OtherUserId { get; private set; }

    public int NotebookId { get; private set; }

    public static async Task<BookingSystemTestContext> CreateAsync()
    {
        var connectionString =
            Environment.GetEnvironmentVariable("VAPP_TEST_CONNECTION")
            ?? "Server=localhost,1436;Database=DbVapp_UserFormTests;User Id=sa;Password=Vapp@Secure2025!;TrustServerCertificate=True;Encrypt=False;MultipleActiveResultSets=true";

        var options = new DbContextOptionsBuilder<Api_Context>()
            .UseSqlServer(connectionString)
            .Options;

        var context = new Api_Context(options);

        await MigrationLock.WaitAsync();
        try
        {
            if (!_migrationsApplied)
            {
                await context.Database.MigrateAsync();
                _migrationsApplied = true;
            }
        }
        finally
        {
            MigrationLock.Release();
        }

        var testContext = new BookingSystemTestContext(context);
        await testContext.InitializeAsync();
        return testContext;
    }

    public BookingStep1Dto BuildStep1Dto(Action<BookingStep1Dto>? configure = null)
    {
        var suffix = Guid.NewGuid().ToString("N")[..6];
        var dto = new BookingStep1Dto
        {
            Title = $"سالن تست {suffix}",
            ActivityType = BookingActivityTypes.BeautySalon,
            Description = "توضیحات تست",
            SaveToPhonebook = false,
            NotebookIds = new List<int>()
        };

        configure?.Invoke(dto);
        return dto;
    }

    public static List<BookingDayScheduleDto> DefaultWeeklySchedule()
    {
        return Enum.GetValues<DayOfWeek>()
            .Select(day => new BookingDayScheduleDto
            {
                DayOfWeek = day,
                IsOpen = day != DayOfWeek.Friday,
                StartTimeUtc = day != DayOfWeek.Friday ? TimeSpan.FromHours(5.5) : null,
                EndTimeUtc = day != DayOfWeek.Friday ? TimeSpan.FromHours(14.5) : null
            })
            .ToList();
    }

    public async Task<(int SystemId, string PublicUrl)> CreateConfirmedSystemAsync(
        Action<BookingStep1Dto>? configureStep1 = null)
    {
        var step1 = BuildStep1Dto(configureStep1);
        var step1Result = await Service.ValidateStep1Async(OwnerUserId, step1);
        if (!step1Result.Success || step1Result.Data?.DraftId == null)
        {
            throw new InvalidOperationException($"Step1 failed: {step1Result.StatusCode} {step1Result.Message}");
        }

        var draftId = step1Result.Data.DraftId;
        var serviceTempId = Guid.NewGuid().ToString("N");

        var step2Result = await Service.ValidateStep2Async(OwnerUserId, new BookingStep2Dto
        {
            DraftId = draftId,
            Services = new List<BookingServiceDraftDto>
            {
                new()
                {
                    ServiceTempId = serviceTempId,
                    Title = "فیشیال تخصصی",
                    DurationMinutes = 60,
                    HasCost = true,
                    Price = 500_000m
                }
            }
        });
        if (!step2Result.Success)
        {
            throw new InvalidOperationException($"Step2 failed: {step2Result.StatusCode} {step2Result.Message}");
        }

        var step3Result = await Service.ValidateStep3Async(OwnerUserId, new BookingStep3Dto
        {
            DraftId = draftId,
            ServiceSchedules = new List<BookingServiceScheduleDraftDto>
            {
                new()
                {
                    ServiceTempId = serviceTempId,
                    WeeklyDays = DefaultWeeklySchedule(),
                    Exceptions = new List<BookingScheduleExceptionDto>
                    {
                        new()
                        {
                            ExceptionDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)),
                            Type = BookingScheduleExceptionTypes.Holiday,
                            Label = "تعطیل"
                        }
                    }
                }
            }
        });
        if (!step3Result.Success)
        {
            throw new InvalidOperationException($"Step3 failed: {step3Result.StatusCode} {step3Result.Message}");
        }

        var step4Result = await Service.ValidateStep4Async(OwnerUserId, new BookingStep4Dto
        {
            DraftId = draftId,
            ServiceSettings = new List<BookingServiceReminderDraftDto>
            {
                new()
                {
                    ServiceTempId = serviceTempId,
                    BufferMinutesBetweenAppointments = 10,
                    MaxDailyReservations = 20,
                    ReminderOffsetMinutes = 1440
                }
            }
        });
        if (!step4Result.Success)
        {
            throw new InvalidOperationException($"Step4 failed: {step4Result.StatusCode} {step4Result.Message}");
        }

        var confirmResult = await Service.ConfirmAsync(OwnerUserId, new ConfirmBookingSystemDto { DraftId = draftId });
        if (!confirmResult.Success || confirmResult.Data?.System == null)
        {
            throw new InvalidOperationException($"Confirm failed: {confirmResult.StatusCode} {confirmResult.Message}");
        }

        return (confirmResult.Data.System.Id, confirmResult.Data.System.PublicUrl);
    }

    public async Task<(string DraftId, string ServiceTempId)> RunWizardThroughStep4Async(
        Action<BookingStep1Dto>? configureStep1 = null)
    {
        var step1 = BuildStep1Dto(configureStep1);
        var step1Result = await Service.ValidateStep1Async(OwnerUserId, step1);
        if (!step1Result.Success || step1Result.Data?.DraftId == null)
        {
            throw new InvalidOperationException($"Step1 failed: {step1Result.Message}");
        }

        var draftId = step1Result.Data.DraftId;
        var serviceTempId = Guid.NewGuid().ToString("N");

        await Service.ValidateStep2Async(OwnerUserId, new BookingStep2Dto
        {
            DraftId = draftId,
            Services = new List<BookingServiceDraftDto>
            {
                new()
                {
                    ServiceTempId = serviceTempId,
                    Title = "خدمت تست",
                    DurationMinutes = 30,
                    HasCost = false
                }
            }
        });

        await Service.ValidateStep3Async(OwnerUserId, new BookingStep3Dto
        {
            DraftId = draftId,
            ServiceSchedules = new List<BookingServiceScheduleDraftDto>
            {
                new()
                {
                    ServiceTempId = serviceTempId,
                    WeeklyDays = DefaultWeeklySchedule()
                }
            }
        });

        await Service.ValidateStep4Async(OwnerUserId, new BookingStep4Dto
        {
            DraftId = draftId,
            ServiceSettings = new List<BookingServiceReminderDraftDto>
            {
                new()
                {
                    ServiceTempId = serviceTempId,
                    BufferMinutesBetweenAppointments = 5,
                    ReminderOffsetMinutes = 60
                }
            }
        });

        return (draftId, serviceTempId);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    private async Task InitializeAsync()
    {
        Service = CreateBookingSystemService(_context);
        AppointmentService = CreateAppointmentService(_context);
        await SeedAsync();
    }

    private static IBookingSystemService CreateBookingSystemService(Api_Context context)
    {
        var options = Options.Create(new BookingSystemOptions
        {
            PublicBaseUrl = "https://app.com/book"
        });

        return new BookingSystemService(
            context,
            new BookingSystemRepository(context),
            new BookingSystemDraftRepository(context),
            options,
            NullLogger<BookingSystemService>.Instance);
    }

    private static IBookingAppointmentService CreateAppointmentService(Api_Context context)
    {
        return new BookingAppointmentService(
            context,
            new BookingAppointmentRepository(context),
            new BookingSystemRepository(context),
            new FakeSmsService(),
            new FakeDeliveryTrackingService(),
            NullLogger<BookingAppointmentService>.Instance);
    }

    private sealed class FakeSmsService : ISmsService
    {
        public Task<bool> SendOtpAsync(string phoneNumber, string otpCode, string templateType = "VerifyOtp") =>
            Task.FromResult(true);

        public Task<string> GenerateOtpAsync() => Task.FromResult("123456");

        public Task<ApiResponse<SendSmsResponseDto>> SendSmsAsync(SendSmsRequestDto request) =>
            Task.FromResult(ApiResponse<SendSmsResponseDto>.CreateSuccess(new SendSmsResponseDto
            {
                Sid = 1,
                Status = 1,
                Message = "sent"
            }));

        public Task<ApiResponse<SendBulkResponseDto>> SendBulkSmsAsync(SendBulkRequestDto request) =>
            Task.FromResult(ApiResponse<SendBulkResponseDto>.CreateSuccess(new SendBulkResponseDto()));

        public Task<ApiResponse<SendArrayResponseDto>> SendArraySmsAsync(SendArrayRequestDto request) =>
            Task.FromResult(ApiResponse<SendArrayResponseDto>.CreateSuccess(new SendArrayResponseDto()));

        public Task<ApiResponse<DeliveryResponseDto>> GetDeliveryStatusAsync(long sid) =>
            Task.FromResult(ApiResponse<DeliveryResponseDto>.CreateSuccess(new DeliveryResponseDto()));

        public Task<ApiResponse<InboxResponseDto>> GetInboxAsync(InboxRequestDto request) =>
            Task.FromResult(ApiResponse<InboxResponseDto>.CreateSuccess(new InboxResponseDto()));

        public Task<ApiResponse<InfoResponseDto>> GetWalletInfoAsync() =>
            Task.FromResult(ApiResponse<InfoResponseDto>.CreateSuccess(new InfoResponseDto()));
    }

    private sealed class FakeDeliveryTrackingService : ISmsDeliveryTrackingService
    {
        public Task TrackSuccessfulSendAsync(SmsDeliveryTrackRequestDto request) => Task.CompletedTask;

        public Task<ApiResponse<SmsDeliveryRecordDto>> GetByIdAsync(int userId, int id) =>
            Task.FromResult(ApiResponse<SmsDeliveryRecordDto>.NotFound("not found"));

        public Task<ApiResponse<SmsDeliveryReportListDto>> GetReportAsync(int userId, SmsDeliveryReportFilterDto filter) =>
            Task.FromResult(ApiResponse<SmsDeliveryReportListDto>.CreateSuccess(new SmsDeliveryReportListDto()));

        public Task<ApiResponse<SmsDeliverySummaryDto>> GetSummaryAsync(int userId, SmsDeliveryReportFilterDto filter) =>
            Task.FromResult(ApiResponse<SmsDeliverySummaryDto>.CreateSuccess(new SmsDeliverySummaryDto()));

        public Task<ApiResponse<SmsDeliveryRecordDto>> RefreshRecordAsync(int userId, int id) =>
            Task.FromResult(ApiResponse<SmsDeliveryRecordDto>.NotFound("not found"));

        public Task SyncPendingDeliveriesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private async Task SeedAsync()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var owner = new User
        {
            PhoneNumber = $"0915{suffix[..7]}",
            PasswordHash = "hash-owner",
            FullName = "مالک تست رزرو",
            IsActive = true,
            IsPhoneVerified = true,
            CreatedAt = DateTime.UtcNow
        };

        var other = new User
        {
            PhoneNumber = $"0916{suffix[..7]}",
            PasswordHash = "hash-other",
            FullName = "کاربر دیگر",
            IsActive = true,
            IsPhoneVerified = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.Users.AddRange(owner, other);
        await _context.SaveChangesAsync();
        OwnerUserId = owner.Id;
        OtherUserId = other.Id;

        var notebook = new ContactNotebook
        {
            UserId = OwnerUserId,
            Name = $"دفترچه رزرو {suffix}",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.ContactNotebooks.Add(notebook);
        await _context.SaveChangesAsync();
        NotebookId = notebook.Id;
    }
}
