using Api_Vapp.Constants;
using Api_Vapp.Data;
using Api_Vapp.DTOs.Admin;
using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.File;
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
        private readonly IFileUploadService _fileUploadService;
        private readonly ILogger<AdminAutomationTypeService> _logger;

        public AdminAutomationTypeService(
            Api_Context context,
            IAuditService audit,
            IMemoryCache cache,
            IFileUploadService fileUploadService,
            ILogger<AdminAutomationTypeService> logger)
        {
            _context = context;
            _audit = audit;
            _cache = cache;
            _fileUploadService = fileUploadService;
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
                    ApplyFlags(type);
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

                ApplyFlags(type);

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

                string? uploadedIconPath = null;
                if (dto.IconFile != null && dto.IconFile.Length > 0)
                {
                    var validationError = SecureFileValidator.ValidateImage(
                        dto.IconFile,
                        SecureFileValidator.IconMaxBytes,
                        "۲ مگابایت");
                    if (!string.IsNullOrEmpty(validationError))
                    {
                        return ApiResponse<AutomationTypeAdminResponseDto>.BadRequest(
                            validationError,
                            errorCode: ErrorCodes.ValidationFailed);
                    }

                    try
                    {
                        uploadedIconPath = await _fileUploadService.UploadFileAsync(
                            dto.IconFile,
                            FileUploadConstants.EntityType_AutomationType,
                            type.Id,
                            FileUploadConstants.SubFolder_Images);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "خطا در آپلود آیکون نوع پیام خودکار — Id: {Id}", id);
                        return ApiResponse<AutomationTypeAdminResponseDto>.InternalServerError(
                            ControlledErrorHelper.FileUploadFailed);
                    }
                }

                var before = Snapshot(type);
                var oldIcon = type.Icon;

                type.Name = name;
                type.Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim();
                type.SortOrder = dto.SortOrder;
                type.IsActive = dto.IsActive;
                type.UpdatedAt = DateTime.UtcNow;

                if (uploadedIconPath != null)
                {
                    type.Icon = uploadedIconPath;
                }
                else if (dto.ClearIcon)
                {
                    type.Icon = null;
                }

                await _context.SaveChangesAsync();

                if ((uploadedIconPath != null || dto.ClearIcon) && IsUploadedIconPath(oldIcon))
                {
                    try
                    {
                        await _fileUploadService.DeleteFileAsync(
                            oldIcon!,
                            FileUploadConstants.EntityType_AutomationType,
                            type.Id,
                            FileUploadConstants.SubFolder_Images);
                    }
                    catch (Exception deleteEx)
                    {
                        _logger.LogWarning(deleteEx, "خطا در حذف آیکون قدیمی نوع پیام خودکار — Path: {Path}", oldIcon);
                    }
                }

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

        public async Task<ApiResponse<bool>> DeleteAsync(int id)
        {
            try
            {
                _logger.LogInformation("شروع حذف نوع پیام خودکار — Id: {Id}", id);

                var type = await _context.AutomationTypes.FirstOrDefaultAsync(t => t.Id == id && !t.IsDeleted);
                if (type == null)
                    return ApiResponse<bool>.NotFound("نوع پیام خودکار یافت نشد");

                var before = Snapshot(type);
                var oldIcon = type.Icon;

                type.IsDeleted = true;
                type.IsActive = false;
                type.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                if (IsUploadedIconPath(oldIcon))
                {
                    try
                    {
                        await _fileUploadService.DeleteFileAsync(
                            oldIcon!,
                            FileUploadConstants.EntityType_AutomationType,
                            type.Id,
                            FileUploadConstants.SubFolder_Images);
                    }
                    catch (Exception deleteEx)
                    {
                        _logger.LogWarning(deleteEx, "خطا در حذف آیکون نوع پیام خودکار — Path: {Path}", oldIcon);
                    }
                }

                _cache.Remove(AutomationTypeCacheKeys.ActiveList);

                await _audit.WriteAsync(new AuditEntry
                {
                    Category = AuditCategories.Admin,
                    Action = AuditActions.AutomationTypeDeleted,
                    EntityType = AuditEntityTypes.AutomationType,
                    EntityId = type.Id.ToString(),
                    Before = before,
                    After = new { type.Id, type.Code, isDeleted = true, isActive = false }
                });

                _logger.LogInformation("پایان حذف نوع پیام خودکار — Id: {Id}, Code: {Code}", id, type.Code);
                return ApiResponse<bool>.CreateSuccess(true, "نوع پیام خودکار حذف شد");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در حذف نوع پیام خودکار — Id: {Id}", id);
                return ApiResponse<bool>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        private static void ApplyFlags(AutomationTypeAdminResponseDto type)
        {
            type.IsSystemManaged = AutomationTypeCodes.IsKnown(type.Code);
            type.CanChangeCode = false;
            type.CanDelete = true;
        }

        private static bool IsUploadedIconPath(string? icon)
        {
            if (string.IsNullOrWhiteSpace(icon))
                return false;

            return icon.Contains('/') || icon.Contains('\\')
                || icon.Contains("uploads", StringComparison.OrdinalIgnoreCase);
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

        private static AutomationTypeAdminResponseDto Map(AutomationTypeDefinition type)
        {
            var dto = new AutomationTypeAdminResponseDto
            {
                Id = type.Id,
                Code = type.Code,
                Name = type.Name,
                Description = type.Description,
                Icon = type.Icon,
                SortOrder = type.SortOrder,
                IsActive = type.IsActive,
                CreatedAt = type.CreatedAt,
                UpdatedAt = type.UpdatedAt
            };
            ApplyFlags(dto);
            return dto;
        }
    }
}
