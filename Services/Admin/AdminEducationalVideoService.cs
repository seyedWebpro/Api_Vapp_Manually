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
    public class AdminEducationalVideoService : IAdminEducationalVideoService
    {
        private const string PendingVideoUrl = "pending";

        private readonly Api_Context _context;
        private readonly IAuditService _audit;
        private readonly IUserPushNotifier _pushNotifier;
        private readonly IFileUploadService _fileUploadService;
        private readonly IMemoryCache _cache;
        private readonly ILogger<AdminEducationalVideoService> _logger;

        public AdminEducationalVideoService(
            Api_Context context,
            IAuditService audit,
            IUserPushNotifier pushNotifier,
            IFileUploadService fileUploadService,
            IMemoryCache cache,
            ILogger<AdminEducationalVideoService> logger)
        {
            _context = context;
            _audit = audit;
            _pushNotifier = pushNotifier;
            _fileUploadService = fileUploadService;
            _cache = cache;
            _logger = logger;
        }

        public async Task<ApiResponse<List<EducationalVideoResponseDto>>> GetAllAsync(bool includeInactive = true)
        {
            _logger.LogInformation("شروع دریافت ویدیوهای آموزشی — IncludeInactive: {IncludeInactive}", includeInactive);

            var query = _context.EducationalVideos.AsNoTracking()
                .Where(v => !v.IsDeleted && v.VideoUrl != PendingVideoUrl);
            if (!includeInactive)
                query = query.Where(v => v.IsActive);

            var videos = await query
                .OrderBy(v => v.SortOrder)
                .ThenByDescending(v => v.CreatedAt)
                .Select(v => new EducationalVideoResponseDto
                {
                    Id = v.Id,
                    Title = v.Title,
                    Description = v.Description,
                    VideoUrl = v.VideoUrl,
                    ThumbnailUrl = v.ThumbnailUrl,
                    SortOrder = v.SortOrder,
                    IsActive = v.IsActive,
                    CreatedAt = v.CreatedAt,
                    UpdatedAt = v.UpdatedAt
                })
                .ToListAsync();

            _logger.LogInformation("پایان دریافت ویدیوهای آموزشی — Count: {Count}", videos.Count);
            return ApiResponse<List<EducationalVideoResponseDto>>.CreateSuccess(videos);
        }

        public async Task<ApiResponse<EducationalVideoResponseDto>> GetByIdAsync(int id)
        {
            _logger.LogInformation("شروع دریافت ویدیو آموزشی — Id: {Id}", id);

            var video = await _context.EducationalVideos.AsNoTracking()
                .Where(v => v.Id == id && !v.IsDeleted && v.VideoUrl != PendingVideoUrl)
                .Select(v => new EducationalVideoResponseDto
                {
                    Id = v.Id,
                    Title = v.Title,
                    Description = v.Description,
                    VideoUrl = v.VideoUrl,
                    ThumbnailUrl = v.ThumbnailUrl,
                    SortOrder = v.SortOrder,
                    IsActive = v.IsActive,
                    CreatedAt = v.CreatedAt,
                    UpdatedAt = v.UpdatedAt
                })
                .FirstOrDefaultAsync();

            if (video == null)
                return ApiResponse<EducationalVideoResponseDto>.NotFound("ویدیو یافت نشد");

            _logger.LogInformation("پایان دریافت ویدیو آموزشی — Id: {Id}", id);
            return ApiResponse<EducationalVideoResponseDto>.CreateSuccess(video);
        }

        public async Task<ApiResponse<EducationalVideoResponseDto>> CreateAsync(
            CreateEducationalVideoDto dto,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("شروع ایجاد ویدیو آموزشی — Title: {Title}", dto.Title);

            var hasFile = dto.VideoFile != null && dto.VideoFile.Length > 0;
            var videoUrl = NormalizeOptionalUrl(dto.VideoUrl, "لینک ویدیو", allowEmpty: true);
            if (videoUrl.Error != null)
                return videoUrl.Error;

            var thumbnailUrl = NormalizeOptionalUrl(dto.ThumbnailUrl, "لینک تصویر بندانگشتی", allowEmpty: true);
            if (thumbnailUrl.Error != null)
                return thumbnailUrl.Error;

            if (!hasFile && string.IsNullOrWhiteSpace(videoUrl.Value))
            {
                return ApiResponse<EducationalVideoResponseDto>.BadRequest(
                    "فایل ویدیو یا لینک ویدیو الزامی است",
                    errorCode: ErrorCodes.ValidationFailed);
            }

            if (hasFile)
            {
                var validationError = SecureFileValidator.ValidateVideo(dto.VideoFile);
                if (!string.IsNullOrEmpty(validationError))
                {
                    return ApiResponse<EducationalVideoResponseDto>.BadRequest(
                        validationError,
                        errorCode: ErrorCodes.ValidationFailed);
                }
            }

            // تا پایان آپلود در لیست فعال/عمومی دیده نشود
            var requestedActive = dto.IsActive;
            var video = new EducationalVideo
            {
                Title = dto.Title.Trim(),
                Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim(),
                VideoUrl = hasFile ? PendingVideoUrl : videoUrl.Value!,
                ThumbnailUrl = thumbnailUrl.Value,
                SortOrder = dto.SortOrder,
                IsActive = hasFile ? false : requestedActive,
                CreatedAt = DateTime.UtcNow
            };

            _context.EducationalVideos.Add(video);
            await _context.SaveChangesAsync();

            string? uploadedPath = null;
            if (hasFile)
            {
                try
                {
                    uploadedPath = await _fileUploadService.UploadFileAsync(
                        dto.VideoFile!,
                        FileUploadConstants.EntityType_EducationalVideo,
                        video.Id,
                        FileUploadConstants.SubFolder_Videos,
                        cancellationToken);
                    video.VideoUrl = uploadedPath;
                    video.IsActive = requestedActive;
                    video.UpdatedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync(cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    await RollbackCreateAsync(video, uploadedPath);
                    throw;
                }
                catch (ArgumentException ex)
                {
                    await RollbackCreateAsync(video, uploadedPath);
                    return ApiResponse<EducationalVideoResponseDto>.BadRequest(
                        ControlledErrorHelper.SanitizeArgumentMessage(ex.Message, ControlledErrorHelper.FileUploadFailed),
                        errorCode: ErrorCodes.ValidationFailed);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "خطا در آپلود فایل ویدیو آموزشی — Id: {Id}", video.Id);
                    await RollbackCreateAsync(video, uploadedPath);
                    return ApiResponse<EducationalVideoResponseDto>.InternalServerError(
                        ControlledErrorHelper.FileUploadFailed);
                }
            }

            InvalidateActiveCache();

            await _audit.WriteAsync(new AuditEntry
            {
                Category = AuditCategories.Admin,
                Action = AuditActions.EducationalVideoCreated,
                EntityType = AuditEntityTypes.EducationalVideo,
                EntityId = video.Id.ToString(),
                After = Snapshot(video)
            });

            if (video.IsActive)
            {
                var tipPush = PushNotificationCopy.EducationTip(video.Title);
                await _pushNotifier.NotifyBroadcastAsync(
                    NotificationCategory.EducationAndTips,
                    tipPush.Title,
                    tipPush.Body);
            }

            _logger.LogInformation("پایان ایجاد ویدیو آموزشی — Id: {Id}", video.Id);
            return ApiResponse<EducationalVideoResponseDto>.CreateSuccess(Map(video), "ویدیو ایجاد شد", 201);
        }

        public async Task<ApiResponse<EducationalVideoResponseDto>> UpdateAsync(
            int id,
            UpdateEducationalVideoDto dto,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("شروع به‌روزرسانی ویدیو آموزشی — Id: {Id}", id);

            var video = await _context.EducationalVideos.FirstOrDefaultAsync(v => v.Id == id && !v.IsDeleted);
            if (video == null)
                return ApiResponse<EducationalVideoResponseDto>.NotFound("ویدیو یافت نشد");

            var hasFile = dto.VideoFile != null && dto.VideoFile.Length > 0;
            var videoUrl = NormalizeOptionalUrl(dto.VideoUrl, "لینک ویدیو", allowEmpty: true);
            if (videoUrl.Error != null)
                return videoUrl.Error;

            var thumbnailUrl = NormalizeOptionalUrl(dto.ThumbnailUrl, "لینک تصویر بندانگشتی", allowEmpty: true);
            if (thumbnailUrl.Error != null)
                return thumbnailUrl.Error;

            if (!hasFile && string.IsNullOrWhiteSpace(videoUrl.Value))
            {
                return ApiResponse<EducationalVideoResponseDto>.BadRequest(
                    "فایل ویدیو یا لینک ویدیو الزامی است",
                    errorCode: ErrorCodes.ValidationFailed);
            }

            string? uploadedPath = null;
            if (hasFile)
            {
                var validationError = SecureFileValidator.ValidateVideo(dto.VideoFile);
                if (!string.IsNullOrEmpty(validationError))
                {
                    return ApiResponse<EducationalVideoResponseDto>.BadRequest(
                        validationError,
                        errorCode: ErrorCodes.ValidationFailed);
                }

                try
                {
                    uploadedPath = await _fileUploadService.UploadFileAsync(
                        dto.VideoFile!,
                        FileUploadConstants.EntityType_EducationalVideo,
                        video.Id,
                        FileUploadConstants.SubFolder_Videos,
                        cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (ArgumentException ex)
                {
                    return ApiResponse<EducationalVideoResponseDto>.BadRequest(
                        ControlledErrorHelper.SanitizeArgumentMessage(ex.Message, ControlledErrorHelper.FileUploadFailed),
                        errorCode: ErrorCodes.ValidationFailed);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "خطا در آپلود فایل ویدیو آموزشی — Id: {Id}", id);
                    return ApiResponse<EducationalVideoResponseDto>.InternalServerError(
                        ControlledErrorHelper.FileUploadFailed);
                }
            }

            var before = Snapshot(video);
            var wasActive = video.IsActive;
            var oldVideoUrl = video.VideoUrl;
            var nextVideoUrl = uploadedPath ?? videoUrl.Value!;

            video.Title = dto.Title.Trim();
            video.Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim();
            video.VideoUrl = nextVideoUrl;
            video.ThumbnailUrl = thumbnailUrl.Value;
            video.SortOrder = dto.SortOrder;
            video.IsActive = dto.IsActive;
            video.UpdatedAt = DateTime.UtcNow;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در ذخیره به‌روزرسانی ویدیو آموزشی — Id: {Id}", id);
                if (uploadedPath != null)
                    await TryDeleteUploadedFileAsync(uploadedPath, video.Id);
                return ApiResponse<EducationalVideoResponseDto>.InternalServerError(
                    ControlledErrorHelper.Database);
            }

            // حذف فایل قدیمی هنگام جایگزینی با فایل جدید یا لینک خارجی
            if (IsUploadedVideoPath(oldVideoUrl) &&
                !string.Equals(oldVideoUrl, nextVideoUrl, StringComparison.OrdinalIgnoreCase))
            {
                await TryDeleteUploadedFileAsync(oldVideoUrl, video.Id);
            }

            InvalidateActiveCache();

            await _audit.WriteAsync(new AuditEntry
            {
                Category = AuditCategories.Admin,
                Action = AuditActions.EducationalVideoUpdated,
                EntityType = AuditEntityTypes.EducationalVideo,
                EntityId = video.Id.ToString(),
                Before = before,
                After = Snapshot(video)
            });

            if (video.IsActive && !wasActive)
            {
                var tipPush = PushNotificationCopy.EducationTip(video.Title);
                await _pushNotifier.NotifyBroadcastAsync(
                    NotificationCategory.EducationAndTips,
                    tipPush.Title,
                    tipPush.Body);
            }

            _logger.LogInformation("پایان به‌روزرسانی ویدیو آموزشی — Id: {Id}", id);
            return ApiResponse<EducationalVideoResponseDto>.CreateSuccess(Map(video), "ویدیو به‌روزرسانی شد");
        }

        public async Task<ApiResponse<bool>> DeleteAsync(int id)
        {
            _logger.LogInformation("شروع حذف ویدیو آموزشی — Id: {Id}", id);

            var video = await _context.EducationalVideos.FirstOrDefaultAsync(v => v.Id == id && !v.IsDeleted);
            if (video == null)
                return ApiResponse<bool>.NotFound("ویدیو یافت نشد");

            var before = Snapshot(video);
            var oldVideoUrl = video.VideoUrl;

            video.IsDeleted = true;
            video.IsActive = false;
            video.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            if (IsUploadedVideoPath(oldVideoUrl))
                await TryDeleteUploadedFileAsync(oldVideoUrl, video.Id);

            // پاکسازی پوشه موجودیت در صورت باقی‌ماندن فایل‌های یتیم
            try
            {
                await _fileUploadService.DeleteAllEntityFilesAsync(
                    FileUploadConstants.EntityType_EducationalVideo,
                    video.Id,
                    FileUploadConstants.SubFolder_Videos);
            }
            catch (Exception deleteEx)
            {
                _logger.LogWarning(deleteEx, "خطا در پاکسازی پوشه فایل‌های ویدیو — Id: {Id}", id);
            }

            InvalidateActiveCache();

            await _audit.WriteAsync(new AuditEntry
            {
                Category = AuditCategories.Admin,
                Action = AuditActions.EducationalVideoDeleted,
                EntityType = AuditEntityTypes.EducationalVideo,
                EntityId = video.Id.ToString(),
                Before = before,
                After = new { video.Id, isDeleted = true, isActive = false }
            });

            _logger.LogInformation("پایان حذف ویدیو آموزشی — Id: {Id}", id);
            return ApiResponse<bool>.CreateSuccess(true, "ویدیو حذف شد");
        }

        public async Task<ApiResponse<List<EducationalVideoResponseDto>>> GetActiveVideosAsync()
        {
            if (_cache.TryGetValue(EducationalVideoCacheKeys.ActiveList, out List<EducationalVideoResponseDto>? cached)
                && cached != null)
            {
                return ApiResponse<List<EducationalVideoResponseDto>>.CreateSuccess(cached);
            }

            var videos = await _context.EducationalVideos.AsNoTracking()
                .Where(v => v.IsActive && !v.IsDeleted && v.VideoUrl != PendingVideoUrl && v.VideoUrl != "")
                .OrderBy(v => v.SortOrder)
                .Select(v => new EducationalVideoResponseDto
                {
                    Id = v.Id,
                    Title = v.Title,
                    Description = v.Description,
                    VideoUrl = v.VideoUrl,
                    ThumbnailUrl = v.ThumbnailUrl,
                    SortOrder = v.SortOrder,
                    IsActive = v.IsActive,
                    CreatedAt = v.CreatedAt,
                    UpdatedAt = v.UpdatedAt
                })
                .ToListAsync();

            _cache.Set(
                EducationalVideoCacheKeys.ActiveList,
                videos,
                new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10),
                    SlidingExpiration = TimeSpan.FromMinutes(3)
                });

            return ApiResponse<List<EducationalVideoResponseDto>>.CreateSuccess(videos);
        }

        private async Task RollbackCreateAsync(EducationalVideo video, string? uploadedPath)
        {
            if (!string.IsNullOrWhiteSpace(uploadedPath))
                await TryDeleteUploadedFileAsync(uploadedPath, video.Id);

            try
            {
                await _fileUploadService.DeleteAllEntityFilesAsync(
                    FileUploadConstants.EntityType_EducationalVideo,
                    video.Id,
                    FileUploadConstants.SubFolder_Videos);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "خطا در پاکسازی فایل‌ها هنگام Rollback ایجاد ویدیو — Id: {Id}", video.Id);
            }

            try
            {
                _context.EducationalVideos.Remove(video);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "خطا در حذف رکورد موقت ویدیو — Id: {Id}", video.Id);
                // Soft-delete fallback if hard remove fails
                video.IsDeleted = true;
                video.IsActive = false;
                video.VideoUrl = PendingVideoUrl;
                video.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
        }

        private async Task TryDeleteUploadedFileAsync(string path, int videoId)
        {
            try
            {
                await _fileUploadService.DeleteFileAsync(
                    path,
                    FileUploadConstants.EntityType_EducationalVideo,
                    videoId,
                    FileUploadConstants.SubFolder_Videos);
            }
            catch (Exception deleteEx)
            {
                _logger.LogWarning(deleteEx, "خطا در حذف فایل ویدیو — Path: {Path}", path);
            }
        }

        private void InvalidateActiveCache() => _cache.Remove(EducationalVideoCacheKeys.ActiveList);

        private static (string? Value, ApiResponse<EducationalVideoResponseDto>? Error) NormalizeOptionalUrl(
            string? raw,
            string fieldLabelFa,
            bool allowEmpty = false)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                if (allowEmpty) return (null, null);
                return (null, ApiResponse<EducationalVideoResponseDto>.BadRequest(
                    $"{fieldLabelFa} الزامی است",
                    errorCode: ErrorCodes.ValidationFailed));
            }

            var trimmed = raw.Trim();

            // مسیر آپلود داخلی مجاز است
            if (IsUploadedVideoPath(trimmed))
                return (trimmed, null);

            if (!IsHttpOrHttpsUrl(trimmed))
            {
                return (null, ApiResponse<EducationalVideoResponseDto>.BadRequest(
                    $"{fieldLabelFa} باید با http یا https شروع شود",
                    errorCode: ErrorCodes.ValidationFailed));
            }

            return (trimmed, null);
        }

        private static bool IsHttpOrHttpsUrl(string url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
                return false;
            return uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps;
        }

        private static bool IsUploadedVideoPath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            var normalized = path.Replace("\\", "/").TrimStart('/');
            return normalized.StartsWith("uploads/", StringComparison.OrdinalIgnoreCase);
        }

        private static object Snapshot(EducationalVideo video) => new
        {
            video.Id,
            video.Title,
            video.VideoUrl,
            video.ThumbnailUrl,
            video.SortOrder,
            video.IsActive
        };

        private static EducationalVideoResponseDto Map(EducationalVideo video) => new()
        {
            Id = video.Id,
            Title = video.Title,
            Description = video.Description,
            VideoUrl = video.VideoUrl,
            ThumbnailUrl = video.ThumbnailUrl,
            SortOrder = video.SortOrder,
            IsActive = video.IsActive,
            CreatedAt = video.CreatedAt,
            UpdatedAt = video.UpdatedAt
        };
    }
}
