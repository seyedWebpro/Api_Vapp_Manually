using Api_Vapp.Constants;
using Api_Vapp.Data;
using Api_Vapp.DTOs.BookingSystem;
using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.File;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;
using Api_Vapp.Services.Audit;
using Api_Vapp.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api_Vapp.Services
{
    public class BookingAppointmentService : IBookingAppointmentService
    {
        private readonly Api_Context _context;
        private readonly IBookingAppointmentRepository _appointmentRepository;
        private readonly IBookingSystemRepository _systemRepository;
        private readonly PublicPhonebookService _phonebookService;
        private readonly IUserSmsBillingService _userSmsBilling;
        private readonly IFileUploadService _fileUploadService;
        private readonly IAuditService _audit;
        private readonly ILogger<BookingAppointmentService> _logger;
        private readonly BookingSystemOptions _options;

        private const int MaxReminderOffsetMinutes = 43200;

        public BookingAppointmentService(
            Api_Context context,
            IBookingAppointmentRepository appointmentRepository,
            IBookingSystemRepository systemRepository,
            PublicPhonebookService phonebookService,
            IUserSmsBillingService userSmsBilling,
            IFileUploadService fileUploadService,
            Microsoft.Extensions.Options.IOptions<BookingSystemOptions> options,
            IAuditService audit,
            ILogger<BookingAppointmentService> logger)
        {
            _context = context;
            _appointmentRepository = appointmentRepository;
            _systemRepository = systemRepository;
            _phonebookService = phonebookService;
            _userSmsBilling = userSmsBilling;
            _fileUploadService = fileUploadService;
            _options = options.Value;
            _audit = audit;
            _logger = logger;
        }

        public async Task<ApiResponse<BookingPublicSystemDto>> GetPublicSystemAsync(string slug)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(slug))
                {
                    return ApiResponse<BookingPublicSystemDto>.BadRequest(
                        "لینک نامعتبر است",
                        errorCode: ErrorCodes.InvalidInput);
                }

                var normalizedSlug = slug.Trim().ToLowerInvariant();
                var system = await _appointmentRepository.GetActiveSystemBySlugAsync(normalizedSlug);
                var availabilityError = EnsurePubliclyAvailable<BookingPublicSystemDto>(system);
                if (availabilityError != null)
                {
                    return availabilityError;
                }

                return ApiResponse<BookingPublicSystemDto>.CreateSuccess(MapToPublicDto(system!));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading public booking system for slug {Slug}", slug);
                return ApiResponse<BookingPublicSystemDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<BookingAvailableSlotsDto>> GetAvailableSlotsAsync(
            string slug, int serviceId, DateOnly date)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(slug))
                {
                    return ApiResponse<BookingAvailableSlotsDto>.BadRequest(
                        "لینک نامعتبر است",
                        errorCode: ErrorCodes.InvalidInput);
                }

                var normalizedSlug = slug.Trim().ToLowerInvariant();
                var service = await _appointmentRepository.GetActiveServiceBySlugAsync(normalizedSlug, serviceId);
                if (service == null)
                {
                    return ApiResponse<BookingAvailableSlotsDto>.NotFound("خدمت یافت نشد");
                }

                var availabilityError = EnsurePubliclyAvailable<BookingAvailableSlotsDto>(service.BookingSystem);
                if (availabilityError != null)
                {
                    return availabilityError;
                }

                if (date < DateOnly.FromDateTime(DateTime.UtcNow))
                {
                    return ApiResponse<BookingAvailableSlotsDto>.BadRequest(
                        "تاریخ گذشته قابل رزرو نیست",
                        errorCode: ErrorCodes.InvalidInput);
                }

                var existing = await _appointmentRepository.GetAppointmentsForServiceOnDateAsync(serviceId, date);
                var blocked = await _appointmentRepository.GetBlockedStartsForSystemOnDateAsync(
                    service.BookingSystemId, date);
                var slots = BookingSlotCalculator.CalculateAvailableSlots(service, date, existing, blocked);

                var now = DateTime.UtcNow;
                slots = slots.Where(s => s.StartUtc > now).ToList();

                return ApiResponse<BookingAvailableSlotsDto>.CreateSuccess(new BookingAvailableSlotsDto
                {
                    ServiceId = serviceId,
                    Date = date,
                    Slots = slots
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading slots for slug {Slug}, service {ServiceId}", slug, serviceId);
                return ApiResponse<BookingAvailableSlotsDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<CreatePublicBookingResponseDto>> CreatePublicBookingAsync(
            string slug,
            CreatePublicBookingDto dto,
            IFormFile? paymentReceiptFile = null)
        {
            if (string.IsNullOrWhiteSpace(slug))
            {
                return ApiResponse<CreatePublicBookingResponseDto>.BadRequest(
                    "لینک نامعتبر است",
                    errorCode: ErrorCodes.InvalidInput);
            }

            var hasReceiptFile = paymentReceiptFile is { Length: > 0 };
            if (hasReceiptFile)
            {
                var receiptError = SecureFileValidator.ValidatePaymentReceipt(paymentReceiptFile);
                if (receiptError != null)
                {
                    return ApiResponse<CreatePublicBookingResponseDto>.BadRequest(
                        receiptError,
                        errorCode: ErrorCodes.ValidationFailed);
                }
            }

            var normalizedSlug = slug.Trim().ToLowerInvariant();
            await using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var system = await _context.BookingSystems
                    .Include(b => b.Notebooks)
                    .FirstOrDefaultAsync(b =>
                        b.Slug == normalizedSlug &&
                        !b.IsDeleted &&
                        b.Status == BookingSystemStatus.Published);

                var availabilityError = EnsurePubliclyAvailable<CreatePublicBookingResponseDto>(system);
                if (availabilityError != null || system is null)
                {
                    return availabilityError ?? ApiResponse<CreatePublicBookingResponseDto>.NotFound(BookingNotFoundMessage);
                }

                if (hasReceiptFile)
                {
                    var service = await _appointmentRepository.GetServiceForBookingAsync(system.Id, dto.ServiceId);
                    if (service == null)
                    {
                        return ApiResponse<CreatePublicBookingResponseDto>.NotFound("خدمت یافت نشد");
                    }

                    if (!service.HasCost)
                    {
                        return ApiResponse<CreatePublicBookingResponseDto>.BadRequest(
                            "آپلود فیش واریز فقط برای خدمات هزینه‌دار مجاز است",
                            errorCode: ErrorCodes.ValidationFailed);
                    }
                }

                var created = await CreateAppointmentInternalAsync(
                    system,
                    dto.ServiceId,
                    dto.StartUtc,
                    dto.CustomerFullName,
                    dto.CustomerMobile,
                    dto.CustomerNote,
                    BookingAppointmentStatuses.Pending,
                    requireFutureSlot: true,
                    remindersEnabled: dto.RemindersEnabled ?? true);

                if (!created.Success)
                {
                    await transaction.RollbackAsync();
                    return ApiResponse<CreatePublicBookingResponseDto>.Error(
                        created.ErrorMessage!,
                        created.StatusCode,
                        errorCode: created.ErrorCode);
                }

                if (hasReceiptFile)
                {
                    try
                    {
                        var relativePath = await _fileUploadService.UploadFileAsync(
                            paymentReceiptFile!,
                            FileUploadConstants.EntityType_BookingAppointment,
                            created.Appointment!.Id,
                            FileUploadConstants.SubFolder_PaymentReceipt);

                        created.Appointment.PaymentReceiptPath = relativePath;
                        created.Appointment.UpdatedAt = DateTime.UtcNow;
                        await _context.SaveChangesAsync();
                    }
                    catch (ArgumentException ex)
                    {
                        await transaction.RollbackAsync();
                        return ApiResponse<CreatePublicBookingResponseDto>.BadRequest(
                            ControlledErrorHelper.SanitizeArgumentMessage(
                                ex.Message,
                                ControlledErrorHelper.FileUploadFailed),
                            errorCode: ErrorCodes.ValidationFailed);
                    }
                    catch (Exception ex)
                    {
                        await transaction.RollbackAsync();
                        _logger.LogError(
                            ex,
                            "Error uploading payment receipt for public booking slug {Slug}",
                            slug);
                        return ApiResponse<CreatePublicBookingResponseDto>.InternalServerError(
                            ControlledErrorHelper.FileUploadFailed);
                    }
                }

                await transaction.CommitAsync();

                _logger.LogInformation(
                    "Public booking created {AppointmentId} for system {SystemId}, hasReceipt={HasReceipt}",
                    created.Appointment!.Id,
                    system.Id,
                    hasReceiptFile);

                return ApiResponse<CreatePublicBookingResponseDto>.CreateSuccess(
                    new CreatePublicBookingResponseDto { Appointment = MapToDto(created.Appointment) },
                    "نوبت با موفقیت ثبت شد و منتظر تأیید است",
                    201);
            }
            catch (DbUpdateException dbEx)
            {
                await transaction.RollbackAsync();
                return BookingDbExceptionHelper.MapDbUpdateException<CreatePublicBookingResponseDto>(
                    dbEx, _logger, "creating public booking");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error creating public booking for slug {Slug}", slug);
                return ApiResponse<CreatePublicBookingResponseDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<PublicBookingStatusDto>> LookupPublicBookingStatusAsync(
            string slug,
            LookupPublicBookingDto dto)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(slug))
                {
                    return ApiResponse<PublicBookingStatusDto>.BadRequest(
                        "لینک نامعتبر است",
                        errorCode: ErrorCodes.InvalidInput);
                }

                if (dto.AppointmentNumber <= 0)
                {
                    return ApiResponse<PublicBookingStatusDto>.BadRequest(
                        "شماره نوبت نامعتبر است",
                        errorCode: ErrorCodes.ValidationFailed);
                }

                var mobile = BookingMobileHelper.Normalize(dto.CustomerMobile);
                if (!BookingMobileHelper.IsValidIranianMobile(mobile))
                {
                    return ApiResponse<PublicBookingStatusDto>.BadRequest(
                        "شماره موبایل نامعتبر است",
                        errorCode: ErrorCodes.ValidationFailed);
                }

                var normalizedSlug = slug.Trim().ToLowerInvariant();
                var appointment = await _appointmentRepository.GetPublicByNumberAndMobileAsync(
                    normalizedSlug,
                    dto.AppointmentNumber,
                    mobile);

                if (appointment == null)
                {
                    return ApiResponse<PublicBookingStatusDto>.NotFound(
                        "نوبتی با این مشخصات یافت نشد");
                }

                return ApiResponse<PublicBookingStatusDto>.CreateSuccess(MapToPublicStatusDto(appointment));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error looking up public booking status for slug {Slug}", slug);
                return ApiResponse<PublicBookingStatusDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<BookingDashboardDto>> GetDashboardAsync(
            int systemId, int userId, DateOnly? dateUtc = null)
        {
            try
            {
                var system = await _systemRepository.GetByIdAndUserIdAsync(systemId, userId);
                if (system == null)
                {
                    return ApiResponse<BookingDashboardDto>.NotFound("سیستم رزرو یافت نشد");
                }

                var today = dateUtc ?? DateOnly.FromDateTime(DateTime.UtcNow);
                var counts = await _appointmentRepository.GetDashboardCountsAsync(systemId, today);
                var todayAppointments = await _appointmentRepository.GetAppointmentsForSystemOnDateAsync(systemId, today);

                var todaySchedule = todayAppointments
                    .Where(a => a.Status == BookingAppointmentStatuses.Confirmed)
                    .OrderBy(a => a.StartUtc)
                    .Select(MapToDto)
                    .ToList();

                return ApiResponse<BookingDashboardDto>.CreateSuccess(new BookingDashboardDto
                {
                    SystemId = system.Id,
                    Title = system.Title,
                    ActivityType = system.ActivityType,
                    ActivityTypeTitle = BookingActivityTypes.GetTitle(system.ActivityType),
                    Location = system.Location,
                    PublicUrl = BuildPublicUrl(system.Slug),
                    IsActive = system.IsActive,
                    Stats = new BookingDashboardStatsDto
                    {
                        TodayTotal = counts.TodayTotal,
                        Confirmed = counts.Confirmed,
                        Pending = counts.Pending,
                        Cancelled = counts.Cancelled
                    },
                    TodaySchedule = todaySchedule
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading dashboard for system {SystemId}", systemId);
                return ApiResponse<BookingDashboardDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<BookingCalendarMonthDto>> GetCalendarAsync(
            int systemId, int userId, int year, int month)
        {
            try
            {
                if (year < 2000 || year > 2100 || month < 1 || month > 12)
                {
                    return ApiResponse<BookingCalendarMonthDto>.BadRequest(
                        "سال یا ماه نامعتبر است",
                        errorCode: ErrorCodes.InvalidInput);
                }

                var system = await _systemRepository.GetByIdAndUserIdAsync(systemId, userId);
                if (system == null)
                {
                    return ApiResponse<BookingCalendarMonthDto>.NotFound("سیستم رزرو یافت نشد");
                }

                var fromUtc = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
                var toUtc = fromUtc.AddMonths(1);
                var appointments = await _appointmentRepository.GetCalendarAppointmentsAsync(systemId, fromUtc, toUtc);

                var days = appointments
                    .GroupBy(a => DateOnly.FromDateTime(a.StartUtc))
                    .OrderBy(g => g.Key)
                    .Select(g => new BookingCalendarDayDto
                    {
                        Date = g.Key,
                        TotalCount = g.Count(),
                        Slots = g
                            .OrderBy(a => a.StartUtc)
                            .Take(5)
                            .Select(a => new BookingCalendarSlotDto
                            {
                                AppointmentId = a.Id,
                                StartUtc = EnsureUtc(a.StartUtc),
                                Status = a.Status,
                                CustomerFullName = a.CustomerFullName,
                                ServiceTitle = a.BookingServiceItem?.Title ?? string.Empty
                            })
                            .ToList()
                    })
                    .ToList();

                return ApiResponse<BookingCalendarMonthDto>.CreateSuccess(new BookingCalendarMonthDto
                {
                    Year = year,
                    Month = month,
                    Days = days
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading calendar for system {SystemId}", systemId);
                return ApiResponse<BookingCalendarMonthDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<BookingAppointmentListDto>> GetAppointmentsAsync(
            int systemId,
            int userId,
            int pageNumber,
            int pageSize,
            string? status,
            DateTime? fromUtc,
            DateTime? toUtc,
            int? serviceId,
            string? searchName = null)
        {
            try
            {
                if (pageNumber < 1)
                {
                    return ApiResponse<BookingAppointmentListDto>.BadRequest(
                        "شماره صفحه باید بزرگتر از صفر باشد",
                        errorCode: ErrorCodes.InvalidInput);
                }

                if (pageSize < 1 || pageSize > 100)
                {
                    pageSize = 20;
                }

                if (!string.IsNullOrWhiteSpace(status) && !BookingAppointmentStatuses.IsValid(status))
                {
                    return ApiResponse<BookingAppointmentListDto>.BadRequest(
                        "وضعیت نامعتبر است",
                        errorCode: ErrorCodes.InvalidInput);
                }

                var system = await _systemRepository.GetByIdAndUserIdAsync(systemId, userId);
                if (system == null)
                {
                    return ApiResponse<BookingAppointmentListDto>.NotFound("سیستم رزرو یافت نشد");
                }

                var (items, totalCount) = await _appointmentRepository.GetBySystemIdAsync(
                    systemId, pageNumber, pageSize, status, fromUtc, toUtc, serviceId, searchName);

                var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);

                return ApiResponse<BookingAppointmentListDto>.CreateSuccess(new BookingAppointmentListDto
                {
                    Appointments = items.Select(MapToDto).ToList(),
                    TotalCount = totalCount,
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    TotalPages = totalPages
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading appointments for system {SystemId}, user {UserId}", systemId, userId);
                return ApiResponse<BookingAppointmentListDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<BookingAppointmentDto>> GetAppointmentByIdAsync(
            int systemId, int appointmentId, int userId)
        {
            try
            {
                var system = await _systemRepository.GetByIdAndUserIdAsync(systemId, userId);
                if (system == null)
                {
                    return ApiResponse<BookingAppointmentDto>.NotFound("سیستم رزرو یافت نشد");
                }

                var appointment = await _appointmentRepository.GetByIdAndSystemIdAsync(appointmentId, systemId);
                if (appointment == null)
                {
                    return ApiResponse<BookingAppointmentDto>.NotFound("نوبت یافت نشد");
                }

                return ApiResponse<BookingAppointmentDto>.CreateSuccess(MapToDto(appointment));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading appointment {AppointmentId}", appointmentId);
                return ApiResponse<BookingAppointmentDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<BookingPaymentReceiptDto>> GetPaymentReceiptAsync(
            int systemId, int appointmentId, int userId)
        {
            try
            {
                var system = await _systemRepository.GetByIdAndUserIdAsync(systemId, userId);
                if (system == null)
                {
                    return ApiResponse<BookingPaymentReceiptDto>.NotFound("سیستم رزرو یافت نشد");
                }

                var appointment = await _appointmentRepository.GetByIdAndSystemIdAsync(appointmentId, systemId);
                if (appointment == null)
                {
                    return ApiResponse<BookingPaymentReceiptDto>.NotFound("نوبت یافت نشد");
                }

                var hasReceipt = !string.IsNullOrWhiteSpace(appointment.PaymentReceiptPath);
                var url = hasReceipt
                    ? _fileUploadService.GetFileUrl(appointment.PaymentReceiptPath!)
                    : null;

                return ApiResponse<BookingPaymentReceiptDto>.CreateSuccess(new BookingPaymentReceiptDto
                {
                    AppointmentId = appointment.Id,
                    AppointmentNumber = appointment.Id,
                    HasPaymentReceipt = hasReceipt,
                    PaymentReceiptUrl = url,
                    CustomerFullName = appointment.CustomerFullName,
                    ServiceTitle = appointment.BookingServiceItem?.Title
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error loading payment receipt for appointment {AppointmentId}",
                    appointmentId);
                return ApiResponse<BookingPaymentReceiptDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<BookingAppointmentDto>> CreateManualBookingAsync(
            int systemId, int userId, CreateManualBookingDto dto)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var system = await _context.BookingSystems
                    .Include(b => b.Notebooks)
                    .FirstOrDefaultAsync(b => b.Id == systemId && b.UserId == userId && !b.IsDeleted);

                if (system == null)
                {
                    return ApiResponse<BookingAppointmentDto>.NotFound("سیستم رزرو یافت نشد");
                }

                var created = await CreateAppointmentInternalAsync(
                    system,
                    dto.ServiceId,
                    dto.StartUtc,
                    dto.CustomerFullName,
                    dto.CustomerMobile,
                    dto.CustomerNote,
                    BookingAppointmentStatuses.Confirmed,
                    requireFutureSlot: false,
                    remindersEnabled: dto.RemindersEnabled ?? true);

                if (!created.Success)
                {
                    await transaction.RollbackAsync();
                    return ApiResponse<BookingAppointmentDto>.Error(
                        created.ErrorMessage!,
                        created.StatusCode,
                        errorCode: created.ErrorCode);
                }

                await transaction.CommitAsync();

                await _audit.WriteAsync(new AuditEntry
                {
                    Category = AuditCategories.Booking,
                    Action = AuditActions.BookingAppointmentCreated,
                    EntityType = AuditEntityTypes.BookingAppointment,
                    EntityId = created.Appointment!.Id.ToString(),
                    ActorUserId = userId,
                    After = new { status = created.Appointment.Status, startUtc = created.Appointment.StartUtc }
                });

                return ApiResponse<BookingAppointmentDto>.CreateSuccess(
                    MapToDto(created.Appointment!),
                    "نوبت دستی با موفقیت ثبت شد",
                    201);
            }
            catch (DbUpdateException dbEx)
            {
                await transaction.RollbackAsync();
                return BookingDbExceptionHelper.MapDbUpdateException<BookingAppointmentDto>(
                    dbEx, _logger, "creating manual booking");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error creating manual booking for system {SystemId}", systemId);
                return ApiResponse<BookingAppointmentDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<BookingAppointmentDto>> UpdateAppointmentAsync(
            int systemId, int appointmentId, int userId, UpdateBookingAppointmentDto dto)
        {
            if (dto == null ||
                (dto.CustomerFullName == null &&
                 dto.CustomerMobile == null &&
                 dto.CustomerNote == null &&
                 !dto.ServiceId.HasValue &&
                 !dto.StartUtc.HasValue &&
                 !dto.RemindersEnabled.HasValue))
            {
                return ApiResponse<BookingAppointmentDto>.BadRequest(
                    "هیچ موردی برای به‌روزرسانی ارسال نشده است",
                    errorCode: ErrorCodes.ValidationFailed);
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var system = await _systemRepository.GetByIdAndUserIdAsync(systemId, userId);
                if (system == null)
                {
                    await transaction.RollbackAsync();
                    return ApiResponse<BookingAppointmentDto>.NotFound("سیستم رزرو یافت نشد");
                }

                var appointment = await _context.BookingAppointments
                    .Include(a => a.BookingServiceItem)
                    .FirstOrDefaultAsync(a =>
                        a.Id == appointmentId &&
                        a.BookingSystemId == systemId &&
                        !a.IsDeleted);

                if (appointment == null)
                {
                    await transaction.RollbackAsync();
                    return ApiResponse<BookingAppointmentDto>.NotFound("نوبت یافت نشد");
                }

                if (appointment.Status == BookingAppointmentStatuses.Cancelled)
                {
                    await transaction.RollbackAsync();
                    return ApiResponse<BookingAppointmentDto>.BadRequest(
                        "نوبت لغو شده قابل ویرایش نیست",
                        errorCode: ErrorCodes.ValidationFailed);
                }

                if (dto.CustomerFullName != null)
                {
                    if (string.IsNullOrWhiteSpace(dto.CustomerFullName))
                    {
                        await transaction.RollbackAsync();
                        return ApiResponse<BookingAppointmentDto>.BadRequest("نام نمی‌تواند خالی باشد");
                    }

                    appointment.CustomerFullName = dto.CustomerFullName.Trim();
                }

                if (dto.CustomerMobile != null)
                {
                    var mobile = BookingMobileHelper.Normalize(dto.CustomerMobile);
                    if (!BookingMobileHelper.IsValidIranianMobile(mobile))
                    {
                        await transaction.RollbackAsync();
                        return ApiResponse<BookingAppointmentDto>.BadRequest(
                            "شماره موبایل نامعتبر است",
                            errorCode: ErrorCodes.ValidationFailed);
                    }

                    appointment.CustomerMobile = mobile;
                }

                if (dto.CustomerNote != null)
                {
                    appointment.CustomerNote = string.IsNullOrWhiteSpace(dto.CustomerNote)
                        ? null
                        : dto.CustomerNote.Trim();
                }

                var serviceId = dto.ServiceId ?? appointment.BookingServiceItemId;
                var startUtc = dto.StartUtc.HasValue
                    ? NormalizeUtc(dto.StartUtc.Value)
                    : EnsureUtc(appointment.StartUtc);

                var currentStartUtc = EnsureUtc(appointment.StartUtc);
                var serviceChanged = serviceId != appointment.BookingServiceItemId;
                var timeChanged = startUtc != currentStartUtc;

                if (serviceChanged || timeChanged)
                {
                    // AsNoTracking — فقط برای اعتبارسنجی/مدت؛ به navigation انتساب نده
                    // (در غیر این صورت با Include قبلی conflict tracking و 500 می‌شود)
                    var service = await _appointmentRepository.GetServiceForBookingAsync(systemId, serviceId);
                    if (service == null)
                    {
                        await transaction.RollbackAsync();
                        return ApiResponse<BookingAppointmentDto>.NotFound("خدمت یافت نشد");
                    }

                    var date = DateOnly.FromDateTime(startUtc);
                    var existing = await _appointmentRepository.GetAppointmentsForServiceOnDateAsync(serviceId, date);
                    existing = existing.Where(a => a.Id != appointmentId).ToList();
                    var blocked = await _appointmentRepository.GetBlockedStartsForSystemOnDateAsync(systemId, date);

                    if (!BookingSlotCalculator.IsSlotAvailable(service, startUtc, existing, blocked))
                    {
                        await transaction.RollbackAsync();
                        return ApiResponse<BookingAppointmentDto>.BadRequest(
                            "این زمان دیگر در دسترس نیست",
                            errorCode: ErrorCodes.ValidationFailed);
                    }

                    if (serviceChanged)
                    {
                        // قطع navigation قدیمی تا EF فقط FK را آپدیت کند
                        appointment.BookingServiceItem = null!;
                        appointment.BookingServiceItemId = serviceId;
                    }

                    appointment.StartUtc = startUtc;
                    appointment.EndUtc = startUtc.AddMinutes(service.DurationMinutes);
                }

                if (dto.RemindersEnabled.HasValue)
                {
                    appointment.RemindersEnabled = dto.RemindersEnabled.Value;
                }

                appointment.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                await _context.Entry(appointment).Reference(a => a.BookingServiceItem).LoadAsync();

                return ApiResponse<BookingAppointmentDto>.CreateSuccess(MapToDto(appointment), "نوبت به‌روزرسانی شد");
            }
            catch (DbUpdateException dbEx)
            {
                await transaction.RollbackAsync();
                return BookingDbExceptionHelper.MapDbUpdateException<BookingAppointmentDto>(
                    dbEx, _logger, "updating appointment", appointmentId, userId);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error updating appointment {AppointmentId}", appointmentId);
                return ApiResponse<BookingAppointmentDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<BookingAppointmentDto>> ConfirmAppointmentAsync(
            int systemId, int appointmentId, int userId)
        {
            try
            {
                var system = await _systemRepository.GetByIdAndUserIdAsync(systemId, userId);
                if (system == null)
                {
                    return ApiResponse<BookingAppointmentDto>.NotFound("سیستم رزرو یافت نشد");
                }

                var appointment = await _context.BookingAppointments
                    .Include(a => a.BookingServiceItem)
                    .Include(a => a.BookingSystem)
                    .FirstOrDefaultAsync(a =>
                        a.Id == appointmentId &&
                        a.BookingSystemId == systemId &&
                        !a.IsDeleted);

                if (appointment == null)
                {
                    return ApiResponse<BookingAppointmentDto>.NotFound("نوبت یافت نشد");
                }

                if (appointment.Status == BookingAppointmentStatuses.Confirmed)
                {
                    return ApiResponse<BookingAppointmentDto>.CreateSuccess(MapToDto(appointment), "نوبت قبلاً تأیید شده است");
                }

                if (appointment.Status != BookingAppointmentStatuses.Pending)
                {
                    return ApiResponse<BookingAppointmentDto>.BadRequest(
                        "فقط نوبت‌های در انتظار تأیید قابل تأیید هستند",
                        errorCode: ErrorCodes.ValidationFailed);
                }

                var previousStatus = appointment.Status;
                appointment.Status = BookingAppointmentStatuses.Confirmed;
                appointment.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                await _audit.WriteAsync(new AuditEntry
                {
                    Category = AuditCategories.Booking,
                    Action = AuditActions.BookingAppointmentStatusChanged,
                    EntityType = AuditEntityTypes.BookingAppointment,
                    EntityId = appointment.Id.ToString(),
                    ActorUserId = userId,
                    Before = new { status = previousStatus },
                    After = new { status = appointment.Status }
                });

                await TrySendStatusSmsAsync(appointment, isConfirmed: true);

                return ApiResponse<BookingAppointmentDto>.CreateSuccess(MapToDto(appointment), "نوبت تأیید شد");
            }
            catch (DbUpdateException dbEx)
            {
                return BookingDbExceptionHelper.MapDbUpdateException<BookingAppointmentDto>(
                    dbEx, _logger, "confirming appointment", appointmentId, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error confirming appointment {AppointmentId}", appointmentId);
                return ApiResponse<BookingAppointmentDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<BookingAppointmentDto>> CancelAppointmentAsync(
            int systemId,
            int appointmentId,
            int userId,
            CancelBookingAppointmentDto? dto)
        {
            try
            {
                var system = await _systemRepository.GetByIdAndUserIdAsync(systemId, userId);
                if (system == null)
                {
                    return ApiResponse<BookingAppointmentDto>.NotFound("سیستم رزرو یافت نشد");
                }

                var appointment = await _context.BookingAppointments
                    .Include(a => a.BookingServiceItem)
                    .Include(a => a.BookingSystem)
                    .FirstOrDefaultAsync(a =>
                        a.Id == appointmentId &&
                        a.BookingSystemId == systemId &&
                        !a.IsDeleted);

                if (appointment == null)
                {
                    return ApiResponse<BookingAppointmentDto>.NotFound("نوبت یافت نشد");
                }

                if (appointment.Status == BookingAppointmentStatuses.Cancelled)
                {
                    return ApiResponse<BookingAppointmentDto>.BadRequest(
                        "این نوبت قبلاً لغو شده است",
                        errorCode: ErrorCodes.ValidationFailed);
                }

                var previousStatus = appointment.Status;
                appointment.Status = BookingAppointmentStatuses.Cancelled;
                appointment.CancelledAt = DateTime.UtcNow;
                appointment.CancellationReason = string.IsNullOrWhiteSpace(dto?.Reason) ? null : dto.Reason.Trim();
                appointment.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                await _audit.WriteAsync(new AuditEntry
                {
                    Category = AuditCategories.Booking,
                    Action = AuditActions.BookingAppointmentCancelled,
                    EntityType = AuditEntityTypes.BookingAppointment,
                    EntityId = appointment.Id.ToString(),
                    ActorUserId = userId,
                    Before = new { status = previousStatus },
                    After = new { status = appointment.Status, reason = appointment.CancellationReason }
                });

                await TrySendStatusSmsAsync(appointment, isConfirmed: false);

                return ApiResponse<BookingAppointmentDto>.CreateSuccess(MapToDto(appointment), "نوبت لغو شد");
            }
            catch (DbUpdateException dbEx)
            {
                return BookingDbExceptionHelper.MapDbUpdateException<BookingAppointmentDto>(
                    dbEx, _logger, "cancelling appointment", appointmentId, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cancelling appointment {AppointmentId}", appointmentId);
                return ApiResponse<BookingAppointmentDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<BookingDayAvailabilityDto>> GetDayAvailabilityAsync(
            int systemId, int userId, DateOnly date, int? serviceId = null)
        {
            try
            {
                var system = await _systemRepository.GetByIdAndUserIdAsync(systemId, userId);
                if (system == null)
                {
                    return ApiResponse<BookingDayAvailabilityDto>.NotFound("سیستم رزرو یافت نشد");
                }

                var service = await ResolveServiceForAvailabilityAsync(systemId, serviceId);
                if (service == null)
                {
                    return ApiResponse<BookingDayAvailabilityDto>.BadRequest(
                        "خدمتی برای مدیریت وقت خالی یافت نشد",
                        errorCode: ErrorCodes.ValidationFailed);
                }

                var dto = await BuildDayAvailabilityAsync(systemId, service, date);
                return ApiResponse<BookingDayAvailabilityDto>.CreateSuccess(dto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading day availability for system {SystemId}", systemId);
                return ApiResponse<BookingDayAvailabilityDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<BookingDayAvailabilityDto>> SaveDayAvailabilityAsync(
            int systemId, int userId, SaveBookingDayAvailabilityDto dto)
        {
            try
            {
                var system = await _systemRepository.GetByIdAndUserIdAsync(systemId, userId);
                if (system == null)
                {
                    return ApiResponse<BookingDayAvailabilityDto>.NotFound("سیستم رزرو یافت نشد");
                }

                if (dto.Slots == null || dto.Slots.Count == 0)
                {
                    return ApiResponse<BookingDayAvailabilityDto>.BadRequest(
                        "لیست اسلات‌ها الزامی است",
                        errorCode: ErrorCodes.ValidationFailed);
                }

                var service = await ResolveServiceForAvailabilityAsync(systemId, dto.ServiceId);
                if (service == null)
                {
                    return ApiResponse<BookingDayAvailabilityDto>.BadRequest(
                        "خدمتی برای مدیریت وقت خالی یافت نشد",
                        errorCode: ErrorCodes.ValidationFailed);
                }

                var dayStart = dto.Date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
                var dayEnd = dayStart.AddDays(1);

                var existingBlocks = await _context.BookingSlotBlocks
                    .Where(b =>
                        b.BookingSystemId == systemId &&
                        b.SlotStartUtc >= dayStart &&
                        b.SlotStartUtc < dayEnd)
                    .ToListAsync();

                var reservedStarts = (await _appointmentRepository.GetAppointmentsForServiceOnDateAsync(service.Id, dto.Date))
                    .Select(a => NormalizeUtc(a.StartUtc))
                    .ToHashSet();

                var blockByStart = existingBlocks.ToDictionary(b => NormalizeUtc(b.SlotStartUtc));

                foreach (var slot in dto.Slots)
                {
                    var start = NormalizeUtc(slot.StartUtc);
                    if (start < dayStart || start >= dayEnd)
                    {
                        return ApiResponse<BookingDayAvailabilityDto>.BadRequest(
                            "زمان اسلات با تاریخ انتخاب‌شده مطابقت ندارد",
                            errorCode: ErrorCodes.ValidationFailed);
                    }

                    if (reservedStarts.Contains(start))
                    {
                        continue;
                    }

                    if (!slot.IsEnabled)
                    {
                        if (!blockByStart.ContainsKey(start))
                        {
                            var block = new BookingSlotBlock
                            {
                                BookingSystemId = systemId,
                                SlotStartUtc = start,
                                CreatedAt = DateTime.UtcNow
                            };
                            await _context.BookingSlotBlocks.AddAsync(block);
                            blockByStart[start] = block;
                        }
                    }
                    else if (blockByStart.TryGetValue(start, out var existing))
                    {
                        _context.BookingSlotBlocks.Remove(existing);
                        blockByStart.Remove(start);
                    }
                }

                await _context.SaveChangesAsync();

                var result = await BuildDayAvailabilityAsync(systemId, service, dto.Date);
                return ApiResponse<BookingDayAvailabilityDto>.CreateSuccess(result, "تغییرات وقت خالی ذخیره شد");
            }
            catch (DbUpdateException dbEx)
            {
                return BookingDbExceptionHelper.MapDbUpdateException<BookingDayAvailabilityDto>(
                    dbEx, _logger, "saving day availability");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving day availability for system {SystemId}", systemId);
                return ApiResponse<BookingDayAvailabilityDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        public async Task ProcessRemindersAsync(CancellationToken cancellationToken = default)
        {
            var now = DateTime.UtcNow;

            var candidates = await _appointmentRepository.GetPendingRemindersAsync(now, MaxReminderOffsetMinutes);

            foreach (var candidate in candidates)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                if (!candidate.RemindersEnabled)
                {
                    continue;
                }

                var offsets = BookingReminderOffsetsHelper.FromJson(
                    candidate.BookingServiceItem?.ReminderOffsetsJson,
                    candidate.BookingServiceItem?.ReminderOffsetMinutes ?? 0);

                if (offsets.Count == 0)
                {
                    continue;
                }

                var alreadySent = BookingReminderOffsetsHelper.ParseSentOffsets(candidate.ReminderSentOffsetsCsv);
                // سازگاری: اگر ReminderSentAt قدیمی پر است ولی CSV خالی، همه offsetهای فعلی را ارسال‌شده فرض کن
                if (alreadySent.Count == 0 &&
                    candidate.ReminderSentAt.HasValue &&
                    string.IsNullOrWhiteSpace(candidate.ReminderSentOffsetsCsv))
                {
                    foreach (var o in offsets)
                    {
                        alreadySent.Add(o);
                    }
                }

                var dueOffsets = offsets
                    .Where(o => !alreadySent.Contains(o))
                    .Where(o => candidate.StartUtc.AddMinutes(-o) <= now)
                    .OrderByDescending(o => o) // اول فاصله‌های بزرگ‌تر (روز قبل)
                    .ToList();

                if (dueOffsets.Count == 0)
                {
                    continue;
                }

                var tracked = await _context.BookingAppointments
                    .FirstOrDefaultAsync(a =>
                        a.Id == candidate.Id &&
                        a.RemindersEnabled &&
                        a.Status == BookingAppointmentStatuses.Confirmed &&
                        a.StartUtc > now,
                        cancellationToken);

                if (tracked == null)
                {
                    continue;
                }

                var sentNow = BookingReminderOffsetsHelper.ParseSentOffsets(tracked.ReminderSentOffsetsCsv);
                if (sentNow.Count == 0 &&
                    tracked.ReminderSentAt.HasValue &&
                    string.IsNullOrWhiteSpace(tracked.ReminderSentOffsetsCsv))
                {
                    foreach (var o in offsets)
                    {
                        sentNow.Add(o);
                    }
                }

                var message = BuildReminderMessage(tracked, candidate.BookingSystem, candidate.BookingServiceItem!);
                if (!message.TrimEnd().EndsWith("لغو11"))
                {
                    message = $"{message.TrimEnd()}\nلغو11";
                }

                foreach (var offset in dueOffsets)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }

                    if (sentNow.Contains(offset))
                    {
                        continue;
                    }

                    try
                    {
                        var sendResult = await _userSmsBilling.TrySendAsync(
                            candidate.BookingSystem.UserId,
                            tracked.CustomerMobile,
                            message,
                            SmsSourceModules.BookingReminder,
                            "یادآوری نوبت",
                            $"هزینه پیامک یادآوری نوبت #{tracked.Id} ({offset} دقیقه قبل)",
                            tracked.Id,
                            candidate.BookingSystem.Title,
                            cancellationToken);

                        if (sendResult.SkippedInsufficientBalance)
                        {
                            _logger.LogInformation(
                                "Booking reminder SMS skipped (insufficient wallet) for appointment {AppointmentId} offset {Offset} — will retry until start",
                                tracked.Id,
                                offset);
                            break;
                        }

                        if (!sendResult.Sent)
                        {
                            _logger.LogWarning(
                                "Booking reminder SMS failed for appointment {AppointmentId} offset {Offset}: {Message}",
                                tracked.Id,
                                offset,
                                sendResult.Message);
                            // offsetهای دیگر را هم امتحان کن (شاید موقتی باشد)
                            continue;
                        }

                        sentNow.Add(offset);
                        tracked.ReminderSentOffsetsCsv = BookingReminderOffsetsHelper.FormatSentOffsets(sentNow);
                        tracked.ReminderSentAt = DateTime.UtcNow;
                        tracked.UpdatedAt = DateTime.UtcNow;
                        await _context.SaveChangesAsync(cancellationToken);

                        _logger.LogInformation(
                            "Booking reminder sent for appointment {AppointmentId} offset {Offset}",
                            tracked.Id,
                            offset);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(
                            ex,
                            "Failed to send booking reminder for appointment {AppointmentId} offset {Offset}",
                            tracked.Id,
                            offset);
                        break;
                    }
                }
            }
        }

        private sealed class AppointmentCreateResult
        {
            public bool Success { get; init; }
            public BookingAppointment? Appointment { get; init; }
            public string? ErrorMessage { get; init; }
            public string? ErrorCode { get; init; }
            public int StatusCode { get; init; } = 400;

            public static AppointmentCreateResult Ok(BookingAppointment appointment) => new()
            {
                Success = true,
                Appointment = appointment
            };

            public static AppointmentCreateResult Fail(string message, int statusCode = 400, string? errorCode = null) => new()
            {
                Success = false,
                ErrorMessage = message,
                StatusCode = statusCode,
                ErrorCode = errorCode
            };
        }

        private async Task<AppointmentCreateResult> CreateAppointmentInternalAsync(
            BookingSystem system,
            int serviceId,
            DateTime startUtcRaw,
            string customerFullName,
            string customerMobile,
            string? customerNote,
            string status,
            bool requireFutureSlot,
            bool remindersEnabled = true)
        {
            var mobile = BookingMobileHelper.Normalize(customerMobile);
            if (!BookingMobileHelper.IsValidIranianMobile(mobile))
            {
                return AppointmentCreateResult.Fail(
                    "شماره موبایل نامعتبر است",
                    errorCode: ErrorCodes.ValidationFailed);
            }

            if (string.IsNullOrWhiteSpace(customerFullName))
            {
                return AppointmentCreateResult.Fail(
                    "نام الزامی است",
                    errorCode: ErrorCodes.ValidationFailed);
            }

            var service = await _appointmentRepository.GetServiceForBookingAsync(system.Id, serviceId);
            if (service == null)
            {
                return AppointmentCreateResult.Fail("خدمت یافت نشد", 404, ErrorCodes.NotFound);
            }

            var startUtc = NormalizeUtc(startUtcRaw);
            if (requireFutureSlot && startUtc <= DateTime.UtcNow)
            {
                return AppointmentCreateResult.Fail(
                    "زمان انتخاب‌شده گذشته است",
                    errorCode: ErrorCodes.InvalidInput);
            }

            var date = DateOnly.FromDateTime(startUtc);
            var existing = await _appointmentRepository.GetAppointmentsForServiceOnDateAsync(service.Id, date);
            var blocked = await _appointmentRepository.GetBlockedStartsForSystemOnDateAsync(system.Id, date);

            if (!BookingSlotCalculator.IsSlotAvailable(service, startUtc, existing, blocked))
            {
                return AppointmentCreateResult.Fail(
                    "این زمان دیگر در دسترس نیست",
                    errorCode: ErrorCodes.ValidationFailed);
            }

            var now = DateTime.UtcNow;
            var appointment = new BookingAppointment
            {
                BookingSystemId = system.Id,
                BookingServiceItemId = service.Id,
                CustomerFullName = customerFullName.Trim(),
                CustomerMobile = mobile,
                CustomerNote = string.IsNullOrWhiteSpace(customerNote) ? null : customerNote.Trim(),
                StartUtc = startUtc,
                EndUtc = startUtc.AddMinutes(service.DurationMinutes),
                Status = status,
                RemindersEnabled = remindersEnabled,
                CreatedAt = now
            };

            await _context.BookingAppointments.AddAsync(appointment);
            await _context.SaveChangesAsync();

            if (system.SaveToPhonebook && system.Notebooks.Count > 0)
            {
                var notebookIds = system.Notebooks.Select(n => n.ContactNotebookId).ToList();
                var contactId = await _phonebookService.SaveParticipantAsync(
                    notebookIds, mobile, appointment.CustomerFullName);
                if (contactId.HasValue)
                {
                    appointment.ContactId = contactId;
                    appointment.UpdatedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                }
            }

            // سرویس از AsNoTracking آمده — به navigation انتساب نده (conflict با Include بعدی)
            await _context.Entry(appointment).Reference(a => a.BookingServiceItem).LoadAsync();
            return AppointmentCreateResult.Ok(appointment);
        }

        private async Task<BookingServiceItem?> ResolveServiceForAvailabilityAsync(int systemId, int? serviceId)
        {
            if (serviceId.HasValue)
            {
                return await _appointmentRepository.GetServiceForBookingAsync(systemId, serviceId.Value);
            }

            return await _context.BookingServiceItems
                .Include(s => s.DaySchedules)
                .Include(s => s.ScheduleExceptions.Where(e => !e.IsDeleted))
                .AsNoTracking()
                .Where(s => s.BookingSystemId == systemId && !s.IsDeleted)
                .OrderBy(s => s.SortOrder)
                .FirstOrDefaultAsync();
        }

        private async Task<BookingDayAvailabilityDto> BuildDayAvailabilityAsync(
            int systemId, BookingServiceItem service, DateOnly date)
        {
            var allSlots = BookingSlotCalculator.CalculateAllSlots(service, date);
            var appointments = await _appointmentRepository.GetAppointmentsForServiceOnDateAsync(service.Id, date);
            var blocked = (await _appointmentRepository.GetBlockedStartsForSystemOnDateAsync(systemId, date))
                .Select(NormalizeUtc)
                .ToHashSet();

            var appointmentByStart = appointments
                .GroupBy(a => NormalizeUtc(a.StartUtc))
                .ToDictionary(g => g.Key, g => g.First());

            var managed = allSlots.Select(slot =>
            {
                var start = NormalizeUtc(slot.StartUtc);
                if (appointmentByStart.TryGetValue(start, out var appointment))
                {
                    return new BookingManagedSlotDto
                    {
                        StartUtc = slot.StartUtc,
                        EndUtc = slot.EndUtc,
                        Status = BookingManagedSlotStatuses.Reserved,
                        IsEnabled = true,
                        AppointmentId = appointment.Id,
                        CustomerFullName = appointment.CustomerFullName
                    };
                }

                if (blocked.Contains(start))
                {
                    return new BookingManagedSlotDto
                    {
                        StartUtc = slot.StartUtc,
                        EndUtc = slot.EndUtc,
                        Status = BookingManagedSlotStatuses.Blocked,
                        IsEnabled = false
                    };
                }

                return new BookingManagedSlotDto
                {
                    StartUtc = slot.StartUtc,
                    EndUtc = slot.EndUtc,
                    Status = BookingManagedSlotStatuses.Empty,
                    IsEnabled = true
                };
            }).ToList();

            return new BookingDayAvailabilityDto
            {
                SystemId = systemId,
                ServiceId = service.Id,
                ServiceTitle = service.Title,
                Date = date,
                Slots = managed
            };
        }

        private const string BookingNotFoundMessage = "صفحه رزرو یافت نشد";
        private const string BookingInactiveMessage = "این صفحه رزرو در حال حاضر فعال نیست و امکان ثبت نوبت وجود ندارد";

        private static ApiResponse<T>? EnsurePubliclyAvailable<T>(BookingSystem? system)
        {
            if (system == null)
            {
                return ApiResponse<T>.NotFound(BookingNotFoundMessage);
            }

            if (!system.IsActive)
            {
                return ApiResponse<T>.Forbidden(BookingInactiveMessage, ErrorCodes.ResourceInactive);
            }

            return null;
        }

        private static BookingPublicSystemDto MapToPublicDto(BookingSystem system) => new()
        {
            Title = system.Title,
            Description = system.Description,
            Location = system.Location,
            ActivityType = system.ActivityType,
            ActivityTypeTitle = BookingActivityTypes.GetTitle(system.ActivityType),
            Slug = system.Slug,
            Services = system.Services
                .Where(s => !s.IsDeleted)
                .OrderBy(s => s.SortOrder)
                .Select(s => new BookingPublicServiceDto
                {
                    Id = s.Id,
                    Title = s.Title,
                    DurationMinutes = s.DurationMinutes,
                    HasCost = s.HasCost,
                    Price = s.Price,
                    DepositAmount = s.DepositAmount,
                    ReminderOffsetsMinutes = BookingReminderOffsetsHelper.FromJson(
                        s.ReminderOffsetsJson,
                        s.ReminderOffsetMinutes)
                })
                .ToList()
        };

        private static BookingAppointmentDto MapToDto(BookingAppointment appointment) => new()
        {
            Id = appointment.Id,
            AppointmentNumber = appointment.Id,
            BookingSystemId = appointment.BookingSystemId,
            ServiceId = appointment.BookingServiceItemId,
            ServiceTitle = appointment.BookingServiceItem?.Title ?? string.Empty,
            CustomerFullName = appointment.CustomerFullName,
            CustomerMobile = appointment.CustomerMobile,
            CustomerNote = appointment.CustomerNote,
            StartUtc = EnsureUtc(appointment.StartUtc),
            EndUtc = EnsureUtc(appointment.EndUtc),
            Status = appointment.Status,
            RemindersEnabled = appointment.RemindersEnabled,
            ReminderSentAt = appointment.ReminderSentAt.HasValue ? EnsureUtc(appointment.ReminderSentAt.Value) : null,
            ReminderOffsetsSent = BookingReminderOffsetsHelper.ParseSentOffsets(appointment.ReminderSentOffsetsCsv)
                .OrderBy(x => x)
                .ToList(),
            CancelledAt = appointment.CancelledAt.HasValue ? EnsureUtc(appointment.CancelledAt.Value) : null,
            CancellationReason = appointment.CancellationReason,
            CreatedAt = EnsureUtc(appointment.CreatedAt),
            HasPaymentReceipt = !string.IsNullOrWhiteSpace(appointment.PaymentReceiptPath)
        };

        private static PublicBookingStatusDto MapToPublicStatusDto(BookingAppointment appointment) => new()
        {
            AppointmentNumber = appointment.Id,
            Status = appointment.Status,
            StatusTitle = GetStatusTitle(appointment.Status),
            BusinessTitle = appointment.BookingSystem?.Title ?? string.Empty,
            ServiceTitle = appointment.BookingServiceItem?.Title ?? string.Empty,
            CustomerFullName = appointment.CustomerFullName,
            CustomerMobileMasked = MaskMobile(appointment.CustomerMobile),
            RemindersEnabled = appointment.RemindersEnabled,
            StartUtc = EnsureUtc(appointment.StartUtc),
            EndUtc = EnsureUtc(appointment.EndUtc)
        };

        private static string GetStatusTitle(string status) => status switch
        {
            BookingAppointmentStatuses.Pending => "در انتظار تأیید",
            BookingAppointmentStatuses.Confirmed => "تأیید شده",
            BookingAppointmentStatuses.Cancelled => "لغو شده",
            BookingAppointmentStatuses.Completed => "انجام شده",
            _ => status
        };

        private static string MaskMobile(string? mobile)
        {
            var normalized = BookingMobileHelper.Normalize(mobile);
            if (normalized.Length < 8)
            {
                return "***";
            }

            return $"{normalized[..4]}***{normalized[^4..]}";
        }

        private async Task TrySendStatusSmsAsync(BookingAppointment appointment, bool isConfirmed)
        {
            try
            {
                var system = appointment.BookingSystem;
                var service = appointment.BookingServiceItem;
                if (system == null || service == null || string.IsNullOrWhiteSpace(appointment.CustomerMobile))
                {
                    return;
                }

                var whenLocal = FormatTehranDateTime(appointment.StartUtc);
                var message = isConfirmed
                    ? $"نوبت شما تأیید شد\n" +
                      $"{system.Title}\n" +
                      $"شماره نوبت: #{appointment.Id}\n" +
                      $"خدمت: {service.Title}\n" +
                      $"زمان: {whenLocal}"
                    : $"نوبت شما لغو شد\n" +
                      $"{system.Title}\n" +
                      $"شماره نوبت: #{appointment.Id}\n" +
                      $"خدمت: {service.Title}\n" +
                      $"زمان: {whenLocal}";

                var sendResult = await _userSmsBilling.TrySendAsync(
                    system.UserId,
                    appointment.CustomerMobile,
                    message,
                    SmsSourceModules.BookingStatus,
                    isConfirmed ? "تأیید نوبت" : "لغو نوبت",
                    $"هزینه پیامک وضعیت نوبت #{appointment.Id}",
                    appointment.Id,
                    system.Title);

                if (sendResult.SkippedInsufficientBalance)
                {
                    _logger.LogInformation(
                        "Booking status SMS skipped (insufficient wallet) for appointment {AppointmentId} confirmed={IsConfirmed}",
                        appointment.Id,
                        isConfirmed);
                    return;
                }

                if (!sendResult.Sent)
                {
                    _logger.LogWarning(
                        "Booking status SMS failed for appointment {AppointmentId} confirmed={IsConfirmed}: {Message}",
                        appointment.Id,
                        isConfirmed,
                        sendResult.Message);
                    return;
                }

                _logger.LogInformation(
                    "Booking status SMS sent for appointment {AppointmentId} confirmed={IsConfirmed}",
                    appointment.Id,
                    isConfirmed);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to send booking status SMS for appointment {AppointmentId}",
                    appointment.Id);
            }
        }

        private string BuildPublicUrl(string slug)
        {
            var baseUrl = string.IsNullOrWhiteSpace(_options.PublicBaseUrl)
                ? "https://app.com/book"
                : _options.PublicBaseUrl.TrimEnd('/');
            return $"{baseUrl}/{slug}";
        }

        private static string BuildReminderMessage(
            BookingAppointment appointment,
            BookingSystem system,
            BookingServiceItem service)
        {
            return BookingReminderOffsetsHelper.BuildMessage(
                system.Title,
                service.Title,
                FormatTehranDateTime(appointment.StartUtc));
        }

        private static string FormatTehranDateTime(DateTime utc)
        {
            var local = TimeZoneInfo.ConvertTimeFromUtc(EnsureUtc(utc), TehranTimeZone);
            return local.ToString("yyyy/MM/dd HH:mm");
        }

        private static readonly TimeZoneInfo TehranTimeZone = ResolveTehranTimeZone();

        private static TimeZoneInfo ResolveTehranTimeZone()
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("Asia/Tehran");
            }
            catch (TimeZoneNotFoundException)
            {
                return TimeZoneInfo.FindSystemTimeZoneById("Iran Standard Time");
            }
        }

        private static DateTime NormalizeUtc(DateTime value) =>
            value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc);

        private static DateTime EnsureUtc(DateTime value) =>
            value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc);
    }
}
