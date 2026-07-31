using Api_Vapp.Constants;
using Api_Vapp.Data;
using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.Message;
using Api_Vapp.DTOs.SocialMediaLink;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;
using Api_Vapp.Services.Audit;
using Api_Vapp.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Api_Vapp.Services
{
    /// <summary>
    /// سرویس مدیریت لینک‌های شبکه‌های اجتماعی
    /// </summary>
    public class SocialMediaLinkService : ISocialMediaLinkService
    {
        private static readonly TimeSpan ListCacheDuration = TimeSpan.FromMinutes(5);

        private readonly ISocialMediaLinkRepository _linkRepository;
        private readonly IContactRepository _contactRepository;
        private readonly IContactNotebookRepository _notebookRepository;
        private readonly IMessageService _messageService;
        private readonly Api_Context _context;
        private readonly IAuditService _audit;
        private readonly IMemoryCache _cache;
        private readonly ILogger<SocialMediaLinkService> _logger;

        public SocialMediaLinkService(
            ISocialMediaLinkRepository linkRepository,
            IContactRepository contactRepository,
            IContactNotebookRepository notebookRepository,
            IMessageService messageService,
            Api_Context context,
            IAuditService audit,
            IMemoryCache cache,
            ILogger<SocialMediaLinkService> logger)
        {
            _linkRepository = linkRepository;
            _contactRepository = contactRepository;
            _notebookRepository = notebookRepository;
            _messageService = messageService;
            _context = context;
            _audit = audit;
            _cache = cache;
            _logger = logger;
        }

        public async Task<ApiResponse<SocialMediaLinkResponseDto>> CreateSocialMediaLinkAsync(
            int userId,
            CreateSocialMediaLinkDto createDto)
        {
            _logger.LogInformation("شروع ایجاد لینک سوشیال — UserId: {UserId}", userId);

            try
            {
                var platform = NormalizePlatform(createDto.Platform);
                var linkUrl = NormalizeLinkUrl(createDto.LinkUrl);

                var urlError = ValidateLinkUrl(linkUrl);
                if (urlError != null)
                    return ApiResponse<SocialMediaLinkResponseDto>.BadRequest(urlError, errorCode: ErrorCodes.InvalidInput);

                if (string.IsNullOrWhiteSpace(platform))
                    return ApiResponse<SocialMediaLinkResponseDto>.BadRequest(
                        "نوع پلتفرم الزامی است",
                        errorCode: ErrorCodes.InvalidInput);

                var userExists = await _context.Users.AsNoTracking()
                    .AnyAsync(u => u.Id == userId && !u.IsDeleted);
                if (!userExists)
                    return ApiResponse<SocialMediaLinkResponseDto>.NotFound("کاربر یافت نشد");

                var activeCount = await _linkRepository.CountActiveByUserIdAsync(userId);
                var setAsDefault = createDto.IsDefault == true || activeCount == 0;

                await using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    if (setAsDefault)
                        await UnsetDefaultsAsync(userId);

                    var entity = new SocialMediaLink
                    {
                        UserId = userId,
                        Platform = platform,
                        LinkUrl = linkUrl,
                        IsDefault = setAsDefault,
                        IsActive = true,
                        IsDeleted = false,
                        CreatedAt = DateTime.UtcNow
                    };

                    await _linkRepository.AddAsync(entity);
                    await transaction.CommitAsync();

                    InvalidateUserCache(userId);

                    await _audit.WriteAsync(new AuditEntry
                    {
                        Category = AuditCategories.Message,
                        Action = AuditActions.SocialMediaLinkCreated,
                        EntityType = AuditEntityTypes.SocialMediaLink,
                        EntityId = entity.Id.ToString(),
                        ActorUserId = userId,
                        After = new { entity.Platform, entity.LinkUrl, entity.IsDefault }
                    });

                    _logger.LogInformation("لینک سوشیال ایجاد شد — Id: {Id}, UserId: {UserId}", entity.Id, userId);

                    return ApiResponse<SocialMediaLinkResponseDto>.CreateSuccess(
                        MapToDto(entity),
                        "لینک با موفقیت ایجاد شد",
                        201);
                }
                catch (Exception)
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در ایجاد لینک سوشیال — UserId: {UserId}", userId);
                return ApiResponse<SocialMediaLinkResponseDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<SocialMediaLinkListResponseDto>> GetSocialMediaLinksAsync(
            int userId,
            int pageNumber = 1,
            int pageSize = 10)
        {
            try
            {
                if (pageNumber < 1) pageNumber = 1;
                if (pageSize < 1 || pageSize > 100) pageSize = 10;

                var cacheKey = BuildListCacheKey(userId, pageNumber, pageSize);
                if (_cache.TryGetValue(cacheKey, out SocialMediaLinkListResponseDto? cached) && cached != null)
                    return ApiResponse<SocialMediaLinkListResponseDto>.CreateSuccess(cached);

                var (items, totalCount) = await _linkRepository.GetPagedByUserIdAsync(userId, pageNumber, pageSize);
                var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);

                var response = new SocialMediaLinkListResponseDto
                {
                    SocialMediaLinks = items.Select(MapToDto).ToList(),
                    TotalCount = totalCount,
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    TotalPages = totalPages
                };

                _cache.Set(
                    cacheKey,
                    response,
                    new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = ListCacheDuration,
                        Size = 1
                    });
                return ApiResponse<SocialMediaLinkListResponseDto>.CreateSuccess(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در دریافت لیست لینک سوشیال — UserId: {UserId}", userId);
                return ApiResponse<SocialMediaLinkListResponseDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<SocialMediaLinkResponseDto>> GetSocialMediaLinkByIdAsync(int id, int userId)
        {
            try
            {
                var link = await _linkRepository.GetOwnedByIdAsync(id, userId, asNoTracking: true);
                if (link == null)
                    return ApiResponse<SocialMediaLinkResponseDto>.NotFound("لینک مورد نظر یافت نشد");

                return ApiResponse<SocialMediaLinkResponseDto>.CreateSuccess(MapToDto(link));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در دریافت لینک سوشیال — Id: {Id}", id);
                return ApiResponse<SocialMediaLinkResponseDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<SocialMediaLinkResponseDto>> UpdateSocialMediaLinkAsync(
            int id,
            int userId,
            UpdateSocialMediaLinkDto updateDto)
        {
            _logger.LogInformation("شروع به‌روزرسانی لینک سوشیال — Id: {Id}, UserId: {UserId}", id, userId);

            try
            {
                var link = await _linkRepository.GetOwnedByIdAsync(id, userId, asNoTracking: false);
                if (link == null)
                    return ApiResponse<SocialMediaLinkResponseDto>.NotFound("لینک مورد نظر یافت نشد");

                if (updateDto.Platform != null)
                {
                    var platform = NormalizePlatform(updateDto.Platform);
                    if (string.IsNullOrWhiteSpace(platform))
                        return ApiResponse<SocialMediaLinkResponseDto>.BadRequest(
                            "نوع پلتفرم نامعتبر است",
                            errorCode: ErrorCodes.InvalidInput);
                    link.Platform = platform;
                }

                if (updateDto.LinkUrl != null)
                {
                    var linkUrl = NormalizeLinkUrl(updateDto.LinkUrl);
                    var urlError = ValidateLinkUrl(linkUrl);
                    if (urlError != null)
                        return ApiResponse<SocialMediaLinkResponseDto>.BadRequest(urlError, errorCode: ErrorCodes.InvalidInput);
                    link.LinkUrl = linkUrl;
                }

                if (updateDto.IsActive.HasValue)
                {
                    if (!updateDto.IsActive.Value && link.IsDefault)
                        return ApiResponse<SocialMediaLinkResponseDto>.BadRequest(
                            "لینک پیش‌فرض را نمی‌توان غیرفعال کرد. ابتدا لینک دیگری را پیش‌فرض کنید",
                            errorCode: ErrorCodes.InvalidInput);

                    link.IsActive = updateDto.IsActive.Value;
                }

                link.UpdatedAt = DateTime.UtcNow;
                await _linkRepository.UpdateAsync(link);
                InvalidateUserCache(userId);

                await _audit.WriteAsync(new AuditEntry
                {
                    Category = AuditCategories.Message,
                    Action = AuditActions.SocialMediaLinkUpdated,
                    EntityType = AuditEntityTypes.SocialMediaLink,
                    EntityId = link.Id.ToString(),
                    ActorUserId = userId,
                    After = new { link.Platform, link.LinkUrl, link.IsActive, link.IsDefault }
                });

                _logger.LogInformation("لینک سوشیال به‌روزرسانی شد — Id: {Id}", id);
                return ApiResponse<SocialMediaLinkResponseDto>.CreateSuccess(MapToDto(link), "لینک با موفقیت به‌روزرسانی شد");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در به‌روزرسانی لینک سوشیال — Id: {Id}", id);
                return ApiResponse<SocialMediaLinkResponseDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<bool>> DeleteSocialMediaLinkAsync(int id, int userId)
        {
            _logger.LogInformation("شروع حذف لینک سوشیال — Id: {Id}, UserId: {UserId}", id, userId);

            try
            {
                var link = await _linkRepository.GetOwnedByIdAsync(id, userId, asNoTracking: false);
                if (link == null)
                    return ApiResponse<bool>.NotFound("لینک مورد نظر یافت نشد");

                await using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    var wasDefault = link.IsDefault;
                    link.IsDeleted = true;
                    link.IsDefault = false;
                    link.IsActive = false;
                    link.UpdatedAt = DateTime.UtcNow;
                    await _linkRepository.UpdateAsync(link);

                    if (wasDefault)
                    {
                        var nextDefault = await _context.SocialMediaLinks
                            .Where(s => s.UserId == userId && !s.IsDeleted && s.IsActive && s.Id != id)
                            .OrderByDescending(s => s.CreatedAt)
                            .FirstOrDefaultAsync();

                        if (nextDefault != null)
                        {
                            nextDefault.IsDefault = true;
                            nextDefault.UpdatedAt = DateTime.UtcNow;
                        }
                    }

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }

                InvalidateUserCache(userId);

                await _audit.WriteAsync(new AuditEntry
                {
                    Category = AuditCategories.Message,
                    Action = AuditActions.SocialMediaLinkDeleted,
                    EntityType = AuditEntityTypes.SocialMediaLink,
                    EntityId = id.ToString(),
                    ActorUserId = userId
                });

                _logger.LogInformation("لینک سوشیال حذف شد — Id: {Id}", id);
                return ApiResponse<bool>.CreateSuccess(true, "لینک با موفقیت حذف شد");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در حذف لینک سوشیال — Id: {Id}", id);
                return ApiResponse<bool>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<SocialMediaLinkResponseDto>> SetUserDefaultSocialMediaLinkAsync(int userId, int linkId)
        {
            _logger.LogInformation("تنظیم لینک پیش‌فرض سوشیال — LinkId: {LinkId}, UserId: {UserId}", linkId, userId);

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var link = await _context.SocialMediaLinks
                    .FirstOrDefaultAsync(s =>
                        s.Id == linkId &&
                        s.UserId == userId &&
                        s.IsActive &&
                        !s.IsDeleted);

                if (link == null)
                {
                    await transaction.RollbackAsync();
                    return ApiResponse<SocialMediaLinkResponseDto>.NotFound("لینک مورد نظر یافت نشد یا فعال نیست");
                }

                await UnsetDefaultsAsync(userId, exceptId: linkId);

                link.IsDefault = true;
                link.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                InvalidateUserCache(userId);

                await _audit.WriteAsync(new AuditEntry
                {
                    Category = AuditCategories.Message,
                    Action = AuditActions.SocialMediaLinkSetDefault,
                    EntityType = AuditEntityTypes.SocialMediaLink,
                    EntityId = link.Id.ToString(),
                    ActorUserId = userId
                });

                _logger.LogInformation("لینک پیش‌فرض سوشیال تنظیم شد — LinkId: {LinkId}", linkId);
                return ApiResponse<SocialMediaLinkResponseDto>.CreateSuccess(
                    MapToDto(link),
                    "لینک پیش‌فرض با موفقیت تنظیم شد");
            }
            catch (DbUpdateConcurrencyException ex)
            {
                await transaction.RollbackAsync();
                _logger.LogWarning(ex, "تداخل همزمانی در تنظیم لینک پیش‌فرض — LinkId: {LinkId}", linkId);
                return ApiResponse<SocialMediaLinkResponseDto>.BadRequest(
                    "این لینک در حال استفاده توسط درخواست دیگری است. لطفاً دوباره تلاش کنید");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "خطا در تنظیم لینک پیش‌فرض — LinkId: {LinkId}", linkId);
                return ApiResponse<SocialMediaLinkResponseDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<DirectSendResultDto>> QuickSendSocialMediaLinkAsync(
            int userId,
            QuickSendSocialMediaLinkDto quickSendDto)
        {
            _logger.LogInformation(
                "ارسال سریع لینک سوشیال — UserId: {UserId}, ContactId: {ContactId}, LinkId: {LinkId}",
                userId,
                quickSendDto.ContactId,
                quickSendDto.LinkId);

            try
            {
                if (quickSendDto.ContactId <= 0 || quickSendDto.LinkId <= 0)
                    return ApiResponse<DirectSendResultDto>.BadRequest(
                        "شناسه مخاطب و لینک الزامی است",
                        errorCode: ErrorCodes.InvalidInput);

                var contact = await _contactRepository.GetByIdAsync(quickSendDto.ContactId);
                if (contact == null || contact.IsDeleted)
                    return ApiResponse<DirectSendResultDto>.NotFound("مخاطب یافت نشد");

                var notebook = await _notebookRepository.GetByIdAsync(contact.ContactNotebookId);
                if (notebook == null || notebook.UserId != userId || notebook.IsDeleted)
                    return ApiResponse<DirectSendResultDto>.Forbidden("مخاطب متعلق به شما نیست");

                var link = await _linkRepository.GetOwnedByIdAsync(quickSendDto.LinkId, userId, asNoTracking: true);
                if (link == null || !link.IsActive)
                    return ApiResponse<DirectSendResultDto>.NotFound("لینک مورد نظر یافت نشد یا فعال نیست");

                if (string.IsNullOrWhiteSpace(link.LinkUrl))
                    return ApiResponse<DirectSendResultDto>.BadRequest(
                        "لینک محتوایی ندارد",
                        errorCode: ErrorCodes.InvalidInput);

                var createMessageResult = await _messageService.CreateMessageAsync(userId, new CreateMessageDto
                {
                    Content = link.LinkUrl.Trim()
                });

                if (!createMessageResult.Success || createMessageResult.Data == null)
                    return ApiResponse<DirectSendResultDto>.BadRequest(
                        createMessageResult.Message ?? "خطا در ایجاد پیام",
                        errorCode: ErrorCodes.InvalidInput);

                var messageId = createMessageResult.Data.Id;

                var selectResult = await _messageService.SelectRecipientsAsync(userId, new SelectRecipientsDto
                {
                    MessageId = messageId,
                    SelectionType = "Individual",
                    MobileNumbers = new List<string> { contact.MobileNumber },
                    FullNames = new List<string> { contact.FullName ?? string.Empty }
                });

                if (!selectResult.Success || selectResult.Data == null)
                    return ApiResponse<DirectSendResultDto>.BadRequest(
                        selectResult.Message ?? "خطا در انتخاب گیرندگان",
                        errorCode: ErrorCodes.InvalidInput);

                var session = await _context.MessageSessions
                    .Where(s =>
                        s.MessageId == messageId &&
                        s.UserId == userId &&
                        !s.IsDeleted &&
                        !s.IsUsed)
                    .OrderByDescending(s => s.CreatedAt)
                    .FirstOrDefaultAsync();

                if (session == null)
                    return ApiResponse<DirectSendResultDto>.BadRequest(
                        "خطا در ایجاد Session برای ارسال",
                        errorCode: ErrorCodes.InvalidInput);

                var sendResult = await _messageService.SendDirectMessageAsync(
                    userId,
                    messageId,
                    new SendDirectMessageDto
                    {
                        SendType = CampaignSendType.Quick,
                        PreventDuplicate = false,
                        DuplicatePreventionHours = 24,
                        SendToSpecificTags = false
                    },
                    session);

                _logger.LogInformation(
                    "ارسال سریع لینک سوشیال انجام شد — MessageId: {MessageId}, ContactId: {ContactId}",
                    messageId,
                    quickSendDto.ContactId);

                return sendResult;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "خطا در ارسال سریع لینک سوشیال — ContactId: {ContactId}, LinkId: {LinkId}",
                    quickSendDto.ContactId,
                    quickSendDto.LinkId);
                return ApiResponse<DirectSendResultDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        private async Task UnsetDefaultsAsync(int userId, int? exceptId = null)
        {
            var defaults = await _context.SocialMediaLinks
                .Where(s =>
                    s.UserId == userId &&
                    s.IsDefault &&
                    !s.IsDeleted &&
                    (exceptId == null || s.Id != exceptId.Value))
                .ToListAsync();

            if (defaults.Count == 0)
                return;

            var now = DateTime.UtcNow;
            foreach (var item in defaults)
            {
                item.IsDefault = false;
                item.UpdatedAt = now;
            }
        }

        private void InvalidateUserCache(int userId)
        {
            // کلیدهای صفحه‌بندی رایج را پاک می‌کنیم (حداکثر ۱۰۰ آیتم در هر صفحه طبق استاندارد)
            for (var page = 1; page <= 20; page++)
            {
                foreach (var size in new[] { 10, 20, 50, 100 })
                    _cache.Remove(BuildListCacheKey(userId, page, size));
            }
        }

        private static string BuildListCacheKey(int userId, int pageNumber, int pageSize) =>
            $"social_media_links:u{userId}:p{pageNumber}:s{pageSize}";

        private static string NormalizePlatform(string? platform) =>
            string.IsNullOrWhiteSpace(platform) ? string.Empty : platform.Trim();

        private static string NormalizeLinkUrl(string? url) =>
            string.IsNullOrWhiteSpace(url) ? string.Empty : url.Trim();

        private static string? ValidateLinkUrl(string linkUrl)
        {
            if (string.IsNullOrWhiteSpace(linkUrl))
                return "آدرس لینک الزامی است";

            if (linkUrl.Length > 500)
                return "آدرس لینک نمی‌تواند بیشتر از 500 کاراکتر باشد";

            if (!Uri.TryCreate(linkUrl, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                return "آدرس لینک باید یک URL معتبر با http یا https باشد";
            }

            return null;
        }

        private static string? DetectLinkType(string? url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return null;

            var normalizedUrl = url.Trim().ToLowerInvariant();
            if (normalizedUrl.StartsWith("http://", StringComparison.Ordinal))
                normalizedUrl = normalizedUrl[7..];
            else if (normalizedUrl.StartsWith("https://", StringComparison.Ordinal))
                normalizedUrl = normalizedUrl[8..];

            if (normalizedUrl.StartsWith("www.", StringComparison.Ordinal))
                normalizedUrl = normalizedUrl[4..];

            if (normalizedUrl.Contains("instagram.com", StringComparison.Ordinal) ||
                normalizedUrl.StartsWith("instagr.am/", StringComparison.Ordinal))
                return "Instagram";

            if (normalizedUrl.Contains("t.me/", StringComparison.Ordinal) ||
                normalizedUrl.Contains("telegram.me/", StringComparison.Ordinal) ||
                normalizedUrl.Contains("telegram.org/", StringComparison.Ordinal))
                return "Telegram";

            if (normalizedUrl.Contains("wa.me/", StringComparison.Ordinal) ||
                normalizedUrl.Contains("whatsapp.com/", StringComparison.Ordinal) ||
                normalizedUrl.Contains("api.whatsapp.com/", StringComparison.Ordinal))
                return "WhatsApp";

            if (normalizedUrl.Contains("linkedin.com/", StringComparison.Ordinal) ||
                normalizedUrl.Contains("linked.in/", StringComparison.Ordinal))
                return "LinkedIn";

            if (normalizedUrl.Contains("twitter.com/", StringComparison.Ordinal) ||
                normalizedUrl.Contains("x.com/", StringComparison.Ordinal) ||
                normalizedUrl.Contains("t.co/", StringComparison.Ordinal))
                return "Twitter";

            if (normalizedUrl.Contains("youtube.com/", StringComparison.Ordinal) ||
                normalizedUrl.Contains("youtu.be/", StringComparison.Ordinal))
                return "YouTube";

            if (normalizedUrl.Contains("facebook.com/", StringComparison.Ordinal) ||
                normalizedUrl.Contains("fb.com/", StringComparison.Ordinal) ||
                normalizedUrl.Contains("fb.me/", StringComparison.Ordinal))
                return "Facebook";

            if (normalizedUrl.Contains("tiktok.com/", StringComparison.Ordinal))
                return "TikTok";

            if (normalizedUrl.Contains("snapchat.com/", StringComparison.Ordinal))
                return "Snapchat";

            if (normalizedUrl.Contains("rubika.ir/", StringComparison.Ordinal) ||
                normalizedUrl.Contains("rubika.com/", StringComparison.Ordinal))
                return "Rubika";

            if (normalizedUrl.Contains("splus.ir/", StringComparison.Ordinal) ||
                normalizedUrl.Contains("soroush+", StringComparison.Ordinal) ||
                normalizedUrl.Contains("sapp.ir/", StringComparison.Ordinal))
                return "Soroush";

            if (normalizedUrl.Contains("eitaa.com/", StringComparison.Ordinal))
                return "Eitaa";

            if (normalizedUrl.Contains("ble.ir/", StringComparison.Ordinal) ||
                normalizedUrl.Contains("bale.ai/", StringComparison.Ordinal))
                return "Bale";

            if (normalizedUrl.Contains('.', StringComparison.Ordinal))
                return "Website";

            return "Unknown";
        }

        private static SocialMediaLinkResponseDto MapToDto(SocialMediaLink link)
        {
            var detected = DetectLinkType(link.LinkUrl);
            return new SocialMediaLinkResponseDto
            {
                Id = link.Id,
                Platform = link.Platform,
                LinkUrl = link.LinkUrl,
                IsActive = link.IsActive,
                IsDefault = link.IsDefault,
                CreatedAt = link.CreatedAt,
                LinkType = detected ?? (string.IsNullOrWhiteSpace(link.Platform) ? "Unknown" : link.Platform)
            };
        }
    }
}
