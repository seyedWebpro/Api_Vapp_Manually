using Api_Vapp.Constants;
using Api_Vapp.Data;
using Api_Vapp.DTOs.BookingSystem;
using Api_Vapp.DTOs.Common;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;
using Api_Vapp.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace Api_Vapp.Services
{
    public class BookingSystemService : IBookingSystemService
    {
        private readonly Api_Context _context;
        private readonly IBookingSystemRepository _systemRepository;
        private readonly IBookingSystemDraftRepository _draftRepository;
        private readonly BookingSystemOptions _options;
        private readonly ILogger<BookingSystemService> _logger;

        private const int DraftExpirationHours = 24;
        private const int MaxSlugLength = 100;

        public BookingSystemService(
            Api_Context context,
            IBookingSystemRepository systemRepository,
            IBookingSystemDraftRepository draftRepository,
            IOptions<BookingSystemOptions> options,
            ILogger<BookingSystemService> logger)
        {
            _context = context;
            _systemRepository = systemRepository;
            _draftRepository = draftRepository;
            _options = options.Value;
            _logger = logger;
        }

        public async Task<ApiResponse<BookingSystemListDto>> GetSystemsAsync(int userId, int pageNumber, int pageSize, bool? isActive)
        {
            if (pageNumber < 1) pageNumber = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 10;

            var systems = await _systemRepository.GetByUserIdAsync(userId, pageNumber, pageSize, isActive);
            var totalCount = await _systemRepository.GetCountByUserIdAsync(userId, isActive);
            var activeCount = await _systemRepository.GetActiveCountByUserIdAsync(userId);
            var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);

            var dtos = systems.Select(MapToListDto).ToList();

            return ApiResponse<BookingSystemListDto>.CreateSuccess(new BookingSystemListDto
            {
                Systems = dtos,
                TotalCount = totalCount,
                ActiveCount = activeCount,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalPages = totalPages
            });
        }

        public async Task<ApiResponse<BookingSystemDto>> GetByIdAsync(int id, int userId)
        {
            var system = await _systemRepository.GetByIdWithDetailsAsync(id, userId);
            if (system == null)
            {
                return ApiResponse<BookingSystemDto>.NotFound("سیستم رزرو یافت نشد");
            }

            return ApiResponse<BookingSystemDto>.CreateSuccess(MapToDetailDto(system));
        }

        public async Task<ApiResponse<BookingSystemDto>> ToggleStatusAsync(int id, int userId)
        {
            var system = await _systemRepository.GetByIdAndUserIdAsync(id, userId);
            if (system == null)
            {
                return ApiResponse<BookingSystemDto>.NotFound("سیستم رزرو یافت نشد");
            }

            system.IsActive = !system.IsActive;
            system.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            var statusText = system.IsActive ? "فعال" : "غیرفعال";
            var refreshed = await _systemRepository.GetByIdWithDetailsAsync(id, userId);
            return ApiResponse<BookingSystemDto>.CreateSuccess(
                MapToDetailDto(refreshed!),
                $"سیستم رزرو {statusText} شد");
        }

        public async Task<ApiResponse<bool>> DeleteAsync(int id, int userId)
        {
            var system = await _systemRepository.GetByIdAndUserIdAsync(id, userId);
            if (system == null)
            {
                return ApiResponse<bool>.NotFound("سیستم رزرو یافت نشد");
            }

            system.IsDeleted = true;
            system.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return ApiResponse<bool>.CreateSuccess(true, "سیستم رزرو حذف شد");
        }

        public async Task<ApiResponse<BookingSystemDto>> UpdateAsync(int id, int userId, UpdateBookingSystemDto updateDto)
        {
            if (updateDto == null || !HasAnyUpdateFields(updateDto))
            {
                return ApiResponse<BookingSystemDto>.BadRequest(
                    "هیچ موردی برای به‌روزرسانی ارسال نشده است",
                    errorCode: ErrorCodes.ValidationFailed);
            }

            var system = await _context.BookingSystems
                .Include(b => b.Notebooks)
                .FirstOrDefaultAsync(b => b.Id == id && b.UserId == userId && !b.IsDeleted);

            if (system == null)
            {
                return ApiResponse<BookingSystemDto>.NotFound("سیستم رزرو یافت نشد");
            }

            if (updateDto.Title != null)
            {
                if (string.IsNullOrWhiteSpace(updateDto.Title))
                {
                    return ApiResponse<BookingSystemDto>.BadRequest("عنوان نمی‌تواند خالی باشد");
                }

                var title = updateDto.Title.Trim();
                if (await _systemRepository.ExistsByTitleAsync(userId, title, id))
                {
                    return ApiResponse<BookingSystemDto>.BadRequest("سیستمی با این عنوان قبلاً ثبت شده است");
                }

                system.Title = title;
            }

            if (updateDto.Description != null)
            {
                system.Description = NormalizeOptionalText(updateDto.Description);
            }

            if (updateDto.ActivityType != null)
            {
                if (!BookingActivityTypes.IsValid(updateDto.ActivityType))
                {
                    return ApiResponse<BookingSystemDto>.BadRequest("نوع فعالیت نامعتبر است");
                }

                system.ActivityType = updateDto.ActivityType;
            }

            if (updateDto.Slug != null)
            {
                var slugValidation = await ValidateSlugAsync<BookingSystemDto>(updateDto.Slug, id);
                if (slugValidation.Error != null)
                {
                    return slugValidation.Error;
                }

                system.Slug = slugValidation.NormalizedSlug!;
            }

            if (updateDto.IsActive.HasValue)
            {
                system.IsActive = updateDto.IsActive.Value;
            }

            if (updateDto.SaveToPhonebook.HasValue)
            {
                system.SaveToPhonebook = updateDto.SaveToPhonebook.Value;
                if (!updateDto.SaveToPhonebook.Value && updateDto.NotebookIds == null)
                {
                    system.Notebooks.Clear();
                }
            }

            if (updateDto.NotebookIds != null)
            {
                var notebookErrors = await ValidateNotebookIdsAsync(userId, updateDto.NotebookIds);
                if (notebookErrors.Count > 0)
                {
                    return ApiResponse<BookingSystemDto>.BadRequest("دفترچه‌های انتخاب‌شده نامعتبر است", notebookErrors);
                }

                system.Notebooks.Clear();
                foreach (var notebookId in updateDto.NotebookIds.Distinct())
                {
                    system.Notebooks.Add(new BookingSystemNotebook { ContactNotebookId = notebookId });
                }
            }

            if (system.SaveToPhonebook && !system.Notebooks.Any())
            {
                return ApiResponse<BookingSystemDto>.BadRequest("حداقل یک دفترچه باید انتخاب شود");
            }

            system.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            var refreshed = await _systemRepository.GetByIdWithDetailsAsync(id, userId);
            return ApiResponse<BookingSystemDto>.CreateSuccess(MapToDetailDto(refreshed!), "سیستم رزرو به‌روزرسانی شد");
        }

        public async Task<ApiResponse<List<BookingNotebookDto>>> GetNotebooksAsync(int userId)
        {
            var notebooks = await _context.ContactNotebooks
                .Where(cn => cn.UserId == userId && !cn.IsDeleted)
                .OrderBy(cn => cn.Name)
                .Select(cn => new BookingNotebookDto
                {
                    Id = cn.Id,
                    Name = cn.Name,
                    MembersCount = cn.Contacts.Count(c => !c.IsDeleted)
                })
                .ToListAsync();

            return ApiResponse<List<BookingNotebookDto>>.CreateSuccess(notebooks);
        }

        public Task<ApiResponse<List<BookingActivityTypeDto>>> GetActivityTypesAsync()
        {
            var types = BookingActivityTypes.Catalog
                .Select(c => new BookingActivityTypeDto { Code = c.Code, Title = c.Title })
                .ToList();

            return Task.FromResult(ApiResponse<List<BookingActivityTypeDto>>.CreateSuccess(types));
        }

        public async Task<ApiResponse<BookingStep1ValidationResponseDto>> ValidateStep1Async(int userId, BookingStep1Dto step1Dto)
        {
            var errors = ValidateStep1Fields(step1Dto);
            if (errors.Count > 0)
            {
                return ApiResponse<BookingStep1ValidationResponseDto>.BadRequest("خطا در اعتبارسنجی", errors);
            }

            var notebookErrors = await ValidateNotebookIdsAsync(userId, step1Dto.NotebookIds);
            if (step1Dto.SaveToPhonebook && notebookErrors.Count > 0)
            {
                return ApiResponse<BookingStep1ValidationResponseDto>.BadRequest("دفترچه‌های انتخاب‌شده نامعتبر است", notebookErrors);
            }

            if (await _systemRepository.ExistsByTitleAsync(userId, step1Dto.Title.Trim()))
            {
                return ApiResponse<BookingStep1ValidationResponseDto>.BadRequest("سیستمی با این عنوان قبلاً ثبت شده است");
            }

            if (!string.IsNullOrWhiteSpace(step1Dto.CustomSlug))
            {
                var slugValidation = await ValidateSlugAsync<BookingStep1ValidationResponseDto>(step1Dto.CustomSlug);
                if (slugValidation.Error != null)
                {
                    return slugValidation.Error;
                }
            }

            var draftId = $"{userId}_{Guid.NewGuid()}";
            var expiresAt = DateTime.UtcNow.AddHours(DraftExpirationHours);
            var draft = new BookingSystemDraft
            {
                UserId = userId,
                DraftId = draftId,
                Step1Data = JsonSerializer.Serialize(step1Dto),
                ExpiresAt = expiresAt,
                CreatedAt = DateTime.UtcNow
            };

            await _draftRepository.AddAsync(draft);

            return ApiResponse<BookingStep1ValidationResponseDto>.CreateSuccess(
                new BookingStep1ValidationResponseDto
                {
                    IsValid = true,
                    DraftId = draftId,
                    DraftExpiresAt = EnsureUtc(expiresAt)
                },
                "اطلاعات مرحله 1 معتبر است");
        }

        public async Task<ApiResponse<BookingStep2ValidationResponseDto>> ValidateStep2Async(int userId, BookingStep2Dto step2Dto)
        {
            var loadResult = await LoadDraftStep1Async(userId, step2Dto.DraftId);
            if (!loadResult.Success)
            {
                return ApiResponse<BookingStep2ValidationResponseDto>.BadRequest(loadResult.ErrorMessage!);
            }

            var errors = ValidateStep2Fields(step2Dto);
            if (errors.Count > 0)
            {
                return ApiResponse<BookingStep2ValidationResponseDto>.BadRequest("خطا در اعتبارسنجی", errors);
            }

            var draft = loadResult.Draft!;
            draft.Step2Data = JsonSerializer.Serialize(step2Dto);
            draft.ExpiresAt = DateTime.UtcNow.AddHours(DraftExpirationHours);
            await _draftRepository.UpdateAsync(draft);

            return ApiResponse<BookingStep2ValidationResponseDto>.CreateSuccess(
                new BookingStep2ValidationResponseDto
                {
                    IsValid = true,
                    ServicesCount = step2Dto.Services.Count
                },
                "اطلاعات مرحله 2 معتبر است");
        }

        public async Task<ApiResponse<BookingStep3ValidationResponseDto>> ValidateStep3Async(int userId, BookingStep3Dto step3Dto)
        {
            var loadResult = await LoadDraftSteps12Async(userId, step3Dto.DraftId);
            if (!loadResult.Success)
            {
                return ApiResponse<BookingStep3ValidationResponseDto>.BadRequest(loadResult.ErrorMessage!);
            }

            var errors = ValidateStep3Fields(loadResult.Step2!.Services, step3Dto);
            if (errors.Count > 0)
            {
                return ApiResponse<BookingStep3ValidationResponseDto>.BadRequest("خطا در اعتبارسنجی", errors);
            }

            var draft = loadResult.Draft!;
            draft.Step3Data = JsonSerializer.Serialize(step3Dto);
            draft.ExpiresAt = DateTime.UtcNow.AddHours(DraftExpirationHours);
            await _draftRepository.UpdateAsync(draft);

            return ApiResponse<BookingStep3ValidationResponseDto>.CreateSuccess(
                new BookingStep3ValidationResponseDto { IsValid = true },
                "اطلاعات مرحله 3 معتبر است");
        }

        public async Task<ApiResponse<BookingStep4ValidationResponseDto>> ValidateStep4Async(int userId, BookingStep4Dto step4Dto)
        {
            var loadResult = await LoadDraftSteps123Async(userId, step4Dto.DraftId);
            if (!loadResult.Success)
            {
                return ApiResponse<BookingStep4ValidationResponseDto>.BadRequest(loadResult.ErrorMessage!);
            }

            var errors = ValidateStep4Fields(loadResult.Step2!.Services, step4Dto);
            if (errors.Count > 0)
            {
                return ApiResponse<BookingStep4ValidationResponseDto>.BadRequest("خطا در اعتبارسنجی", errors);
            }

            var draft = loadResult.Draft!;
            draft.Step4Data = JsonSerializer.Serialize(step4Dto);
            draft.ExpiresAt = DateTime.UtcNow.AddHours(DraftExpirationHours);
            await _draftRepository.UpdateAsync(draft);

            return ApiResponse<BookingStep4ValidationResponseDto>.CreateSuccess(
                new BookingStep4ValidationResponseDto { IsValid = true },
                "اطلاعات مرحله 4 معتبر است");
        }

        public async Task<ApiResponse<BookingSummaryDto>> GetSummaryAsync(int userId, string draftId)
        {
            var loadResult = await LoadAllDraftStepsAsync(userId, draftId);
            if (!loadResult.Success)
            {
                return ApiResponse<BookingSummaryDto>.BadRequest(loadResult.ErrorMessage!);
            }

            var slug = await ResolveSlugAsync(loadResult.Step1!, null);
            var summary = new BookingSummaryDto
            {
                Step1 = loadResult.Step1!,
                Services = loadResult.Step2!.Services,
                ServiceSchedules = loadResult.Step3!.ServiceSchedules,
                ServiceSettings = loadResult.Step4!.ServiceSettings,
                ResolvedSlug = slug,
                PublicUrlPreview = BuildPublicUrl(slug)
            };

            return ApiResponse<BookingSummaryDto>.CreateSuccess(summary);
        }

        public async Task<ApiResponse<ConfirmBookingSystemResponseDto>> ConfirmAsync(int userId, ConfirmBookingSystemDto request)
        {
            var loadResult = await LoadAllDraftStepsAsync(userId, request.DraftId);
            if (!loadResult.Success)
            {
                return ApiResponse<ConfirmBookingSystemResponseDto>.BadRequest(loadResult.ErrorMessage!);
            }

            var step1 = loadResult.Step1!;
            var step2 = loadResult.Step2!;
            var step3 = loadResult.Step3!;
            var step4 = loadResult.Step4!;

            var step1Errors = ValidateStep1Fields(step1);
            var step2Errors = ValidateStep2Fields(step2);
            var step3Errors = ValidateStep3Fields(step2.Services, step3);
            var step4Errors = ValidateStep4Fields(step2.Services, step4);
            var allErrors = step1Errors
                .Concat(step2Errors)
                .Concat(step3Errors)
                .Concat(step4Errors)
                .ToList();

            if (allErrors.Count > 0)
            {
                return ApiResponse<ConfirmBookingSystemResponseDto>.BadRequest("داده‌های پیش‌نویس نامعتبر است", allErrors);
            }

            if (await _systemRepository.ExistsByTitleAsync(userId, step1.Title.Trim()))
            {
                return ApiResponse<ConfirmBookingSystemResponseDto>.BadRequest("سیستمی با این عنوان قبلاً ثبت شده است");
            }

            var slug = await ResolveSlugAsync(step1, null);
            if (string.IsNullOrEmpty(slug))
            {
                return ApiResponse<ConfirmBookingSystemResponseDto>.BadRequest("امکان ساخت لینک وجود ندارد");
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var now = DateTime.UtcNow;
                var system = new BookingSystem
                {
                    UserId = userId,
                    Title = step1.Title.Trim(),
                    ActivityType = step1.ActivityType,
                    Description = NormalizeOptionalText(step1.Description),
                    Slug = slug,
                    Status = BookingSystemStatus.Published,
                    SaveToPhonebook = step1.SaveToPhonebook,
                    IsActive = true,
                    PublishedAt = now,
                    CreatedAt = now
                };

                if (step1.SaveToPhonebook)
                {
                    foreach (var notebookId in step1.NotebookIds.Distinct())
                    {
                        system.Notebooks.Add(new BookingSystemNotebook { ContactNotebookId = notebookId });
                    }
                }

                var settingsLookup = step4.ServiceSettings.ToDictionary(s => s.ServiceTempId);
                var scheduleLookup = step3.ServiceSchedules.ToDictionary(s => s.ServiceTempId);

                for (var i = 0; i < step2.Services.Count; i++)
                {
                    var serviceDraft = step2.Services[i];
                    if (!settingsLookup.TryGetValue(serviceDraft.ServiceTempId, out var settings))
                    {
                        await transaction.RollbackAsync();
                        return ApiResponse<ConfirmBookingSystemResponseDto>.BadRequest("تنظیمات یادآوری برای همه خدمات الزامی است");
                    }

                    if (!scheduleLookup.TryGetValue(serviceDraft.ServiceTempId, out var schedule))
                    {
                        await transaction.RollbackAsync();
                        return ApiResponse<ConfirmBookingSystemResponseDto>.BadRequest("برنامه هفتگی برای همه خدمات الزامی است");
                    }

                    var service = new BookingServiceItem
                    {
                        Title = serviceDraft.Title.Trim(),
                        DurationMinutes = serviceDraft.DurationMinutes,
                        HasCost = serviceDraft.HasCost,
                        Price = serviceDraft.HasCost ? serviceDraft.Price : null,
                        ServiceCost = serviceDraft.HasCost ? serviceDraft.ServiceCost : null,
                        DepositAmount = serviceDraft.DepositAmount,
                        BufferMinutesBetweenAppointments = settings.BufferMinutesBetweenAppointments,
                        MaxDailyReservations = settings.MaxDailyReservations,
                        ReminderOffsetMinutes = settings.ReminderOffsetMinutes,
                        SortOrder = i,
                        CreatedAt = now
                    };

                    foreach (var day in schedule.WeeklyDays)
                    {
                        service.DaySchedules.Add(new BookingServiceDaySchedule
                        {
                            DayOfWeek = day.DayOfWeek,
                            IsOpen = day.IsOpen,
                            StartTimeUtc = day.IsOpen ? day.StartTimeUtc : null,
                            EndTimeUtc = day.IsOpen ? day.EndTimeUtc : null
                        });
                    }

                    foreach (var exception in schedule.Exceptions)
                    {
                        service.ScheduleExceptions.Add(new BookingScheduleException
                        {
                            ExceptionDateUtc = exception.ExceptionDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
                            Type = exception.Type,
                            Label = NormalizeOptionalText(exception.Label),
                            CreatedAt = now
                        });
                    }

                    system.Services.Add(service);
                }

                await _context.BookingSystems.AddAsync(system);
                await _context.SaveChangesAsync();
                await _draftRepository.DeleteAsync(request.DraftId, userId);
                await transaction.CommitAsync();

                _logger.LogInformation("Booking system {SystemId} created with slug {Slug} for user {UserId}",
                    system.Id, system.Slug, userId);

                var created = await _systemRepository.GetByIdWithDetailsAsync(system.Id, userId);
                return ApiResponse<ConfirmBookingSystemResponseDto>.CreateSuccess(
                    new ConfirmBookingSystemResponseDto { System = MapToDetailDto(created!) },
                    "سیستم رزرو با موفقیت ایجاد شد",
                    201);
            }
            catch (DbUpdateException dbEx)
            {
                await transaction.RollbackAsync();
                return BookingDbExceptionHelper.MapDbUpdateException<ConfirmBookingSystemResponseDto>(
                    dbEx, _logger, "confirming booking system draft", userId: userId);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error confirming booking system draft {DraftId} for user {UserId}", request.DraftId, userId);
                return ApiResponse<ConfirmBookingSystemResponseDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<List<BookingServiceItemDto>>> GetServicesAsync(int systemId, int userId)
        {
            var system = await GetOwnedSystemWithServicesAsync(systemId, userId);
            if (system == null)
            {
                return ApiResponse<List<BookingServiceItemDto>>.NotFound("سیستم رزرو یافت نشد");
            }

            var services = system.Services
                .Where(s => !s.IsDeleted)
                .OrderBy(s => s.SortOrder)
                .Select(MapServiceToDto)
                .ToList();

            return ApiResponse<List<BookingServiceItemDto>>.CreateSuccess(services);
        }

        public async Task<ApiResponse<BookingServiceItemDto>> AddServiceAsync(int systemId, int userId, AddBookingServiceDto dto)
        {
            var system = await GetOwnedSystemWithServicesTrackedAsync(systemId, userId);
            if (system == null)
            {
                return ApiResponse<BookingServiceItemDto>.NotFound("سیستم رزرو یافت نشد");
            }

            var errors = ValidateServiceDraftFields(dto.Title, dto.DurationMinutes, dto.HasCost, dto.Price, dto.ServiceCost);
            var scheduleErrors = ValidateWeeklyDays(dto.WeeklyDays);
            errors.AddRange(scheduleErrors);

            if (errors.Count > 0)
            {
                return ApiResponse<BookingServiceItemDto>.BadRequest("خطا در اعتبارسنجی", errors);
            }

            var now = DateTime.UtcNow;
            var maxOrder = system.Services.Where(s => !s.IsDeleted).Select(s => (int?)s.SortOrder).Max() ?? -1;
            var service = new BookingServiceItem
            {
                Title = dto.Title.Trim(),
                DurationMinutes = dto.DurationMinutes,
                HasCost = dto.HasCost,
                Price = dto.HasCost ? dto.Price : null,
                ServiceCost = dto.HasCost ? dto.ServiceCost : null,
                DepositAmount = dto.DepositAmount,
                BufferMinutesBetweenAppointments = dto.BufferMinutesBetweenAppointments,
                MaxDailyReservations = dto.MaxDailyReservations,
                ReminderOffsetMinutes = dto.ReminderOffsetMinutes,
                SortOrder = maxOrder + 1,
                CreatedAt = now
            };

            ApplyWeeklyDays(service, dto.WeeklyDays);
            ApplyExceptions(service, dto.Exceptions, now);
            system.Services.Add(service);
            system.UpdatedAt = now;
            await _context.SaveChangesAsync();

            var refreshed = await GetServiceByIdAsync(systemId, service.Id, userId);
            return ApiResponse<BookingServiceItemDto>.CreateSuccess(
                MapServiceToDto(refreshed!),
                "خدمت با موفقیت افزوده شد",
                201);
        }

        public async Task<ApiResponse<BookingServiceItemDto>> UpdateServiceAsync(
            int systemId, int serviceId, int userId, UpdateBookingServiceDto dto)
        {
            if (dto == null || !HasAnyServiceUpdateFields(dto))
            {
                return ApiResponse<BookingServiceItemDto>.BadRequest("هیچ موردی برای به‌روزرسانی ارسال نشده است");
            }

            var service = await GetServiceByIdTrackedAsync(systemId, serviceId, userId);
            if (service == null)
            {
                return ApiResponse<BookingServiceItemDto>.NotFound("خدمت یافت نشد");
            }

            if (dto.Title != null)
            {
                if (string.IsNullOrWhiteSpace(dto.Title))
                {
                    return ApiResponse<BookingServiceItemDto>.BadRequest("عنوان خدمت نمی‌تواند خالی باشد");
                }

                service.Title = dto.Title.Trim();
            }

            if (dto.DurationMinutes.HasValue)
            {
                if (dto.DurationMinutes.Value < 1)
                {
                    return ApiResponse<BookingServiceItemDto>.BadRequest("مدت زمان باید حداقل 1 دقیقه باشد");
                }

                service.DurationMinutes = dto.DurationMinutes.Value;
            }

            if (dto.HasCost.HasValue)
            {
                service.HasCost = dto.HasCost.Value;
                if (!dto.HasCost.Value)
                {
                    service.Price = null;
                    service.ServiceCost = null;
                }
            }

            if (dto.Price.HasValue) service.Price = dto.Price;
            if (dto.ServiceCost.HasValue) service.ServiceCost = dto.ServiceCost;
            if (dto.DepositAmount.HasValue) service.DepositAmount = dto.DepositAmount;
            if (dto.BufferMinutesBetweenAppointments.HasValue)
            {
                service.BufferMinutesBetweenAppointments = dto.BufferMinutesBetweenAppointments.Value;
            }

            if (dto.MaxDailyReservations.HasValue)
            {
                service.MaxDailyReservations = dto.MaxDailyReservations;
            }

            if (dto.ReminderOffsetMinutes.HasValue)
            {
                service.ReminderOffsetMinutes = dto.ReminderOffsetMinutes.Value;
            }

            service.UpdatedAt = DateTime.UtcNow;
            service.BookingSystem.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            var refreshed = await GetServiceByIdAsync(systemId, serviceId, userId);
            return ApiResponse<BookingServiceItemDto>.CreateSuccess(MapServiceToDto(refreshed!), "خدمت به‌روزرسانی شد");
        }

        public async Task<ApiResponse<bool>> DeleteServiceAsync(int systemId, int serviceId, int userId)
        {
            var service = await GetServiceByIdTrackedAsync(systemId, serviceId, userId);
            if (service == null)
            {
                return ApiResponse<bool>.NotFound("خدمت یافت نشد");
            }

            service.IsDeleted = true;
            service.UpdatedAt = DateTime.UtcNow;
            service.BookingSystem.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return ApiResponse<bool>.CreateSuccess(true, "خدمت حذف شد");
        }

        public async Task<ApiResponse<BookingServiceItemDto>> GetServiceScheduleAsync(int systemId, int serviceId, int userId)
        {
            var service = await GetServiceByIdAsync(systemId, serviceId, userId);
            if (service == null)
            {
                return ApiResponse<BookingServiceItemDto>.NotFound("خدمت یافت نشد");
            }

            return ApiResponse<BookingServiceItemDto>.CreateSuccess(MapServiceToDto(service));
        }

        public async Task<ApiResponse<BookingServiceItemDto>> SaveServiceScheduleAsync(
            int systemId, int serviceId, int userId, SaveBookingServiceScheduleDto dto)
        {
            var service = await GetServiceByIdTrackedAsync(systemId, serviceId, userId);
            if (service == null)
            {
                return ApiResponse<BookingServiceItemDto>.NotFound("خدمت یافت نشد");
            }

            var errors = ValidateWeeklyDays(dto.WeeklyDays);
            if (errors.Count > 0)
            {
                return ApiResponse<BookingServiceItemDto>.BadRequest("خطا در اعتبارسنجی", errors);
            }

            _context.BookingServiceDaySchedules.RemoveRange(service.DaySchedules);
            service.DaySchedules.Clear();
            ApplyWeeklyDays(service, dto.WeeklyDays);

            service.UpdatedAt = DateTime.UtcNow;
            service.BookingSystem.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            var refreshed = await GetServiceByIdAsync(systemId, serviceId, userId);
            return ApiResponse<BookingServiceItemDto>.CreateSuccess(MapServiceToDto(refreshed!), "برنامه هفتگی ذخیره شد");
        }

        public async Task<ApiResponse<BookingScheduleExceptionDto>> AddScheduleExceptionAsync(
            int systemId, int serviceId, int userId, AddBookingScheduleExceptionDto dto)
        {
            var service = await GetServiceByIdTrackedAsync(systemId, serviceId, userId);
            if (service == null)
            {
                return ApiResponse<BookingScheduleExceptionDto>.NotFound("خدمت یافت نشد");
            }

            if (!IsValidExceptionType(dto.Type))
            {
                return ApiResponse<BookingScheduleExceptionDto>.BadRequest("نوع استثنا نامعتبر است");
            }

            var now = DateTime.UtcNow;
            var exception = new BookingScheduleException
            {
                ExceptionDateUtc = dto.ExceptionDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
                Type = dto.Type,
                Label = NormalizeOptionalText(dto.Label),
                CreatedAt = now
            };

            service.ScheduleExceptions.Add(exception);
            service.UpdatedAt = now;
            service.BookingSystem.UpdatedAt = now;
            await _context.SaveChangesAsync();

            return ApiResponse<BookingScheduleExceptionDto>.CreateSuccess(
                MapExceptionToDto(exception),
                "روز استثنا افزوده شد",
                201);
        }

        public async Task<ApiResponse<bool>> DeleteScheduleExceptionAsync(
            int systemId, int serviceId, int exceptionId, int userId)
        {
            var service = await GetServiceByIdTrackedAsync(systemId, serviceId, userId);
            if (service == null)
            {
                return ApiResponse<bool>.NotFound("خدمت یافت نشد");
            }

            var exception = service.ScheduleExceptions.FirstOrDefault(e => e.Id == exceptionId && !e.IsDeleted);
            if (exception == null)
            {
                return ApiResponse<bool>.NotFound("روز استثنا یافت نشد");
            }

            exception.IsDeleted = true;
            service.UpdatedAt = DateTime.UtcNow;
            service.BookingSystem.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return ApiResponse<bool>.CreateSuccess(true, "روز استثنا حذف شد");
        }

        // ─── Private helpers ─────────────────────────────────────────────

        private static bool HasAnyUpdateFields(UpdateBookingSystemDto dto) =>
            dto.Title != null ||
            dto.Description != null ||
            dto.ActivityType != null ||
            dto.SaveToPhonebook.HasValue ||
            dto.NotebookIds != null ||
            dto.IsActive.HasValue ||
            dto.Slug != null;

        private static bool HasAnyServiceUpdateFields(UpdateBookingServiceDto dto) =>
            dto.Title != null ||
            dto.DurationMinutes.HasValue ||
            dto.HasCost.HasValue ||
            dto.Price.HasValue ||
            dto.ServiceCost.HasValue ||
            dto.DepositAmount.HasValue ||
            dto.BufferMinutesBetweenAppointments.HasValue ||
            dto.MaxDailyReservations.HasValue ||
            dto.ReminderOffsetMinutes.HasValue;

        private BookingSystemDto MapToListDto(BookingSystem system) => new()
        {
            Id = system.Id,
            Title = system.Title,
            ActivityType = system.ActivityType,
            ActivityTypeTitle = BookingActivityTypes.GetTitle(system.ActivityType),
            Description = system.Description,
            Slug = system.Slug,
            PublicUrl = BuildPublicUrl(system.Slug),
            Status = system.Status.ToString(),
            SaveToPhonebook = system.SaveToPhonebook,
            IsActive = system.IsActive,
            CreatedAt = EnsureUtc(system.CreatedAt),
            UpdatedAt = system.UpdatedAt.HasValue ? EnsureUtc(system.UpdatedAt.Value) : null,
            PublishedAt = system.PublishedAt.HasValue ? EnsureUtc(system.PublishedAt.Value) : null
        };

        private BookingSystemDto MapToDetailDto(BookingSystem system)
        {
            var dto = MapToListDto(system);
            dto.NotebookIds = system.Notebooks.Select(n => n.ContactNotebookId).ToList();
            dto.Services = system.Services
                .Where(s => !s.IsDeleted)
                .OrderBy(s => s.SortOrder)
                .Select(MapServiceToDto)
                .ToList();
            return dto;
        }

        private static BookingServiceItemDto MapServiceToDto(BookingServiceItem service) => new()
        {
            Id = service.Id,
            Title = service.Title,
            DurationMinutes = service.DurationMinutes,
            HasCost = service.HasCost,
            Price = service.Price,
            ServiceCost = service.ServiceCost,
            DepositAmount = service.DepositAmount,
            BufferMinutesBetweenAppointments = service.BufferMinutesBetweenAppointments,
            MaxDailyReservations = service.MaxDailyReservations,
            ReminderOffsetMinutes = service.ReminderOffsetMinutes,
            SortOrder = service.SortOrder,
            WeeklyDays = service.DaySchedules
                .OrderBy(d => d.DayOfWeek)
                .Select(d => new BookingDayScheduleDto
                {
                    DayOfWeek = d.DayOfWeek,
                    IsOpen = d.IsOpen,
                    StartTimeUtc = d.StartTimeUtc,
                    EndTimeUtc = d.EndTimeUtc
                })
                .ToList(),
            Exceptions = service.ScheduleExceptions
                .Where(e => !e.IsDeleted)
                .OrderBy(e => e.ExceptionDateUtc)
                .Select(MapExceptionToDto)
                .ToList()
        };

        private static BookingScheduleExceptionDto MapExceptionToDto(BookingScheduleException exception) => new()
        {
            Id = exception.Id,
            ExceptionDate = DateOnly.FromDateTime(exception.ExceptionDateUtc),
            Type = exception.Type,
            Label = exception.Label
        };

        private string BuildPublicUrl(string slug)
        {
            var baseUrl = string.IsNullOrWhiteSpace(_options.PublicBaseUrl)
                ? "https://app.com/book"
                : _options.PublicBaseUrl.TrimEnd('/');

            return $"{baseUrl}/{slug}";
        }

        private static DateTime EnsureUtc(DateTime value) =>
            value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc);

        private static string? NormalizeOptionalText(string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        private async Task<(ApiResponse<T>? Error, string? NormalizedSlug)> ValidateSlugAsync<T>(string slug, int? excludeId = null)
        {
            var normalized = UserFormSlugHelper.Normalize(slug);
            if (normalized == null)
            {
                return (ApiResponse<T>.BadRequest(
                    "فرمت لینک نامعتبر است. فقط حروف انگلیسی کوچک، اعداد و خط تیره مجاز است",
                    errorCode: ErrorCodes.ValidationFailed), null);
            }

            if (await _systemRepository.ExistsBySlugAsync(normalized, excludeId))
            {
                return (ApiResponse<T>.BadRequest("این لینک قبلاً استفاده شده است", errorCode: ErrorCodes.ValidationFailed), null);
            }

            return (null, normalized);
        }

        private async Task<string> ResolveSlugAsync(BookingStep1Dto step1, int? excludeId)
        {
            if (!string.IsNullOrWhiteSpace(step1.CustomSlug))
            {
                var validation = await ValidateSlugAsync<BookingSystemDto>(step1.CustomSlug, excludeId);
                if (validation.Error == null && validation.NormalizedSlug != null)
                {
                    return validation.NormalizedSlug;
                }
            }

            var baseSlug = UserFormSlugHelper.SlugifyTitle(step1.Title);
            for (var suffix = 0; suffix < 100; suffix++)
            {
                var candidate = UserFormSlugHelper.BuildCandidateSlug(baseSlug, suffix);
                if (candidate.Length > MaxSlugLength)
                {
                    candidate = candidate[..MaxSlugLength].Trim('-');
                }

                if (!await _systemRepository.ExistsBySlugAsync(candidate, excludeId))
                {
                    return candidate;
                }
            }

            return $"{baseSlug}-{Guid.NewGuid():N}"[..MaxSlugLength].Trim('-');
        }

        private async Task<List<string>> ValidateNotebookIdsAsync(int userId, List<int> notebookIds)
        {
            var errors = new List<string>();
            if (notebookIds == null || notebookIds.Count == 0)
            {
                errors.Add("حداقل یک دفترچه باید انتخاب شود");
                return errors;
            }

            var distinctIds = notebookIds.Distinct().ToList();
            var validCount = await _context.ContactNotebooks
                .CountAsync(cn => distinctIds.Contains(cn.Id) && cn.UserId == userId && !cn.IsDeleted);

            if (validCount != distinctIds.Count)
            {
                errors.Add("برخی دفترچه‌های انتخاب‌شده نامعتبر هستند");
            }

            return errors;
        }

        private static List<string> ValidateStep1Fields(BookingStep1Dto step1)
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(step1.Title))
            {
                errors.Add("عنوان کسب‌وکار الزامی است");
            }

            if (!BookingActivityTypes.IsValid(step1.ActivityType))
            {
                errors.Add("نوع فعالیت نامعتبر است");
            }

            if (step1.SaveToPhonebook && (step1.NotebookIds == null || step1.NotebookIds.Count == 0))
            {
                errors.Add("برای ذخیره در دفترچه، حداقل یک دفترچه باید انتخاب شود");
            }

            return errors;
        }

        private static List<string> ValidateStep2Fields(BookingStep2Dto step2)
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(step2.DraftId))
            {
                errors.Add("شناسه پیش‌نویس الزامی است");
            }

            if (step2.Services == null || step2.Services.Count == 0)
            {
                errors.Add("حداقل یک خدمت باید اضافه شود");
                return errors;
            }

            var tempIds = new HashSet<string>();
            for (var i = 0; i < step2.Services.Count; i++)
            {
                var service = step2.Services[i];
                var prefix = $"خدمت {i + 1}: ";

                if (string.IsNullOrWhiteSpace(service.ServiceTempId))
                {
                    errors.Add(prefix + "شناسه موقت خدمت الزامی است");
                }
                else if (!tempIds.Add(service.ServiceTempId))
                {
                    errors.Add(prefix + "شناسه موقت تکراری است");
                }

                errors.AddRange(ValidateServiceDraftFields(service.Title, service.DurationMinutes, service.HasCost, service.Price, service.ServiceCost)
                    .Select(e => prefix + e));
            }

            return errors;
        }

        private static List<string> ValidateStep3Fields(
            List<BookingServiceDraftDto> services,
            BookingStep3Dto step3)
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(step3.DraftId))
            {
                errors.Add("شناسه پیش‌نویس الزامی است");
            }

            var serviceIds = services.Select(s => s.ServiceTempId).ToHashSet();
            var providedIds = step3.ServiceSchedules.Select(s => s.ServiceTempId).ToHashSet();

            foreach (var id in serviceIds)
            {
                if (!providedIds.Contains(id))
                {
                    var title = services.First(s => s.ServiceTempId == id).Title;
                    errors.Add($"برنامه هفتگی برای خدمت «{title}» الزامی است");
                }
            }

            foreach (var extraId in providedIds.Except(serviceIds))
            {
                errors.Add($"برنامه هفتگی برای خدمت ناشناخته ({extraId}) ارسال شده است");
            }

            foreach (var schedule in step3.ServiceSchedules)
            {
                var title = services.FirstOrDefault(s => s.ServiceTempId == schedule.ServiceTempId)?.Title ?? schedule.ServiceTempId;
                var prefix = $"خدمت «{title}»: ";

                errors.AddRange(ValidateWeeklyDays(schedule.WeeklyDays).Select(e => prefix + e));

                foreach (var exception in schedule.Exceptions)
                {
                    if (!IsValidExceptionType(exception.Type))
                    {
                        errors.Add(prefix + "نوع استثنا نامعتبر است");
                    }
                }
            }

            return errors;
        }

        private static List<string> ValidateStep4Fields(
            List<BookingServiceDraftDto> services,
            BookingStep4Dto step4)
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(step4.DraftId))
            {
                errors.Add("شناسه پیش‌نویس الزامی است");
            }

            var serviceIds = services.Select(s => s.ServiceTempId).ToHashSet();
            var providedIds = step4.ServiceSettings.Select(s => s.ServiceTempId).ToHashSet();

            foreach (var id in serviceIds)
            {
                if (!providedIds.Contains(id))
                {
                    var title = services.First(s => s.ServiceTempId == id).Title;
                    errors.Add($"تنظیمات یادآوری برای خدمت «{title}» الزامی است");
                }
            }

            foreach (var extraId in providedIds.Except(serviceIds))
            {
                errors.Add($"تنظیمات یادآوری برای خدمت ناشناخته ({extraId}) ارسال شده است");
            }

            foreach (var settings in step4.ServiceSettings)
            {
                if (settings.ReminderOffsetMinutes < 1)
                {
                    errors.Add("زمان یادآوری باید حداقل 1 دقیقه باشد");
                }

                if (settings.BufferMinutesBetweenAppointments < 0)
                {
                    errors.Add("فاصله بین نوبت‌ها نمی‌تواند منفی باشد");
                }
            }

            return errors;
        }

        private static List<string> ValidateServiceDraftFields(
            string title,
            int durationMinutes,
            bool hasCost,
            decimal? price,
            decimal? serviceCost)
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(title))
            {
                errors.Add("عنوان خدمت الزامی است");
            }

            if (durationMinutes < 1)
            {
                errors.Add("مدت زمان باید حداقل 1 دقیقه باشد");
            }

            if (hasCost && (!price.HasValue || price.Value < 0))
            {
                errors.Add("قیمت خدمت الزامی است");
            }

            return errors;
        }

        private static List<string> ValidateWeeklyDays(List<BookingDayScheduleDto> weeklyDays)
        {
            var errors = new List<string>();

            if (weeklyDays == null || weeklyDays.Count == 0)
            {
                errors.Add("برنامه هفتگی الزامی است");
                return errors;
            }

            if (!weeklyDays.Any(d => d.IsOpen))
            {
                errors.Add("حداقل یک روز کاری باید فعال باشد");
            }

            foreach (var day in weeklyDays.Where(d => d.IsOpen))
            {
                if (!day.StartTimeUtc.HasValue || !day.EndTimeUtc.HasValue)
                {
                    errors.Add($"ساعت شروع و پایان برای {day.DayOfWeek} الزامی است");
                    continue;
                }

                if (day.EndTimeUtc <= day.StartTimeUtc)
                {
                    errors.Add($"ساعت پایان باید بعد از ساعت شروع برای {day.DayOfWeek} باشد");
                }
            }

            return errors;
        }

        private static bool IsValidExceptionType(string type) =>
            type == BookingScheduleExceptionTypes.Holiday ||
            type == BookingScheduleExceptionTypes.Leave;

        private static void ApplyWeeklyDays(BookingServiceItem service, List<BookingDayScheduleDto> weeklyDays)
        {
            foreach (var day in weeklyDays)
            {
                service.DaySchedules.Add(new BookingServiceDaySchedule
                {
                    DayOfWeek = day.DayOfWeek,
                    IsOpen = day.IsOpen,
                    StartTimeUtc = day.IsOpen ? day.StartTimeUtc : null,
                    EndTimeUtc = day.IsOpen ? day.EndTimeUtc : null
                });
            }
        }

        private static void ApplyExceptions(
            BookingServiceItem service,
            List<BookingScheduleExceptionDto> exceptions,
            DateTime now)
        {
            foreach (var exception in exceptions)
            {
                if (!IsValidExceptionType(exception.Type))
                {
                    continue;
                }

                service.ScheduleExceptions.Add(new BookingScheduleException
                {
                    ExceptionDateUtc = exception.ExceptionDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
                    Type = exception.Type,
                    Label = NormalizeOptionalText(exception.Label),
                    CreatedAt = now
                });
            }
        }

        private async Task<BookingSystem?> GetOwnedSystemWithServicesAsync(int systemId, int userId)
        {
            return await _context.BookingSystems
                .Include(b => b.Services.Where(s => !s.IsDeleted))
                    .ThenInclude(s => s.DaySchedules)
                .Include(b => b.Services.Where(s => !s.IsDeleted))
                    .ThenInclude(s => s.ScheduleExceptions.Where(e => !e.IsDeleted))
                .AsNoTracking()
                .FirstOrDefaultAsync(b => b.Id == systemId && b.UserId == userId && !b.IsDeleted);
        }

        private async Task<BookingSystem?> GetOwnedSystemWithServicesTrackedAsync(int systemId, int userId)
        {
            return await _context.BookingSystems
                .Include(b => b.Services)
                    .ThenInclude(s => s.DaySchedules)
                .Include(b => b.Services)
                    .ThenInclude(s => s.ScheduleExceptions)
                .FirstOrDefaultAsync(b => b.Id == systemId && b.UserId == userId && !b.IsDeleted);
        }

        private async Task<BookingServiceItem?> GetServiceByIdAsync(int systemId, int serviceId, int userId)
        {
            return await _context.BookingServiceItems
                .Include(s => s.DaySchedules)
                .Include(s => s.ScheduleExceptions.Where(e => !e.IsDeleted))
                .AsNoTracking()
                .FirstOrDefaultAsync(s =>
                    s.Id == serviceId &&
                    s.BookingSystemId == systemId &&
                    !s.IsDeleted &&
                    s.BookingSystem.UserId == userId &&
                    !s.BookingSystem.IsDeleted);
        }

        private async Task<BookingServiceItem?> GetServiceByIdTrackedAsync(int systemId, int serviceId, int userId)
        {
            return await _context.BookingServiceItems
                .Include(s => s.DaySchedules)
                .Include(s => s.ScheduleExceptions)
                .Include(s => s.BookingSystem)
                .FirstOrDefaultAsync(s =>
                    s.Id == serviceId &&
                    s.BookingSystemId == systemId &&
                    !s.IsDeleted &&
                    s.BookingSystem.UserId == userId &&
                    !s.BookingSystem.IsDeleted);
        }

        private async Task<(bool Success, string? ErrorMessage, BookingSystemDraft? Draft, BookingStep1Dto? Step1)> LoadDraftStep1Async(int userId, string draftId)
        {
            var draft = await _draftRepository.GetActiveByDraftIdAsync(draftId, userId);
            if (draft == null)
            {
                return (false, "پیش‌نویس یافت نشد یا منقضی شده است", null, null);
            }

            var step1 = JsonSerializer.Deserialize<BookingStep1Dto>(draft.Step1Data);
            if (step1 == null)
            {
                return (false, "خطا در خواندن داده‌های مرحله 1", draft, null);
            }

            return (true, null, draft, step1);
        }

        private async Task<(bool Success, string? ErrorMessage, BookingSystemDraft? Draft, BookingStep1Dto? Step1, BookingStep2Dto? Step2)> LoadDraftSteps12Async(int userId, string draftId)
        {
            var step1Result = await LoadDraftStep1Async(userId, draftId);
            if (!step1Result.Success)
            {
                return (false, step1Result.ErrorMessage, step1Result.Draft, step1Result.Step1, null);
            }

            if (string.IsNullOrEmpty(step1Result.Draft!.Step2Data))
            {
                return (false, "مرحله 2 هنوز تکمیل نشده است", step1Result.Draft, step1Result.Step1, null);
            }

            var step2 = JsonSerializer.Deserialize<BookingStep2Dto>(step1Result.Draft.Step2Data);
            if (step2 == null)
            {
                return (false, "خطا در خواندن داده‌های مرحله 2", step1Result.Draft, step1Result.Step1, null);
            }

            return (true, null, step1Result.Draft, step1Result.Step1, step2);
        }

        private async Task<(bool Success, string? ErrorMessage, BookingSystemDraft? Draft, BookingStep1Dto? Step1, BookingStep2Dto? Step2, BookingStep3Dto? Step3)> LoadDraftSteps123Async(int userId, string draftId)
        {
            var result = await LoadDraftSteps12Async(userId, draftId);
            if (!result.Success)
            {
                return (false, result.ErrorMessage, result.Draft, result.Step1, result.Step2, null);
            }

            if (string.IsNullOrEmpty(result.Draft!.Step3Data))
            {
                return (false, "مرحله 3 هنوز تکمیل نشده است", result.Draft, result.Step1, result.Step2, null);
            }

            var step3 = JsonSerializer.Deserialize<BookingStep3Dto>(result.Draft.Step3Data);
            if (step3 == null)
            {
                return (false, "خطا در خواندن داده‌های مرحله 3", result.Draft, result.Step1, result.Step2, null);
            }

            return (true, null, result.Draft, result.Step1, result.Step2, step3);
        }

        private async Task<(bool Success, string? ErrorMessage, BookingStep1Dto? Step1, BookingStep2Dto? Step2, BookingStep3Dto? Step3, BookingStep4Dto? Step4)> LoadAllDraftStepsAsync(int userId, string draftId)
        {
            var result = await LoadDraftSteps123Async(userId, draftId);
            if (!result.Success)
            {
                return (false, result.ErrorMessage, result.Step1, result.Step2, result.Step3, null);
            }

            if (string.IsNullOrEmpty(result.Draft!.Step4Data))
            {
                return (false, "مرحله 4 هنوز تکمیل نشده است", result.Step1, result.Step2, result.Step3, null);
            }

            var step4 = JsonSerializer.Deserialize<BookingStep4Dto>(result.Draft.Step4Data);
            if (step4 == null)
            {
                return (false, "خطا در خواندن داده‌های مرحله 4", result.Step1, result.Step2, result.Step3, null);
            }

            return (true, null, result.Step1, result.Step2, result.Step3, step4);
        }
    }
}
