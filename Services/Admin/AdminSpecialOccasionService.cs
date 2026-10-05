using Api_Vapp.Constants;
using Api_Vapp.Data;
using Api_Vapp.DTOs.Admin;
using Api_Vapp.DTOs.Common;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;
using Api_Vapp.Services.Audit;
using Api_Vapp.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api_Vapp.Services.Admin
{
    /// <summary>
    /// مدیریت مناسبت‌های سیستمی/پیش‌فرض کاربران از پنل ادمین.
    /// </summary>
    public class AdminSpecialOccasionService : IAdminSpecialOccasionService
    {
        private static readonly HashSet<string> CatalogCodes = new(
            SystemOccasionCatalog.All.Select(x => x.Code),
            StringComparer.OrdinalIgnoreCase);

        private readonly Api_Context _context;
        private readonly IAuditService _audit;
        private readonly ILogger<AdminSpecialOccasionService> _logger;

        public AdminSpecialOccasionService(
            Api_Context context,
            IAuditService audit,
            ILogger<AdminSpecialOccasionService> logger)
        {
            _context = context;
            _audit = audit;
            _logger = logger;
        }

        public async Task<ApiResponse<List<SpecialOccasionAdminResponseDto>>> GetAllAsync(bool includeInactive = true)
        {
            try
            {
                _logger.LogInformation(
                    "Admin list system occasions — IncludeInactive: {IncludeInactive}",
                    includeInactive);

                var query = _context.SpecialOccasions.AsNoTracking()
                    .Where(o => o.IsSystem && !o.IsDeleted);

                if (!includeInactive)
                    query = query.Where(o => o.IsActive);

                var items = await query
                    .OrderBy(o => o.Category)
                    .ThenBy(o => o.SortOrder)
                    .ThenBy(o => o.Month)
                    .ThenBy(o => o.Day)
                    .ThenBy(o => o.Id)
                    .ToListAsync();

                var mapped = items.Select(Map).ToList();
                _logger.LogInformation("Admin system occasions listed — Count: {Count}", mapped.Count);
                return ApiResponse<List<SpecialOccasionAdminResponseDto>>.CreateSuccess(mapped);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در دریافت مناسبت‌های سیستمی ادمین");
                return ApiResponse<List<SpecialOccasionAdminResponseDto>>.InternalServerError(
                    ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<SpecialOccasionAdminResponseDto>> GetByIdAsync(int id)
        {
            try
            {
                var entity = await FindSystemOccasionAsync(id, tracking: false);
                if (entity == null)
                    return ApiResponse<SpecialOccasionAdminResponseDto>.NotFound(
                        "مناسبت سیستمی یافت نشد",
                        ErrorCodes.NotFound);

                return ApiResponse<SpecialOccasionAdminResponseDto>.CreateSuccess(Map(entity));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در دریافت مناسبت سیستمی ادمین — Id: {Id}", id);
                return ApiResponse<SpecialOccasionAdminResponseDto>.InternalServerError(
                    ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<SpecialOccasionAdminResponseDto>> CreateAsync(CreateSpecialOccasionAdminDto dto)
        {
            try
            {
                if (!TryNormalizeFields(
                        dto.Name,
                        dto.Type,
                        dto.Category,
                        dto.CalendarType,
                        dto.Month,
                        dto.Day,
                        dto.DefaultMessage,
                        out var name,
                        out var type,
                        out var category,
                        out var calendarType,
                        out var month,
                        out var day,
                        out var occasionDate,
                        out var defaultMessage,
                        out var error))
                {
                    return ApiResponse<SpecialOccasionAdminResponseDto>.BadRequest(
                        error!,
                        errorCode: ErrorCodes.ValidationFailed);
                }

                var codeResult = await ResolveCreateCodeAsync(dto.Code);
                if (!codeResult.Success)
                    return codeResult.Error!;

                var entity = new SpecialOccasion
                {
                    Code = codeResult.Code,
                    Name = name,
                    Type = type,
                    Category = category,
                    CalendarType = calendarType,
                    Month = (byte)month,
                    Day = (byte)day,
                    OccasionDate = occasionDate,
                    DefaultMessage = defaultMessage,
                    SortOrder = dto.SortOrder,
                    IsSystem = true,
                    IsActive = dto.IsActive,
                    IsDeleted = false,
                    UserId = null,
                    CreatedAt = DateTime.UtcNow
                };

                _context.SpecialOccasions.Add(entity);
                await _context.SaveChangesAsync();

                await _audit.WriteAsync(new AuditEntry
                {
                    Category = AuditCategories.Admin,
                    Action = AuditActions.SpecialOccasionCreated,
                    EntityType = AuditEntityTypes.SpecialOccasion,
                    EntityId = entity.Id.ToString(),
                    After = Snapshot(entity)
                });

                _logger.LogInformation(
                    "Admin created system occasion — Id: {Id}, Code: {Code}",
                    entity.Id,
                    entity.Code);

                return ApiResponse<SpecialOccasionAdminResponseDto>.CreateSuccess(
                    Map(entity),
                    "مناسبت سیستمی با موفقیت ایجاد شد");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در ایجاد مناسبت سیستمی ادمین");
                return ApiResponse<SpecialOccasionAdminResponseDto>.InternalServerError(
                    ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<SpecialOccasionAdminResponseDto>> UpdateAsync(
            int id,
            UpdateSpecialOccasionAdminDto dto)
        {
            try
            {
                var entity = await FindSystemOccasionAsync(id, tracking: true);
                if (entity == null)
                    return ApiResponse<SpecialOccasionAdminResponseDto>.NotFound(
                        "مناسبت سیستمی یافت نشد",
                        ErrorCodes.NotFound);

                if (!TryNormalizeFields(
                        dto.Name,
                        dto.Type,
                        dto.Category,
                        dto.CalendarType,
                        dto.Month,
                        dto.Day,
                        dto.DefaultMessage,
                        out var name,
                        out var type,
                        out var category,
                        out var calendarType,
                        out var month,
                        out var day,
                        out var occasionDate,
                        out var defaultMessage,
                        out var error))
                {
                    return ApiResponse<SpecialOccasionAdminResponseDto>.BadRequest(
                        error!,
                        errorCode: ErrorCodes.ValidationFailed);
                }

                var before = Snapshot(entity);

                entity.Name = name;
                entity.Type = type;
                entity.Category = category;
                entity.CalendarType = calendarType;
                entity.Month = (byte)month;
                entity.Day = (byte)day;
                entity.OccasionDate = occasionDate;
                entity.DefaultMessage = defaultMessage;
                entity.SortOrder = dto.SortOrder;
                entity.IsActive = dto.IsActive;
                entity.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                await _audit.WriteAsync(new AuditEntry
                {
                    Category = AuditCategories.Admin,
                    Action = AuditActions.SpecialOccasionUpdated,
                    EntityType = AuditEntityTypes.SpecialOccasion,
                    EntityId = entity.Id.ToString(),
                    Before = before,
                    After = Snapshot(entity)
                });

                _logger.LogInformation("Admin updated system occasion — Id: {Id}", id);
                return ApiResponse<SpecialOccasionAdminResponseDto>.CreateSuccess(
                    Map(entity),
                    "مناسبت سیستمی با موفقیت به‌روزرسانی شد");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در به‌روزرسانی مناسبت سیستمی ادمین — Id: {Id}", id);
                return ApiResponse<SpecialOccasionAdminResponseDto>.InternalServerError(
                    ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<bool>> DeleteAsync(int id)
        {
            try
            {
                var entity = await FindSystemOccasionAsync(id, tracking: true);
                if (entity == null)
                    return ApiResponse<bool>.NotFound("مناسبت سیستمی یافت نشد", ErrorCodes.NotFound);

                var before = Snapshot(entity);
                entity.IsDeleted = true;
                entity.IsActive = false;
                entity.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                await _audit.WriteAsync(new AuditEntry
                {
                    Category = AuditCategories.Admin,
                    Action = AuditActions.SpecialOccasionDeleted,
                    EntityType = AuditEntityTypes.SpecialOccasion,
                    EntityId = entity.Id.ToString(),
                    Before = before,
                    After = Snapshot(entity)
                });

                _logger.LogInformation("Admin deleted system occasion — Id: {Id}", id);
                return ApiResponse<bool>.CreateSuccess(true, "مناسبت سیستمی با موفقیت حذف شد");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در حذف مناسبت سیستمی ادمین — Id: {Id}", id);
                return ApiResponse<bool>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        private async Task<SpecialOccasion?> FindSystemOccasionAsync(int id, bool tracking)
        {
            var query = tracking
                ? _context.SpecialOccasions.AsQueryable()
                : _context.SpecialOccasions.AsNoTracking();

            return await query.FirstOrDefaultAsync(o =>
                o.Id == id && o.IsSystem && !o.IsDeleted);
        }

        private async Task<(bool Success, string Code, ApiResponse<SpecialOccasionAdminResponseDto>? Error)> ResolveCreateCodeAsync(
            string? requestedCode)
        {
            string code;
            if (!string.IsNullOrWhiteSpace(requestedCode))
            {
                code = requestedCode.Trim().ToUpperInvariant();
            }
            else
            {
                code = $"ADMIN_{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid().ToString("N")[..8]}"
                    .ToUpperInvariant();
            }

            var exists = await _context.SpecialOccasions
                .AnyAsync(o => o.Code == code && !o.IsDeleted);

            if (exists)
            {
                return (
                    false,
                    code,
                    ApiResponse<SpecialOccasionAdminResponseDto>.BadRequest(
                        "کد مناسبت تکراری است",
                        errorCode: ErrorCodes.ValidationFailed));
            }

            return (true, code, null);
        }

        private static bool TryNormalizeFields(
            string? nameRaw,
            string? typeRaw,
            string? categoryRaw,
            string? calendarRaw,
            int month,
            int day,
            string? defaultMessageRaw,
            out string name,
            out string type,
            out string category,
            out string calendarType,
            out int resolvedMonth,
            out int resolvedDay,
            out DateTime occasionDate,
            out string? defaultMessage,
            out string? error)
        {
            name = string.Empty;
            type = OccasionTypeCodes.Custom;
            category = OccasionCategories.Congratulation;
            calendarType = OccasionCalendarTypes.Jalali;
            resolvedMonth = 0;
            resolvedDay = 0;
            occasionDate = default;
            defaultMessage = null;
            error = null;

            name = (nameRaw ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                error = "نام مناسبت الزامی است";
                return false;
            }

            if (!OccasionTypeCodes.IsKnown(typeRaw))
            {
                error = "نوع مناسبت نامعتبر است";
                return false;
            }

            type = OccasionTypeCodes.Normalize(typeRaw);

            if (!string.IsNullOrWhiteSpace(categoryRaw) && !OccasionCategories.IsKnown(categoryRaw))
            {
                error = "دسته‌بندی مناسبت نامعتبر است";
                return false;
            }

            category = string.IsNullOrWhiteSpace(categoryRaw)
                ? OccasionTypeCodes.ToCategory(type)
                : OccasionCategories.Normalize(categoryRaw);

            if (!string.IsNullOrWhiteSpace(calendarRaw)
                && !string.Equals(calendarRaw.Trim(), OccasionCalendarTypes.Jalali, StringComparison.OrdinalIgnoreCase))
            {
                error = "فقط تقویم شمسی پشتیبانی می‌شود";
                return false;
            }

            calendarType = OccasionCalendarTypes.Jalali;

            if (month < 1 || month > 12 || day < 1 || day > 31)
            {
                error = "ماه یا روز مناسبت نامعتبر است";
                return false;
            }

            resolvedMonth = month;
            resolvedDay = day;
            occasionDate = OccasionCalendarHelper.BuildReferenceOccasionDateUtc(calendarType, month, day);
            defaultMessage = string.IsNullOrWhiteSpace(defaultMessageRaw)
                ? null
                : defaultMessageRaw.Trim();
            return true;
        }

        private static SpecialOccasionAdminResponseDto Map(SpecialOccasion entity) => new()
        {
            Id = entity.Id,
            Code = entity.Code,
            Name = entity.Name,
            Type = OccasionTypeCodes.Normalize(entity.Type),
            Category = OccasionCategories.Normalize(entity.Category),
            CategoryPersian = OccasionCategories.ToPersian(entity.Category),
            CalendarType = OccasionCalendarTypes.Normalize(entity.CalendarType),
            Month = entity.Month,
            Day = entity.Day,
            OccasionDate = entity.OccasionDate,
            DefaultMessage = entity.DefaultMessage,
            SortOrder = entity.SortOrder,
            IsActive = entity.IsActive,
            IsFromCatalog = !string.IsNullOrWhiteSpace(entity.Code)
                && CatalogCodes.Contains(entity.Code),
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };

        private static object Snapshot(SpecialOccasion entity) => new
        {
            entity.Id,
            entity.Code,
            entity.Name,
            entity.Type,
            entity.Category,
            entity.CalendarType,
            entity.Month,
            entity.Day,
            entity.DefaultMessage,
            entity.SortOrder,
            entity.IsActive,
            entity.IsDeleted,
            entity.IsSystem
        };
    }
}
