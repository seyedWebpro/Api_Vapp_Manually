using Api_Vapp.Constants;
using Api_Vapp.Data;
using Api_Vapp.DTOs.Automation;
using Api_Vapp.DTOs.Common;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;
using Api_Vapp.Services.Audit;
using Api_Vapp.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api_Vapp.Services
{
    /// <summary>
    /// سرویس جدول تبریک و تسلیت مناسبتی + CRUD مناسبت‌های سفارشی
    /// </summary>
    public class SpecialOccasionService : ISpecialOccasionService
    {
        private readonly ISpecialOccasionRepository _specialOccasionRepository;
        private readonly IUserOccasionPreferenceRepository _preferenceRepository;
        private readonly IUserOccasionProfileRepository _profileRepository;
        private readonly Api_Context _context;
        private readonly IAuditService _audit;
        private readonly ILogger<SpecialOccasionService> _logger;

        public SpecialOccasionService(
            ISpecialOccasionRepository specialOccasionRepository,
            IUserOccasionPreferenceRepository preferenceRepository,
            IUserOccasionProfileRepository profileRepository,
            Api_Context context,
            IAuditService audit,
            ILogger<SpecialOccasionService> logger)
        {
            _specialOccasionRepository = specialOccasionRepository;
            _preferenceRepository = preferenceRepository;
            _profileRepository = profileRepository;
            _context = context;
            _audit = audit;
            _logger = logger;
        }

        public async Task<ApiResponse<SpecialOccasionResponseDto>> CreateSpecialOccasionAsync(int userId, CreateSpecialOccasionDto createDto)
        {
            try
            {
                if (!TryResolveDateParts(createDto, out var calendarType, out var month, out var day, out var occasionDate, out var error))
                    return ApiResponse<SpecialOccasionResponseDto>.BadRequest(error!, errorCode: ErrorCodes.ValidationFailed);

                var type = OccasionTypeCodes.Normalize(createDto.Type);
                var category = string.IsNullOrWhiteSpace(createDto.Category)
                    ? OccasionTypeCodes.ToCategory(type)
                    : OccasionCategories.Normalize(createDto.Category);

                if (!OccasionCategories.IsKnown(category))
                    return ApiResponse<SpecialOccasionResponseDto>.BadRequest("دسته‌بندی مناسبت نامعتبر است", errorCode: ErrorCodes.InvalidInput);

                var occasion = new SpecialOccasion
                {
                    UserId = userId,
                    Name = createDto.Name.Trim(),
                    Type = type,
                    Category = category,
                    CalendarType = calendarType,
                    Month = (byte)month,
                    Day = (byte)day,
                    OccasionDate = occasionDate,
                    DefaultMessage = NormalizeMessage(createDto.DefaultMessage),
                    IsSystem = false,
                    IsActive = true,
                    IsDeleted = false,
                    SortOrder = 1000,
                    CreatedAt = DateTime.UtcNow
                };

                await _specialOccasionRepository.AddAsync(occasion);

                await _preferenceRepository.AddAsync(new UserOccasionPreference
                {
                    UserId = userId,
                    SpecialOccasionId = occasion.Id,
                    IsEnabled = true,
                    CustomMessage = occasion.DefaultMessage,
                    TemplateApprovalStatus = string.IsNullOrWhiteSpace(occasion.DefaultMessage)
                        ? AdminApprovalStatuses.Approved
                        : AdminApprovalStatuses.Pending,
                    CreatedAt = DateTime.UtcNow
                });

                await _audit.WriteAsync(new AuditEntry
                {
                    Category = AuditCategories.Message,
                    Action = AuditActions.SpecialOccasionCreated,
                    EntityType = AuditEntityTypes.SpecialOccasion,
                    EntityId = occasion.Id.ToString(),
                    ActorUserId = userId,
                    After = new { name = occasion.Name, type = occasion.Type, category = occasion.Category, month, day, calendarType }
                });

                _logger.LogInformation("Custom occasion created — UserId: {UserId}, OccasionId: {OccasionId}", userId, occasion.Id);

                return ApiResponse<SpecialOccasionResponseDto>.CreateSuccess(
                    MapToDto(occasion),
                    "مناسبت با موفقیت ایجاد شد",
                    201);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در ایجاد مناسبت — UserId: {UserId}", userId);
                return ApiResponse<SpecialOccasionResponseDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<List<SpecialOccasionResponseDto>>> GetSpecialOccasionsAsync(int? userId)
        {
            try
            {
                if (!userId.HasValue)
                    return ApiResponse<List<SpecialOccasionResponseDto>>.Unauthorized(ControlledErrorHelper.Unauthorized, ErrorCodes.Unauthorized);

                var catalog = await _specialOccasionRepository.GetCatalogForUserAsync(userId.Value);
                return ApiResponse<List<SpecialOccasionResponseDto>>.CreateSuccess(catalog.Select(MapToDto).ToList());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در دریافت لیست مناسبت‌ها — UserId: {UserId}", userId);
                return ApiResponse<List<SpecialOccasionResponseDto>>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<SpecialOccasionResponseDto>> GetSpecialOccasionByIdAsync(int id)
        {
            try
            {
                var occasion = await _specialOccasionRepository.GetByIdAsync(id);
                if (occasion == null)
                    return ApiResponse<SpecialOccasionResponseDto>.NotFound("مناسبت مورد نظر یافت نشد", ErrorCodes.NotFound);

                return ApiResponse<SpecialOccasionResponseDto>.CreateSuccess(MapToDto(occasion));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در دریافت مناسبت — OccasionId: {OccasionId}", id);
                return ApiResponse<SpecialOccasionResponseDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<SpecialOccasionResponseDto>> UpdateSpecialOccasionAsync(int id, int? userId, UpdateSpecialOccasionDto updateDto)
        {
            try
            {
                var occasion = await _specialOccasionRepository.GetByIdAsync(id);
                if (occasion == null)
                    return ApiResponse<SpecialOccasionResponseDto>.NotFound("مناسبت مورد نظر یافت نشد", ErrorCodes.NotFound);

                if (occasion.IsSystem)
                    return ApiResponse<SpecialOccasionResponseDto>.Forbidden("مناسبت‌های سیستمی قابل ویرایش نیستند", ErrorCodes.Forbidden);

                if (occasion.UserId != userId)
                    return ApiResponse<SpecialOccasionResponseDto>.Forbidden("شما مجاز به ویرایش این مناسبت نیستید", ErrorCodes.Forbidden);

                if (updateDto.Name != null) occasion.Name = updateDto.Name.Trim();
                if (updateDto.Type != null) occasion.Type = OccasionTypeCodes.Normalize(updateDto.Type);
                if (updateDto.Category != null) occasion.Category = OccasionCategories.Normalize(updateDto.Category);
                if (updateDto.CalendarType != null) occasion.CalendarType = OccasionCalendarTypes.Normalize(updateDto.CalendarType);

                if (updateDto.Month.HasValue || updateDto.Day.HasValue || updateDto.OccasionDate.HasValue || updateDto.CalendarType != null)
                {
                    var calendarType = occasion.CalendarType;
                    int month = occasion.Month;
                    int day = occasion.Day;

                    if (updateDto.Month.HasValue) month = updateDto.Month.Value;
                    if (updateDto.Day.HasValue) day = updateDto.Day.Value;

                    if (updateDto.OccasionDate.HasValue && !updateDto.Month.HasValue && !updateDto.Day.HasValue)
                    {
                        var parts = OccasionCalendarHelper.ExtractMonthDayFromDate(updateDto.OccasionDate.Value, calendarType);
                        month = parts.Month;
                        day = parts.Day;
                        occasion.OccasionDate = updateDto.OccasionDate.Value.EnsureDateOnlyUtc();
                    }
                    else
                    {
                        occasion.OccasionDate = OccasionCalendarHelper.BuildReferenceOccasionDateUtc(calendarType, month, day);
                    }

                    occasion.Month = (byte)month;
                    occasion.Day = (byte)day;
                }

                if (updateDto.DefaultMessage != null)
                    occasion.DefaultMessage = NormalizeMessage(updateDto.DefaultMessage);
                if (updateDto.IsActive.HasValue)
                    occasion.IsActive = updateDto.IsActive.Value;

                occasion.UpdatedAt = DateTime.UtcNow;
                await _specialOccasionRepository.UpdateAsync(occasion);

                await _audit.WriteAsync(new AuditEntry
                {
                    Category = AuditCategories.Message,
                    Action = AuditActions.SpecialOccasionUpdated,
                    EntityType = AuditEntityTypes.SpecialOccasion,
                    EntityId = occasion.Id.ToString(),
                    ActorUserId = userId,
                    After = new { name = occasion.Name, type = occasion.Type, isActive = occasion.IsActive }
                });

                return ApiResponse<SpecialOccasionResponseDto>.CreateSuccess(MapToDto(occasion), "مناسبت با موفقیت به‌روزرسانی شد");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در به‌روزرسانی مناسبت — OccasionId: {OccasionId}", id);
                return ApiResponse<SpecialOccasionResponseDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<bool>> DeleteSpecialOccasionAsync(int id, int? userId)
        {
            try
            {
                var occasion = await _specialOccasionRepository.GetByIdAsync(id);
                if (occasion == null)
                    return ApiResponse<bool>.NotFound("مناسبت مورد نظر یافت نشد", ErrorCodes.NotFound);

                if (occasion.IsSystem)
                    return ApiResponse<bool>.Forbidden("مناسبت‌های سیستمی قابل حذف نیستند", ErrorCodes.Forbidden);

                if (occasion.UserId != userId)
                    return ApiResponse<bool>.Forbidden("شما مجاز به حذف این مناسبت نیستید", ErrorCodes.Forbidden);

                occasion.IsDeleted = true;
                occasion.UpdatedAt = DateTime.UtcNow;
                await _specialOccasionRepository.UpdateAsync(occasion);

                await _audit.WriteAsync(new AuditEntry
                {
                    Category = AuditCategories.Message,
                    Action = AuditActions.SpecialOccasionDeleted,
                    EntityType = AuditEntityTypes.SpecialOccasion,
                    EntityId = occasion.Id.ToString(),
                    ActorUserId = userId
                });

                return ApiResponse<bool>.CreateSuccess(true, "مناسبت با موفقیت حذف شد");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در حذف مناسبت — OccasionId: {OccasionId}", id);
                return ApiResponse<bool>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<OccasionTableResponseDto>> GetOccasionTableAsync(int userId, string? category = null)
        {
            try
            {
                var profile = await _profileRepository.GetOrCreateAsync(userId);
                var catalog = await _specialOccasionRepository.GetCatalogForUserAsync(userId);
                var prefMap = await _preferenceRepository.GetMapByUserIdAsync(userId);
                var today = OccasionCalendarHelper.GetTodayParts();

                if (!string.IsNullOrWhiteSpace(category))
                {
                    var normalized = OccasionCategories.Normalize(category);
                    catalog = catalog.Where(o => string.Equals(o.Category, normalized, StringComparison.OrdinalIgnoreCase)).ToList();
                }

                var items = catalog.Select(o =>
                {
                    prefMap.TryGetValue(o.Id, out var pref);
                    return MapToTableItem(o, pref, today);
                }).ToList();

                var response = new OccasionTableResponseDto
                {
                    Profile = MapProfile(profile),
                    Items = items,
                    TotalCount = items.Count,
                    EnabledCount = items.Count(i => i.IsEnabled),
                    Today = MapToday(today)
                };

                return ApiResponse<OccasionTableResponseDto>.CreateSuccess(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در دریافت جدول مناسبتی — UserId: {UserId}", userId);
                return ApiResponse<OccasionTableResponseDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<UserOccasionProfileDto>> GetProfileAsync(int userId)
        {
            try
            {
                var profile = await _profileRepository.GetOrCreateAsync(userId);
                return ApiResponse<UserOccasionProfileDto>.CreateSuccess(MapProfile(profile));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در دریافت پروفایل مناسبتی — UserId: {UserId}", userId);
                return ApiResponse<UserOccasionProfileDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<UserOccasionProfileDto>> UpdateProfileAsync(int userId, UpdateUserOccasionProfileDto dto)
        {
            try
            {
                var profile = await _profileRepository.GetOrCreateAsync(userId);

                if (dto.BusinessName != null)
                    profile.BusinessName = string.IsNullOrWhiteSpace(dto.BusinessName) ? null : dto.BusinessName.Trim();

                if (dto.CongratulationsEnabled.HasValue)
                    profile.CongratulationsEnabled = dto.CongratulationsEnabled.Value;

                if (dto.CondolencesEnabled.HasValue)
                    profile.CondolencesEnabled = dto.CondolencesEnabled.Value;

                if (dto.ScheduledTimeTehran != null)
                {
                    if (!TimeSpan.TryParseExact(
                            dto.ScheduledTimeTehran.Trim(),
                            new[] { @"hh\:mm", @"h\:mm" },
                            System.Globalization.CultureInfo.InvariantCulture,
                            out var sendTime)
                        || sendTime < TimeSpan.Zero
                        || sendTime >= TimeSpan.FromDays(1))
                    {
                        return ApiResponse<UserOccasionProfileDto>.BadRequest(
                            "فرمت ساعت نامعتبر است. باید به صورت HH:mm باشد",
                            errorCode: ErrorCodes.ValidationFailed);
                    }

                    profile.ScheduledTimeTehran = sendTime;
                }

                if (dto.AutomatedMessageId.HasValue)
                {
                    if (dto.AutomatedMessageId.Value == 0)
                    {
                        profile.AutomatedMessageId = null;
                    }
                    else
                    {
                        var am = await _context.AutomatedMessages.AsNoTracking()
                            .FirstOrDefaultAsync(a => a.Id == dto.AutomatedMessageId.Value
                                && a.UserId == userId
                                && !a.IsDeleted
                                && a.AutomationType == AutomationTypeCodes.SpecialOccasion);

                        if (am == null)
                            return ApiResponse<UserOccasionProfileDto>.BadRequest(
                                "پیام خودکار مناسبتی معتبر یافت نشد",
                                errorCode: ErrorCodes.InvalidInput);

                        profile.AutomatedMessageId = am.Id;

                        // همگام‌سازی زمان ارسال روی AutomatedMessage (برای سازگاری با بک‌گراند)
                        var tracked = await _context.AutomatedMessages.FirstAsync(a => a.Id == am.Id);
                        tracked.ScheduledTime = profile.ScheduledTimeTehran;
                        tracked.UpdatedAt = DateTime.UtcNow;
                    }
                }
                else if (profile.AutomatedMessageId.HasValue && dto.ScheduledTimeTehran != null)
                {
                    var tracked = await _context.AutomatedMessages
                        .FirstOrDefaultAsync(a => a.Id == profile.AutomatedMessageId.Value && a.UserId == userId && !a.IsDeleted);
                    if (tracked != null)
                    {
                        tracked.ScheduledTime = profile.ScheduledTimeTehran;
                        tracked.UpdatedAt = DateTime.UtcNow;
                    }
                }

                await _profileRepository.UpdateAsync(profile);
                await _context.SaveChangesAsync();

                await _audit.WriteAsync(new AuditEntry
                {
                    Category = AuditCategories.Message,
                    Action = AuditActions.OccasionProfileUpdated,
                    EntityType = AuditEntityTypes.UserOccasionProfile,
                    EntityId = profile.Id.ToString(),
                    ActorUserId = userId,
                    After = new
                    {
                        profile.BusinessName,
                        profile.CongratulationsEnabled,
                        profile.CondolencesEnabled,
                        scheduledTimeTehran = profile.ScheduledTimeTehran?.ToString(@"hh\:mm")
                    }
                });

                return ApiResponse<UserOccasionProfileDto>.CreateSuccess(MapProfile(profile), "تنظیمات با موفقیت ذخیره شد");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در به‌روزرسانی پروفایل مناسبتی — UserId: {UserId}", userId);
                return ApiResponse<UserOccasionProfileDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<OccasionTableItemDto>> TogglePreferenceAsync(int userId, int occasionId, ToggleOccasionPreferenceDto dto)
        {
            try
            {
                var occasion = await EnsureAccessibleOccasionAsync(userId, occasionId);
                if (occasion == null)
                    return ApiResponse<OccasionTableItemDto>.NotFound("مناسبت مورد نظر یافت نشد", ErrorCodes.NotFound);

                var preference = await GetOrCreatePreferenceAsync(userId, occasionId);
                preference.IsEnabled = dto.IsEnabled;
                await _preferenceRepository.UpdateAsync(preference);

                await _audit.WriteAsync(new AuditEntry
                {
                    Category = AuditCategories.Message,
                    Action = AuditActions.OccasionPreferenceUpdated,
                    EntityType = AuditEntityTypes.UserOccasionPreference,
                    EntityId = preference.Id.ToString(),
                    ActorUserId = userId,
                    After = new { occasionId, isEnabled = dto.IsEnabled }
                });

                return ApiResponse<OccasionTableItemDto>.CreateSuccess(
                    MapToTableItem(occasion, preference, OccasionCalendarHelper.GetTodayParts()),
                    dto.IsEnabled ? "مناسبت فعال شد" : "مناسبت غیرفعال شد");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در تغییر وضعیت مناسبت — UserId: {UserId}, OccasionId: {OccasionId}", userId, occasionId);
                return ApiResponse<OccasionTableItemDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<UserOccasionProfileDto>> ToggleCategoryAsync(int userId, ToggleOccasionCategoryDto dto)
        {
            try
            {
                if (!OccasionCategories.IsKnown(dto.Category))
                    return ApiResponse<UserOccasionProfileDto>.BadRequest("دسته‌بندی نامعتبر است", errorCode: ErrorCodes.InvalidInput);

                var profile = await _profileRepository.GetOrCreateAsync(userId);
                var category = OccasionCategories.Normalize(dto.Category);

                if (category == OccasionCategories.Condolence)
                    profile.CondolencesEnabled = dto.IsEnabled;
                else
                    profile.CongratulationsEnabled = dto.IsEnabled;

                await _profileRepository.UpdateAsync(profile);

                await _audit.WriteAsync(new AuditEntry
                {
                    Category = AuditCategories.Message,
                    Action = AuditActions.OccasionProfileUpdated,
                    EntityType = AuditEntityTypes.UserOccasionProfile,
                    EntityId = profile.Id.ToString(),
                    ActorUserId = userId,
                    After = new { category, isEnabled = dto.IsEnabled }
                });

                return ApiResponse<UserOccasionProfileDto>.CreateSuccess(
                    MapProfile(profile),
                    dto.IsEnabled
                        ? $"{OccasionCategories.ToPersian(category)}‌ها فعال شدند"
                        : $"{OccasionCategories.ToPersian(category)}‌ها غیرفعال شدند");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در تغییر دسته مناسبتی — UserId: {UserId}", userId);
                return ApiResponse<UserOccasionProfileDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<OccasionTableItemDto>> UpdateTemplateAsync(int userId, int occasionId, UpdateOccasionTemplateDto dto)
        {
            try
            {
                var occasion = await EnsureAccessibleOccasionAsync(userId, occasionId);
                if (occasion == null)
                    return ApiResponse<OccasionTableItemDto>.NotFound("مناسبت مورد نظر یافت نشد", ErrorCodes.NotFound);

                var message = NormalizeMessage(dto.CustomMessage);
                if (string.IsNullOrWhiteSpace(message))
                    return ApiResponse<OccasionTableItemDto>.BadRequest("متن قالب الزامی است", errorCode: ErrorCodes.ValidationFailed);

                var preference = await GetOrCreatePreferenceAsync(userId, occasionId);
                var unchangedApproved = string.Equals(preference.CustomMessage, message, StringComparison.Ordinal)
                    && string.Equals(preference.TemplateApprovalStatus, AdminApprovalStatuses.Approved, StringComparison.OrdinalIgnoreCase);

                // اگر همان متن پیش‌فرض ادمین باشد و تغییری نکرده، Approved بماند
                var isSameAsDefault = string.Equals(message, occasion.DefaultMessage?.Trim(), StringComparison.Ordinal);

                preference.CustomMessage = message;
                if (!unchangedApproved)
                {
                    preference.TemplateApprovalStatus = isSameAsDefault
                        ? AdminApprovalStatuses.Approved
                        : AdminApprovalStatuses.Pending;
                    preference.TemplateApprovedAt = isSameAsDefault ? DateTime.UtcNow : null;
                    preference.TemplateApprovedByUserId = null;
                    preference.TemplateRejectionReason = null;
                }

                // ثبت/به‌روزرسانی قالب در MessageTemplates برای صف تأیید متن
                await UpsertOccasionMessageTemplateAsync(userId, occasion, preference, message);

                await _preferenceRepository.UpdateAsync(preference);

                await _audit.WriteAsync(new AuditEntry
                {
                    Category = AuditCategories.Message,
                    Action = AuditActions.OccasionTemplateSubmitted,
                    EntityType = AuditEntityTypes.UserOccasionPreference,
                    EntityId = preference.Id.ToString(),
                    ActorUserId = userId,
                    After = new { occasionId, approvalStatus = preference.TemplateApprovalStatus }
                });

                var msg = string.Equals(preference.TemplateApprovalStatus, AdminApprovalStatuses.Pending, StringComparison.OrdinalIgnoreCase)
                    ? "قالب ذخیره شد و برای تأیید متن ارسال شد"
                    : "قالب با موفقیت ذخیره شد";

                return ApiResponse<OccasionTableItemDto>.CreateSuccess(
                    MapToTableItem(occasion, preference, OccasionCalendarHelper.GetTodayParts()),
                    msg);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در ویرایش قالب مناسبت — UserId: {UserId}, OccasionId: {OccasionId}", userId, occasionId);
                return ApiResponse<OccasionTableItemDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<OccasionTableItemDto>> ResetTemplateAsync(int userId, int occasionId)
        {
            try
            {
                var occasion = await EnsureAccessibleOccasionAsync(userId, occasionId);
                if (occasion == null)
                    return ApiResponse<OccasionTableItemDto>.NotFound("مناسبت مورد نظر یافت نشد", ErrorCodes.NotFound);

                var preference = await GetOrCreatePreferenceAsync(userId, occasionId);
                preference.CustomMessage = null;
                preference.TemplateApprovalStatus = AdminApprovalStatuses.Approved;
                preference.TemplateApprovedAt = DateTime.UtcNow;
                preference.TemplateRejectionReason = null;
                await _preferenceRepository.UpdateAsync(preference);

                return ApiResponse<OccasionTableItemDto>.CreateSuccess(
                    MapToTableItem(occasion, preference, OccasionCalendarHelper.GetTodayParts()),
                    "قالب به پیش‌فرض ادمین بازگردانده شد");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در بازنشانی قالب — UserId: {UserId}, OccasionId: {OccasionId}", userId, occasionId);
                return ApiResponse<OccasionTableItemDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        private async Task UpsertOccasionMessageTemplateAsync(
            int userId,
            SpecialOccasion occasion,
            UserOccasionPreference preference,
            string message)
        {
            MessageTemplate? template = null;
            if (preference.MessageTemplateId.HasValue)
            {
                template = await _context.MessageTemplates
                    .FirstOrDefaultAsync(t => t.Id == preference.MessageTemplateId.Value
                        && t.UserId == userId
                        && !t.IsDeleted);
            }

            if (template == null)
            {
                template = new MessageTemplate
                {
                    UserId = userId,
                    Name = $"مناسبت — {occasion.Name}",
                    Content = message,
                    Category = "مناسبت‌ها",
                    Description = $"قالب مناسبت {occasion.Name}",
                    IsDefault = false,
                    IsActive = true,
                    ApprovalStatus = preference.TemplateApprovalStatus,
                    CreatedAt = DateTime.UtcNow
                };
                await _context.MessageTemplates.AddAsync(template);
                await _context.SaveChangesAsync();
                preference.MessageTemplateId = template.Id;
            }
            else
            {
                template.Content = message;
                template.Name = $"مناسبت — {occasion.Name}";
                template.ApprovalStatus = preference.TemplateApprovalStatus;
                template.ApprovedAt = preference.TemplateApprovedAt;
                template.RejectionReason = preference.TemplateRejectionReason;
                template.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
        }

        private async Task<SpecialOccasion?> EnsureAccessibleOccasionAsync(int userId, int occasionId)
        {
            var occasion = await _specialOccasionRepository.GetByIdAsync(occasionId);
            if (occasion == null || !occasion.IsActive)
                return null;

            if (occasion.IsSystem)
                return occasion;

            return occasion.UserId == userId ? occasion : null;
        }

        private async Task<UserOccasionPreference> GetOrCreatePreferenceAsync(int userId, int occasionId)
        {
            var existing = await _preferenceRepository.GetByUserAndOccasionAsync(userId, occasionId);
            if (existing != null)
                return existing;

            var preference = new UserOccasionPreference
            {
                UserId = userId,
                SpecialOccasionId = occasionId,
                IsEnabled = true,
                TemplateApprovalStatus = AdminApprovalStatuses.Approved,
                CreatedAt = DateTime.UtcNow
            };
            await _preferenceRepository.AddAsync(preference);
            return preference;
        }

        private static bool TryResolveDateParts(
            CreateSpecialOccasionDto dto,
            out string calendarType,
            out int month,
            out int day,
            out DateTime occasionDate,
            out string? error)
        {
            calendarType = OccasionCalendarTypes.Normalize(dto.CalendarType);
            month = 0;
            day = 0;
            occasionDate = default;
            error = null;

            if (dto.Month.HasValue && dto.Day.HasValue)
            {
                month = dto.Month.Value;
                day = dto.Day.Value;
                if (month < 1 || month > 12 || day < 1 || day > 31)
                {
                    error = "ماه یا روز مناسبت نامعتبر است";
                    return false;
                }

                occasionDate = OccasionCalendarHelper.BuildReferenceOccasionDateUtc(calendarType, month, day);
                return true;
            }

            if (dto.OccasionDate.HasValue)
            {
                occasionDate = dto.OccasionDate.Value.EnsureDateOnlyUtc();
                var parts = OccasionCalendarHelper.ExtractMonthDayFromDate(occasionDate, calendarType);
                month = parts.Month;
                day = parts.Day;
                return true;
            }

            error = "تاریخ مناسبت یا ماه و روز الزامی است";
            return false;
        }

        private static string? NormalizeMessage(string? message) =>
            string.IsNullOrWhiteSpace(message) ? null : message.Trim();

        private static SpecialOccasionResponseDto MapToDto(SpecialOccasion occasion) => new()
        {
            Id = occasion.Id,
            Code = occasion.Code,
            Name = occasion.Name,
            Type = occasion.Type,
            Category = OccasionCategories.Normalize(occasion.Category),
            CategoryPersian = OccasionCategories.ToPersian(occasion.Category),
            CalendarType = OccasionCalendarTypes.Normalize(occasion.CalendarType),
            Month = occasion.Month,
            Day = occasion.Day,
            OccasionDate = occasion.OccasionDate,
            DefaultMessage = occasion.DefaultMessage,
            IsSystem = occasion.IsSystem,
            IsActive = occasion.IsActive,
            SortOrder = occasion.SortOrder,
            CreatedAt = occasion.CreatedAt
        };

        private static OccasionTableItemDto MapToTableItem(
            SpecialOccasion occasion,
            UserOccasionPreference? preference,
            OccasionCalendarHelper.CalendarDayParts today)
        {
            var isEnabled = preference?.IsEnabled ?? true;
            var approval = preference?.TemplateApprovalStatus ?? AdminApprovalStatuses.Approved;
            var effective = OccasionMessagePersonalizer.ResolveEffectiveTemplate(occasion, preference);
            var canSend = !string.IsNullOrWhiteSpace(effective)
                && string.Equals(approval, AdminApprovalStatuses.Approved, StringComparison.OrdinalIgnoreCase);

            // اگر قالب سفارشی Pending است ولی Default وجود دارد، ارسال با Default مجاز است
            if (!canSend && !string.IsNullOrWhiteSpace(occasion.DefaultMessage))
            {
                effective = occasion.DefaultMessage;
                canSend = true;
            }

            return new OccasionTableItemDto
            {
                Id = occasion.Id,
                Code = occasion.Code,
                Name = occasion.Name,
                Type = occasion.Type,
                Category = OccasionCategories.Normalize(occasion.Category),
                CategoryPersian = OccasionCategories.ToPersian(occasion.Category),
                CalendarType = OccasionCalendarTypes.Normalize(occasion.CalendarType),
                Month = occasion.Month,
                Day = occasion.Day,
                OccasionDate = occasion.OccasionDate,
                DaysRemaining = OccasionCalendarHelper.CalculateDaysRemaining(occasion.CalendarType, occasion.Month, occasion.Day),
                IsToday = OccasionCalendarHelper.IsOccasionToday(occasion.CalendarType, occasion.Month, occasion.Day, today),
                IsSystem = occasion.IsSystem,
                IsEnabled = isEnabled,
                EffectiveMessage = effective,
                DefaultMessage = occasion.DefaultMessage,
                CustomMessage = preference?.CustomMessage,
                TemplateApprovalStatus = approval,
                TemplateRejectionReason = preference?.TemplateRejectionReason,
                MessageTemplateId = preference?.MessageTemplateId,
                CanSendWithCurrentTemplate = canSend
            };
        }

        private static UserOccasionProfileDto MapProfile(UserOccasionProfile profile) => new()
        {
            BusinessName = profile.BusinessName,
            CongratulationsEnabled = profile.CongratulationsEnabled,
            CondolencesEnabled = profile.CondolencesEnabled,
            ScheduledTimeTehran = FormatTehranTime(profile.ScheduledTimeTehran),
            AutomatedMessageId = profile.AutomatedMessageId
        };

        private static string? FormatTehranTime(TimeSpan? time)
        {
            if (!time.HasValue)
                return null;

            // TimeSpan custom "hh" = hours 00–23 (not 12-hour clock)
            return time.Value.ToString(@"hh\:mm", System.Globalization.CultureInfo.InvariantCulture);
        }

        private static OccasionCalendarTodayDto MapToday(OccasionCalendarHelper.CalendarDayParts today) => new()
        {
            TehranDate = today.TehranDate.ToString("yyyy-MM-dd"),
            JalaliYear = today.JalaliYear,
            JalaliMonth = today.JalaliMonth,
            JalaliDay = today.JalaliDay,
            GregorianYear = today.GregorianYear,
            GregorianMonth = today.GregorianMonth,
            GregorianDay = today.GregorianDay,
            HijriYear = today.HijriYear,
            HijriMonth = today.HijriMonth,
            HijriDay = today.HijriDay
        };
    }
}
