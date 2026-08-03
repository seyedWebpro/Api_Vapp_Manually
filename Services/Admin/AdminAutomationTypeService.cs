using Api_Vapp.Constants;
using Api_Vapp.Data;
using Api_Vapp.DTOs.Admin;
using Api_Vapp.DTOs.Common;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;
using Api_Vapp.Services.Audit;
using Api_Vapp.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Api_Vapp.Services.Admin
{
    /// <summary>
    /// مدیریت انواع پیام خودکار — منبع حقیقت برای اپ، فقط از پنل ادمین.
    /// </summary>
    public class AdminAutomationTypeService : IAdminAutomationTypeService
    {
        private readonly Api_Context _context;
        private readonly IAuditService _audit;
        private readonly IMemoryCache _cache;
        private readonly ILogger<AdminAutomationTypeService> _logger;

        public AdminAutomationTypeService(
            Api_Context context,
            IAuditService audit,
            IMemoryCache cache,
            ILogger<AdminAutomationTypeService> logger)
        {
            _context = context;
            _audit = audit;
            _cache = cache;
            _logger = logger;
        }

        public async Task<ApiResponse<List<AutomationTypeAdminResponseDto>>> GetAllAsync(bool includeInactive = true)
        {
            try
            {
                _logger.LogInformation("شروع دریافت انواع پیام خودکار — IncludeInactive: {IncludeInactive}", includeInactive);

                var query = _context.AutomationTypes.AsNoTracking().Where(t => !t.IsDeleted);
                if (!includeInactive)
                    query = query.Where(t => t.IsActive);

                var types = await query
                    .OrderBy(t => t.SortOrder)
                    .ThenBy(t => t.Id)
                    .Select(t => new AutomationTypeAdminResponseDto
                    {
                        Id = t.Id,
                        Code = t.Code,
                        Name = t.Name,
                        Description = t.Description,
                        Icon = t.Icon,
                        SortOrder = t.SortOrder,
                        IsActive = t.IsActive,
                        CreatedAt = t.CreatedAt,
                        UpdatedAt = t.UpdatedAt
                    })
                    .ToListAsync();

                foreach (var type in types)
                {
                    type.IsSystemManaged = AutomationTypeCodes.IsKnown(type.Code);
                    type.CanChangeCode = false;
                    type.CanDelete = false;
                }

                _logger.LogInformation("پایان دریافت انواع پیام خودکار — Count: {Count}", types.Count);
                return ApiResponse<List<AutomationTypeAdminResponseDto>>.CreateSuccess(types);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در دریافت انواع پیام خودکار");
                return ApiResponse<List<AutomationTypeAdminResponseDto>>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<AutomationTypeAdminResponseDto>> GetByIdAsync(int id)
        {
            try
            {
                var type = await _context.AutomationTypes.AsNoTracking()
                    .Where(t => t.Id == id && !t.IsDeleted)
                    .Select(t => new AutomationTypeAdminResponseDto
                    {
                        Id = t.Id,
                        Code = t.Code,
                        Name = t.Name,
                        Description = t.Description,
                        Icon = t.Icon,
                        SortOrder = t.SortOrder,
                        IsActive = t.IsActive,
                        CreatedAt = t.CreatedAt,
                        UpdatedAt = t.UpdatedAt
                    })
                    .FirstOrDefaultAsync();

                if (type == null)
                    return ApiResponse<AutomationTypeAdminResponseDto>.NotFound("نوع پیام خودکار یافت نشد");

                type.IsSystemManaged = AutomationTypeCodes.IsKnown(type.Code);
                type.CanChangeCode = false;
                type.CanDelete = false;

                return ApiResponse<AutomationTypeAdminResponseDto>.CreateSuccess(type);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در دریافت نوع پیام خودکار — Id: {Id}", id);
                return ApiResponse<AutomationTypeAdminResponseDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<AutomationTypeAdminResponseDto>> UpdateAsync(int id, UpdateAutomationTypeDto dto)
        {
            try
            {
                _logger.LogInformation("شروع به‌روزرسانی نوع پیام خودکار — Id: {Id}", id);

                var type = await _context.AutomationTypes.FirstOrDefaultAsync(t => t.Id == id && !t.IsDeleted);
                if (type == null)
                    return ApiResponse<AutomationTypeAdminResponseDto>.NotFound("نوع پیام خودکار یافت نشد");

                if (!AutomationTypeCodes.IsKnown(type.Code))
                {
                    return ApiResponse<AutomationTypeAdminResponseDto>.BadRequest(
                        "این نوع قابل ویرایش نیست",
                        errorCode: ErrorCodes.InvalidInput);
                }

                var name = dto.Name.Trim();
                if (string.IsNullOrWhiteSpace(name))
                {
                    return ApiResponse<AutomationTypeAdminResponseDto>.BadRequest(
                        "نام الزامی است",
                        errorCode: ErrorCodes.ValidationFailed);
                }

                var before = Snapshot(type);

                type.Name = name;
                type.Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim();
                type.Icon = string.IsNullOrWhiteSpace(dto.Icon) ? null : dto.Icon.Trim();
                type.SortOrder = dto.SortOrder;
                type.IsActive = dto.IsActive;
                type.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                // اپ فقط از کش انواع فعال می‌خواند — بلافاصله باطل شود
                _cache.Remove(AutomationTypeCacheKeys.ActiveList);

                await _audit.WriteAsync(new AuditEntry
                {
                    Category = AuditCategories.Admin,
                    Action = AuditActions.AutomationTypeUpdated,
                    EntityType = AuditEntityTypes.AutomationType,
                    EntityId = type.Id.ToString(),
                    Before = before,
                    After = Snapshot(type)
                });

                _logger.LogInformation("پایان به‌روزرسانی نوع پیام خودکار — Id: {Id}, Code: {Code}", id, type.Code);
                return ApiResponse<AutomationTypeAdminResponseDto>.CreateSuccess(Map(type), "نوع پیام خودکار به‌روزرسانی شد");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در به‌روزرسانی نوع پیام خودکار — Id: {Id}", id);
                return ApiResponse<AutomationTypeAdminResponseDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        private static object Snapshot(AutomationTypeDefinition type) => new
        {
            type.Id,
            type.Code,
            type.Name,
            type.Description,
            type.Icon,
            type.SortOrder,
            type.IsActive
        };

        private static AutomationTypeAdminResponseDto Map(AutomationTypeDefinition type) => new()
        {
            Id = type.Id,
            Code = type.Code,
            Name = type.Name,
            Description = type.Description,
            Icon = type.Icon,
            SortOrder = type.SortOrder,
            IsActive = type.IsActive,
            IsSystemManaged = AutomationTypeCodes.IsKnown(type.Code),
            CanChangeCode = false,
            CanDelete = false,
            CreatedAt = type.CreatedAt,
            UpdatedAt = type.UpdatedAt
        };
    }
}
