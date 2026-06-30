using Api_Vapp.Constants;
using Api_Vapp.Data;
using Api_Vapp.DTOs.BookingSystem;
using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.Sms;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;
using Api_Vapp.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api_Vapp.Services
{
    public class BookingAppointmentService : IBookingAppointmentService
    {
        private readonly Api_Context _context;
        private readonly IBookingAppointmentRepository _appointmentRepository;
        private readonly IBookingSystemRepository _systemRepository;
        private readonly ISmsService _smsService;
        private readonly ISmsDeliveryTrackingService _deliveryTracking;
        private readonly ILogger<BookingAppointmentService> _logger;

        private const int ReminderWindowMinutes = 2;
        private const int MaxReminderOffsetMinutes = 43200;

        public BookingAppointmentService(
            Api_Context context,
            IBookingAppointmentRepository appointmentRepository,
            IBookingSystemRepository systemRepository,
            ISmsService smsService,
            ISmsDeliveryTrackingService deliveryTracking,
            ILogger<BookingAppointmentService> logger)
        {
            _context = context;
            _appointmentRepository = appointmentRepository;
            _systemRepository = systemRepository;
            _smsService = smsService;
            _deliveryTracking = deliveryTracking;
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
                if (system == null)
                {
                    return ApiResponse<BookingPublicSystemDto>.NotFound("صفحه رزرو یافت نشد یا غیرفعال است");
                }

                return ApiResponse<BookingPublicSystemDto>.CreateSuccess(MapToPublicDto(system));
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

                if (date < DateOnly.FromDateTime(DateTime.UtcNow))
                {
                    return ApiResponse<BookingAvailableSlotsDto>.BadRequest(
                        "تاریخ گذشته قابل رزرو نیست",
                        errorCode: ErrorCodes.InvalidInput);
                }

                var existing = await _appointmentRepository.GetAppointmentsForServiceOnDateAsync(serviceId, date);
                var slots = BookingSlotCalculator.CalculateAvailableSlots(service, date, existing);

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
            string slug, CreatePublicBookingDto dto)
        {
            if (string.IsNullOrWhiteSpace(slug))
            {
                return ApiResponse<CreatePublicBookingResponseDto>.BadRequest(
                    "لینک نامعتبر است",
                    errorCode: ErrorCodes.InvalidInput);
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
                        b.IsActive &&
                        b.Status == BookingSystemStatus.Published);

                if (system == null)
                {
                    return ApiResponse<CreatePublicBookingResponseDto>.NotFound("صفحه رزرو یافت نشد");
                }

                var mobile = BookingMobileHelper.Normalize(dto.CustomerMobile);
                if (!BookingMobileHelper.IsValidIranianMobile(mobile))
                {
                    return ApiResponse<CreatePublicBookingResponseDto>.BadRequest(
                        "شماره موبایل نامعتبر است",
                        errorCode: ErrorCodes.ValidationFailed);
                }

                if (string.IsNullOrWhiteSpace(dto.CustomerFullName))
                {
                    return ApiResponse<CreatePublicBookingResponseDto>.BadRequest(
                        "نام الزامی است",
                        errorCode: ErrorCodes.ValidationFailed);
                }

                var service = await _appointmentRepository.GetServiceForBookingAsync(system.Id, dto.ServiceId);
                if (service == null)
                {
                    return ApiResponse<CreatePublicBookingResponseDto>.NotFound("خدمت یافت نشد");
                }

                var startUtc = NormalizeUtc(dto.StartUtc);
                if (startUtc <= DateTime.UtcNow)
                {
                    return ApiResponse<CreatePublicBookingResponseDto>.BadRequest(
                        "زمان انتخاب‌شده گذشته است",
                        errorCode: ErrorCodes.InvalidInput);
                }

                var date = DateOnly.FromDateTime(startUtc);
                var existing = await _appointmentRepository.GetAppointmentsForServiceOnDateAsync(service.Id, date);

                if (!BookingSlotCalculator.IsSlotAvailable(service, startUtc, existing))
                {
                    return ApiResponse<CreatePublicBookingResponseDto>.BadRequest(
                        "این زمان دیگر در دسترس نیست",
                        errorCode: ErrorCodes.ValidationFailed);
                }

                var now = DateTime.UtcNow;
                var appointment = new BookingAppointment
                {
                    BookingSystemId = system.Id,
                    BookingServiceItemId = service.Id,
                    CustomerFullName = dto.CustomerFullName.Trim(),
                    CustomerMobile = mobile,
                    StartUtc = startUtc,
                    EndUtc = startUtc.AddMinutes(service.DurationMinutes),
                    Status = BookingAppointmentStatuses.Confirmed,
                    CreatedAt = now
                };

                await _context.BookingAppointments.AddAsync(appointment);
                await _context.SaveChangesAsync();

                if (system.SaveToPhonebook && system.Notebooks.Count > 0)
                {
                    var contactId = await SaveCustomerToPhonebooksAsync(system, mobile, dto.CustomerFullName.Trim());
                    if (contactId.HasValue)
                    {
                        appointment.ContactId = contactId;
                        appointment.UpdatedAt = DateTime.UtcNow;
                        await _context.SaveChangesAsync();
                    }
                }

                await transaction.CommitAsync();

                _logger.LogInformation(
                    "Public booking created {AppointmentId} for system {SystemId}",
                    appointment.Id,
                    system.Id);

                var responseDto = MapToDto(appointment);
                responseDto.ServiceTitle = service.Title;

                return ApiResponse<CreatePublicBookingResponseDto>.CreateSuccess(
                    new CreatePublicBookingResponseDto { Appointment = responseDto },
                    "نوبت با موفقیت ثبت شد",
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

        public async Task<ApiResponse<BookingAppointmentListDto>> GetAppointmentsAsync(
            int systemId,
            int userId,
            int pageNumber,
            int pageSize,
            string? status,
            DateTime? fromUtc,
            DateTime? toUtc,
            int? serviceId)
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

                var system = await _systemRepository.GetByIdAndUserIdAsync(systemId, userId);
                if (system == null)
                {
                    return ApiResponse<BookingAppointmentListDto>.NotFound("سیستم رزرو یافت نشد");
                }

                var (items, totalCount) = await _appointmentRepository.GetBySystemIdAsync(
                    systemId, pageNumber, pageSize, status, fromUtc, toUtc, serviceId);

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

                appointment.Status = BookingAppointmentStatuses.Cancelled;
                appointment.CancelledAt = DateTime.UtcNow;
                appointment.CancellationReason = string.IsNullOrWhiteSpace(dto?.Reason) ? null : dto.Reason.Trim();
                appointment.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

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

        public async Task ProcessRemindersAsync(CancellationToken cancellationToken = default)
        {
            var now = DateTime.UtcNow;
            var windowStart = now.AddMinutes(-ReminderWindowMinutes);

            var candidates = await _appointmentRepository.GetPendingRemindersAsync(now, MaxReminderOffsetMinutes);

            foreach (var candidate in candidates)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                var reminderAt = candidate.StartUtc.AddMinutes(-candidate.BookingServiceItem.ReminderOffsetMinutes);
                if (reminderAt > now || reminderAt <= windowStart)
                {
                    continue;
                }

                var tracked = await _context.BookingAppointments
                    .FirstOrDefaultAsync(a =>
                        a.Id == candidate.Id &&
                        a.ReminderSentAt == null &&
                        a.Status == BookingAppointmentStatuses.Confirmed,
                        cancellationToken);

                if (tracked == null)
                {
                    continue;
                }

                var message = BuildReminderMessage(tracked, candidate.BookingSystem, candidate.BookingServiceItem);
                if (!message.TrimEnd().EndsWith("لغو11"))
                {
                    message = $"{message.TrimEnd()}\nلغو11";
                }

                try
                {
                    var smsResult = await _smsService.SendSmsAsync(new SendSmsRequestDto
                    {
                        Mobile = tracked.CustomerMobile,
                        Message = message
                    });

                    var isSuccess = smsResult.Success && smsResult.Data != null &&
                                    (smsResult.Data.Sid > 0 || smsResult.Data.Status > 0);

                    if (!isSuccess)
                    {
                        _logger.LogWarning(
                            "Booking reminder SMS failed for appointment {AppointmentId}",
                            tracked.Id);
                        continue;
                    }

                    await _deliveryTracking.TrackSuccessfulSendAsync(new SmsDeliveryTrackRequestDto
                    {
                        UserId = candidate.BookingSystem.UserId,
                        SourceModule = SmsSourceModules.BookingReminder,
                        SourceEntityId = tracked.Id,
                        SourceEntityLabel = candidate.BookingSystem.Title,
                        Mobile = tracked.CustomerMobile,
                        Sid = smsResult.Data!.Sid
                    });

                    tracked.ReminderSentAt = DateTime.UtcNow;
                    tracked.UpdatedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync(cancellationToken);

                    _logger.LogInformation("Booking reminder sent for appointment {AppointmentId}", tracked.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send booking reminder for appointment {AppointmentId}", tracked.Id);
                }
            }
        }

        private async Task<int?> SaveCustomerToPhonebooksAsync(
            BookingSystem system,
            string mobile,
            string fullName)
        {
            var notebookIds = system.Notebooks.Select(n => n.ContactNotebookId).ToList();
            if (notebookIds.Count == 0)
            {
                return null;
            }

            var existingContacts = await _context.Contacts
                .Where(c =>
                    notebookIds.Contains(c.ContactNotebookId) &&
                    c.MobileNumber == mobile &&
                    !c.IsDeleted)
                .ToListAsync();

            var existingByNotebook = existingContacts.ToDictionary(c => c.ContactNotebookId);
            Contact? savedContact = existingContacts.FirstOrDefault();
            var now = DateTime.UtcNow;

            foreach (var notebookId in notebookIds)
            {
                if (existingByNotebook.TryGetValue(notebookId, out var existing))
                {
                    savedContact ??= existing;
                    if (string.IsNullOrWhiteSpace(existing.FullName) && !string.IsNullOrWhiteSpace(fullName))
                    {
                        existing.FullName = fullName;
                        existing.UpdatedAt = now;
                    }
                }
                else
                {
                    var contact = new Contact
                    {
                        ContactNotebookId = notebookId,
                        MobileNumber = mobile,
                        FullName = fullName,
                        CreatedAt = now
                    };

                    await _context.Contacts.AddAsync(contact);
                    savedContact ??= contact;
                }
            }

            await _context.SaveChangesAsync();
            return savedContact?.Id;
        }

        private static BookingPublicSystemDto MapToPublicDto(BookingSystem system) => new()
        {
            Title = system.Title,
            Description = system.Description,
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
                    DepositAmount = s.DepositAmount
                })
                .ToList()
        };

        private static BookingAppointmentDto MapToDto(BookingAppointment appointment) => new()
        {
            Id = appointment.Id,
            BookingSystemId = appointment.BookingSystemId,
            ServiceId = appointment.BookingServiceItemId,
            ServiceTitle = appointment.BookingServiceItem?.Title ?? string.Empty,
            CustomerFullName = appointment.CustomerFullName,
            CustomerMobile = appointment.CustomerMobile,
            StartUtc = EnsureUtc(appointment.StartUtc),
            EndUtc = EnsureUtc(appointment.EndUtc),
            Status = appointment.Status,
            ReminderSentAt = appointment.ReminderSentAt.HasValue ? EnsureUtc(appointment.ReminderSentAt.Value) : null,
            CancelledAt = appointment.CancelledAt.HasValue ? EnsureUtc(appointment.CancelledAt.Value) : null,
            CancellationReason = appointment.CancellationReason,
            CreatedAt = EnsureUtc(appointment.CreatedAt)
        };

        private static string BuildReminderMessage(
            BookingAppointment appointment,
            BookingSystem system,
            BookingServiceItem service)
        {
            return $"یادآوری نوبت\n" +
                   $"{system.Title}\n" +
                   $"خدمت: {service.Title}\n" +
                   $"زمان: {appointment.StartUtc:yyyy-MM-dd HH:mm} UTC";
        }

        private static DateTime NormalizeUtc(DateTime value) =>
            value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc);

        private static DateTime EnsureUtc(DateTime value) =>
            value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc);
    }
}
