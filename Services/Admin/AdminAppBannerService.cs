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
    /// مدیریت بنرهای اپ — اسلات‌های سیستمی از seed؛ ویرایش تصویر/لینک فقط از پنل ادمین.
    /// </summary>
    public class AdminAppBannerService : IAdminAppBannerService
    {
        private readonly Api_Context _context;
        private readonly IAuditService _audit;
        private readonly IMemoryCache _cache;
        private readonly IFileUploadService _fileUploadService;
        private readonly ILogger<AdminAppBannerService> _logger;

        public AdminAppBannerService(
            Api_Context context,
            IAuditService audit,
            IMemoryCache cache,
            IFileUploadService fileUploadService,
            ILogger<AdminAppBannerService> logger)
        {
            _context = context;
            _audit = audit;
            _cache = cache;
            _fileUploadService = fileUploadService;
            _logger = logger;
        }

        public async Task<ApiResponse<List<AppBannerResponseDto>>> GetAllAsync(bool includeInactive = true)
        {
            try
            {
                _logger.LogInformation("شروع دریافت بنرهای اپ — IncludeInactive: {IncludeInactive}", includeInactive);

                var query = _context.AppBanners.AsNoTracking().Where(b => !b.IsDeleted);
                if (!includeInactive)
                    query = query.Where(b => b.IsActive);

                var banners = await query
                    .OrderBy(b => b.SortOrder)
                    .ThenBy(b => b.Id)
                    .Select(b => new AppBannerResponseDto
                    {
                        Id = b.Id,
                        Key = b.Key,
                        Title = b.Title,
                        Description = b.Description,
                        ImageUrl = b.ImageUrl,
                        LinkUrl = b.LinkUrl,
                        LinkType = b.LinkType,
                        SortOrder = b.SortOrder,
                        IsActive = b.IsActive,
                        CreatedAt = b.CreatedAt,
                        UpdatedAt = b.UpdatedAt
                    })
                    .ToListAsync();

                foreach (var banner in banners)
                    ApplyFlags(banner);

                _logger.LogInformation("پایان دریافت بنرهای اپ — Count: {Count}", banners.Count);
                return ApiResponse<List<AppBannerResponseDto>>.CreateSuccess(banners);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در دریافت بنرهای اپ");
                return ApiResponse<List<AppBannerResponseDto>>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<AppBannerResponseDto>> GetByIdAsync(int id)
        {
            try
            {
                _logger.LogInformation("شروع دریافت بنر اپ — Id: {Id}", id);

                var entity = await _context.AppBanners.AsNoTracking()
                    .FirstOrDefaultAsync(b => b.Id == id && !b.IsDeleted);

                if (entity == null)
                    return ApiResponse<AppBannerResponseDto>.NotFound("بنر یافت نشد");

                _logger.LogInformation("پایان دریافت بنر اپ — Id: {Id}", id);
                return ApiResponse<AppBannerResponseDto>.CreateSuccess(Map(entity));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در دریافت بنر اپ — Id: {Id}", id);
                return ApiResponse<AppBannerResponseDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<List<AppBannerResponseDto>>> GetActiveBannersAsync()
        {
            try
            {
                if (_cache.TryGetValue(AppBannerCacheKeys.ActiveList, out List<AppBannerResponseDto>? cached)
                    && cached != null)
                {
                    return ApiResponse<List<AppBannerResponseDto>>.CreateSuccess(cached);
                }

                _logger.LogInformation("شروع دریافت بنرهای فعال اپ");

                var banners = await _context.AppBanners.AsNoTracking()
                    .Where(b => !b.IsDeleted && b.IsActive && b.ImageUrl != null && b.ImageUrl != "")
                    .OrderBy(b => b.SortOrder)
                    .ThenBy(b => b.Id)
                    .Select(b => new AppBannerResponseDto
                    {
                        Id = b.Id,
                        Key = b.Key,
                        Title = b.Title,
                        Description = b.Description,
                        ImageUrl = b.ImageUrl,
                        LinkUrl = b.LinkUrl,
                        LinkType = b.LinkType,
                        SortOrder = b.SortOrder,
                        IsActive = b.IsActive,
                        CreatedAt = b.CreatedAt,
                        UpdatedAt = b.UpdatedAt
                    })
                    .ToListAsync();

                foreach (var banner in banners)
                    ApplyFlags(banner);

                _cache.Set(
                    AppBannerCacheKeys.ActiveList,
                    banners,
                    new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10),
                        Size = 1 // الزامی وقتی SizeLimit روی MemoryCache تنظیم شده
                    });

                _logger.LogInformation("پایان دریافت بنرهای فعال اپ — Count: {Count}", banners.Count);
                return ApiResponse<List<AppBannerResponseDto>>.CreateSuccess(banners);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در دریافت بنرهای فعال اپ");
                return ApiResponse<List<AppBannerResponseDto>>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<AppBannerResponseDto>> UpdateAsync(int id, UpdateAppBannerDto dto)
        {
            try
            {
                _logger.LogInformation("شروع به‌روزرسانی بنر اپ — Id: {Id}", id);

                var banner = await _context.AppBanners.FirstOrDefaultAsync(b => b.Id == id && !b.IsDeleted);
                if (banner == null)
                    return ApiResponse<AppBannerResponseDto>.NotFound("بنر یافت نشد");

                if (!AppBannerKeys.IsKnown(banner.Key))
                {
                    return ApiResponse<AppBannerResponseDto>.BadRequest(
                        "این بنر قابل ویرایش نیست",
                        errorCode: ErrorCodes.InvalidInput);
                }

                var title = (dto.Title ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(title))
                {
                    return ApiResponse<AppBannerResponseDto>.BadRequest(
                        "عنوان الزامی است",
                        errorCode: ErrorCodes.ValidationFailed);
                }
                if (title.Length > 200)
                {
                    return ApiResponse<AppBannerResponseDto>.BadRequest(
                        "عنوان نمی‌تواند بیشتر از ۲۰۰ کاراکتر باشد",
                        errorCode: ErrorCodes.ValidationFailed);
                }

                var description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim();
                if (description != null && description.Length > 1000)
                {
                    return ApiResponse<AppBannerResponseDto>.BadRequest(
                        "توضیحات نمی‌تواند بیشتر از ۱۰۰۰ کاراکتر باشد",
                        errorCode: ErrorCodes.ValidationFailed);
                }

                var linkType = string.IsNullOrWhiteSpace(dto.LinkType)
                    ? AppBannerLinkTypes.None
                    : dto.LinkType.Trim().ToLowerInvariant();
                if (!AppBannerLinkTypes.IsValid(linkType))
                {
                    return ApiResponse<AppBannerResponseDto>.BadRequest(
                        "نوع لینک معتبر نیست",
                        errorCode: ErrorCodes.ValidationFailed);
                }

                var linkUrl = string.IsNullOrWhiteSpace(dto.LinkUrl) ? null : dto.LinkUrl.Trim();
                var linkError = ValidateLink(linkType, linkUrl);
                if (linkError != null)
                {
                    return ApiResponse<AppBannerResponseDto>.BadRequest(
                        linkError,
                        errorCode: ErrorCodes.ValidationFailed);
                }

                var before = Snapshot(banner);
                var oldImage = banner.ImageUrl;
                var clearImage = dto.ClearImage == true;
                var isActive = dto.IsActive ?? true;

                banner.Title = title;
                banner.Description = description;
                banner.LinkType = linkType;
                banner.LinkUrl = linkType == AppBannerLinkTypes.None ? null : linkUrl;
                banner.SortOrder = dto.SortOrder ?? banner.SortOrder;
                banner.IsActive = isActive;
                banner.UpdatedAt = DateTime.UtcNow;

                if (clearImage)
                    banner.ImageUrl = null;

                await _context.SaveChangesAsync();

                if (clearImage && !string.IsNullOrWhiteSpace(oldImage))
                {
                    try
                    {
                        await _fileUploadService.DeleteFileAsync(
                            oldImage!,
                            FileUploadConstants.EntityType_AppBanner,
                            banner.Id,
                            FileUploadConstants.SubFolder_Images);
                    }
                    catch (Exception deleteEx)
                    {
                        _logger.LogWarning(deleteEx, "خطا در حذف تصویر قدیمی بنر — BannerId: {BannerId}", banner.Id);
                    }
                }

                InvalidateActiveCache();

                await _audit.WriteAsync(new AuditEntry
                {
                    Category = AuditCategories.Admin,
                    Action = AuditActions.AppBannerUpdated,
                    EntityType = AuditEntityTypes.AppBanner,
                    EntityId = banner.Id.ToString(),
                    Before = before,
                    After = Snapshot(banner)
                });

                _logger.LogInformation("پایان به‌روزرسانی بنر اپ — Id: {Id}, Key: {Key}", id, banner.Key);
                return ApiResponse<AppBannerResponseDto>.CreateSuccess(Map(banner), "بنر به‌روزرسانی شد");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در به‌روزرسانی بنر اپ — Id: {Id}", id);
                return ApiResponse<AppBannerResponseDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<AppBannerResponseDto>> UpdateImageAsync(int id, IFormFile? imageFile, bool clearImage)
        {
            try
            {
                _logger.LogInformation("شروع به‌روزرسانی تصویر بنر اپ — Id: {Id}", id);

                var banner = await _context.AppBanners.FirstOrDefaultAsync(b => b.Id == id && !b.IsDeleted);
                if (banner == null)
                    return ApiResponse<AppBannerResponseDto>.NotFound("بنر یافت نشد");

                if (!AppBannerKeys.IsKnown(banner.Key))
                {
                    return ApiResponse<AppBannerResponseDto>.BadRequest(
                        "این بنر قابل ویرایش نیست",
                        errorCode: ErrorCodes.InvalidInput);
                }

                var hasFile = imageFile != null && imageFile.Length > 0;
                if (!hasFile && !clearImage)
                {
                    return ApiResponse<AppBannerResponseDto>.BadRequest(
                        "فایل تصویر یا درخواست حذف تصویر الزامی است",
                        errorCode: ErrorCodes.ValidationFailed);
                }

                string? uploadedImagePath = null;
                if (hasFile)
                {
                    var validationError = SecureFileValidator.ValidateImage(
                        imageFile!,
                        SecureFileValidator.ProfileImageMaxBytes,
                        "۵ مگابایت");
                    if (!string.IsNullOrEmpty(validationError))
                    {
                        return ApiResponse<AppBannerResponseDto>.BadRequest(
                            validationError,
                            errorCode: ErrorCodes.ValidationFailed);
                    }

                    try
                    {
                        uploadedImagePath = await _fileUploadService.UploadFileAsync(
                            imageFile!,
                            FileUploadConstants.EntityType_AppBanner,
                            banner.Id,
                            FileUploadConstants.SubFolder_Images);
                    }
                    catch (ArgumentException ex)
                    {
                        _logger.LogWarning(ex, "اعتبارسنجی آپلود تصویر بنر ناموفق — Id: {Id}", id);
                        return ApiResponse<AppBannerResponseDto>.BadRequest(
                            ControlledErrorHelper.SanitizeArgumentMessage(ex.Message, ControlledErrorHelper.FileUploadFailed),
                            errorCode: ErrorCodes.ValidationFailed);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "خطا در آپلود تصویر بنر — Id: {Id}", id);
                        return ApiResponse<AppBannerResponseDto>.InternalServerError(
                            ControlledErrorHelper.FileUploadFailed,
                            ErrorCodes.FileUploadFailed);
                    }
                }

                var before = Snapshot(banner);
                var oldImage = banner.ImageUrl;
                banner.UpdatedAt = DateTime.UtcNow;

                if (uploadedImagePath != null)
                    banner.ImageUrl = uploadedImagePath;
                else if (clearImage)
                    banner.ImageUrl = null;

                await _context.SaveChangesAsync();

                if ((uploadedImagePath != null || clearImage) && !string.IsNullOrWhiteSpace(oldImage))
                {
                    try
                    {
                        await _fileUploadService.DeleteFileAsync(
                            oldImage!,
                            FileUploadConstants.EntityType_AppBanner,
                            banner.Id,
                            FileUploadConstants.SubFolder_Images);
                    }
                    catch (Exception deleteEx)
                    {
                        _logger.LogWarning(deleteEx, "خطا در حذف تصویر قدیمی بنر — BannerId: {BannerId}", banner.Id);
                    }
                }

                InvalidateActiveCache();

                await _audit.WriteAsync(new AuditEntry
                {
                    Category = AuditCategories.Admin,
                    Action = AuditActions.AppBannerUpdated,
                    EntityType = AuditEntityTypes.AppBanner,
                    EntityId = banner.Id.ToString(),
                    Before = before,
                    After = Snapshot(banner)
                });

                _logger.LogInformation("پایان به‌روزرسانی تصویر بنر اپ — Id: {Id}, Key: {Key}", id, banner.Key);
                return ApiResponse<AppBannerResponseDto>.CreateSuccess(Map(banner), "تصویر بنر به‌روزرسانی شد");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در به‌روزرسانی تصویر بنر اپ — Id: {Id}", id);
                return ApiResponse<AppBannerResponseDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        private void InvalidateActiveCache() => _cache.Remove(AppBannerCacheKeys.ActiveList);

        private static string? ValidateLink(string linkType, string? linkUrl)
        {
            if (linkType == AppBannerLinkTypes.None)
                return null;

            if (string.IsNullOrWhiteSpace(linkUrl))
                return "برای این نوع لینک، مقدار لینک الزامی است";

            if (linkType == AppBannerLinkTypes.ExternalUrl)
            {
                if (!Uri.TryCreate(linkUrl, UriKind.Absolute, out var uri)
                    || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                {
                    return "لینک خارجی باید با http یا https شروع شود";
                }
            }

            if (linkType == AppBannerLinkTypes.AppRoute)
            {
                if (linkUrl.Contains(' '))
                    return "کلید مسیر اپ نباید فاصله داشته باشد";
                if (!linkUrl.StartsWith('/'))
                    return "مسیر داخل اپ باید با / شروع شود";
            }

            return null;
        }

        private static AppBannerResponseDto Map(AppBanner banner)
        {
            var dto = new AppBannerResponseDto
            {
                Id = banner.Id,
                Key = banner.Key,
                Title = banner.Title,
                Description = banner.Description,
                ImageUrl = banner.ImageUrl,
                LinkUrl = banner.LinkUrl,
                LinkType = banner.LinkType,
                SortOrder = banner.SortOrder,
                IsActive = banner.IsActive,
                CreatedAt = banner.CreatedAt,
                UpdatedAt = banner.UpdatedAt
            };
            ApplyFlags(dto);
            return dto;
        }

        private static void ApplyFlags(AppBannerResponseDto banner)
        {
            banner.IsSystemManaged = AppBannerKeys.IsKnown(banner.Key);
            banner.CanDelete = false;
        }

        private static object Snapshot(AppBanner banner) => new
        {
            banner.Id,
            banner.Key,
            banner.Title,
            banner.Description,
            banner.ImageUrl,
            banner.LinkUrl,
            banner.LinkType,
            banner.SortOrder,
            banner.IsActive
        };
    }
}
