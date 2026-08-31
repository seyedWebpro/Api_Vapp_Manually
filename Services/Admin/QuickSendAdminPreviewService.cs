using System.Security.Cryptography;
using Api_Vapp.Constants;
using Api_Vapp.DTOs.Admin;
using Api_Vapp.DTOs.Common;
using Api_Vapp.Interfaces;
using Api_Vapp.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;

namespace Api_Vapp.Services.Admin
{
    /// <summary>
    /// پیش‌نمایش ادمین برای محتوای ارسال سریع — توکن کوتاه‌عمر بدون انتشار عمومی.
    /// </summary>
    public class QuickSendAdminPreviewService : IQuickSendAdminPreviewService
    {
        private static readonly TimeSpan PreviewTokenTtl = TimeSpan.FromMinutes(30);
        /// <summary>Base64url بدون padding — 24 بایت entropy</summary>
        private const int PreviewTokenByteLength = 24;
        private const int PreviewTokenCharLength = 32;

        private readonly IMemoryCache _cache;
        private readonly IAdminQuickSendApprovalService _approvalService;
        private readonly IUserFormPublicService _formPublicService;
        private readonly IBusinessCardPublicService _businessCardPublicService;
        private readonly IQuickSendPreviewRateLimiter _rateLimiter;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<QuickSendAdminPreviewService> _logger;

        public QuickSendAdminPreviewService(
            IMemoryCache cache,
            IAdminQuickSendApprovalService approvalService,
            IUserFormPublicService formPublicService,
            IBusinessCardPublicService businessCardPublicService,
            IQuickSendPreviewRateLimiter rateLimiter,
            IHttpContextAccessor httpContextAccessor,
            ILogger<QuickSendAdminPreviewService> logger)
        {
            _cache = cache;
            _approvalService = approvalService;
            _formPublicService = formPublicService;
            _businessCardPublicService = businessCardPublicService;
            _rateLimiter = rateLimiter;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }

        public async Task<ApiResponse<QuickSendPreviewTokenDto>> CreatePreviewTokenAsync(
            string itemType,
            int id,
            int adminUserId)
        {
            try
            {
                if (!QuickSendItemTypes.IsValid(itemType))
                {
                    return ApiResponse<QuickSendPreviewTokenDto>.BadRequest(
                        "نوع آیتم ارسال سریع نامعتبر است",
                        errorCode: ErrorCodes.InvalidInput);
                }

                var normalized = QuickSendItemTypes.Normalize(itemType);
                if (!SupportsVisualPreview(normalized))
                {
                    return ApiResponse<QuickSendPreviewTokenDto>.BadRequest(
                        "پیش‌نمایش بصری برای این نوع محتوا پشتیبانی نمی‌شود",
                        errorCode: ErrorCodes.InvalidInput);
                }

                var itemResult = await _approvalService.GetByIdAsync(normalized, id);
                if (!itemResult.Success || itemResult.Data == null)
                {
                    return ApiResponse<QuickSendPreviewTokenDto>.Error(
                        itemResult.Message,
                        itemResult.StatusCode,
                        itemResult.Errors,
                        itemResult.ErrorCode);
                }

                var (issueAllowed, issueRetryAfter) = await _rateLimiter.CheckTokenIssueAsync(adminUserId);
                if (!issueAllowed)
                {
                    _logger.LogWarning(
                        "Preview token issue rate limited for admin {AdminUserId}, retryAfter={RetryAfter}s",
                        adminUserId,
                        issueRetryAfter);
                    return ApiResponse<QuickSendPreviewTokenDto>.Error(
                        "تعداد درخواست پیش‌نمایش بیش از حد مجاز است. لطفاً کمی بعد دوباره تلاش کنید.",
                        StatusCodes.Status429TooManyRequests,
                        errorCode: ErrorCodes.RateLimited);
                }

                var token = GenerateToken();
                var expiresAt = DateTime.UtcNow.Add(PreviewTokenTtl);
                var cacheKey = BuildCacheKey(token);

                _cache.Set(
                    cacheKey,
                    new PreviewTokenEntry
                    {
                        ItemType = normalized,
                        ItemId = id,
                        CreatedByAdminUserId = adminUserId,
                        ExpiresAt = expiresAt
                    },
                    new MemoryCacheEntryOptions
                    {
                        AbsoluteExpiration = expiresAt,
                        Size = 1
                    });

                await _rateLimiter.RecordTokenIssueAsync(adminUserId);

                return ApiResponse<QuickSendPreviewTokenDto>.CreateSuccess(new QuickSendPreviewTokenDto
                {
                    Token = token,
                    ExpiresAt = expiresAt,
                    PreviewPath = $"/preview/{token}"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating preview token for {ItemType}/{Id}", itemType, id);
                return ApiResponse<QuickSendPreviewTokenDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<QuickSendPreviewContentDto>> GetPreviewByTokenAsync(string token)
        {
            try
            {
                var clientKey = ResolveClientKey();
                var (readAllowed, readRetryAfter) = await _rateLimiter.CheckPreviewReadAsync(clientKey);
                if (!readAllowed)
                {
                    _logger.LogWarning(
                        "Preview read rate limited for client {ClientKey}, retryAfter={RetryAfter}s",
                        clientKey,
                        readRetryAfter);
                    return ApiResponse<QuickSendPreviewContentDto>.Error(
                        "تعداد درخواست پیش‌نمایش بیش از حد مجاز است. لطفاً کمی بعد دوباره تلاش کنید.",
                        StatusCodes.Status429TooManyRequests,
                        errorCode: ErrorCodes.RateLimited);
                }

                var normalizedToken = token?.Trim();
                if (string.IsNullOrWhiteSpace(normalizedToken) || !IsValidTokenFormat(normalizedToken))
                {
                    await _rateLimiter.RecordPreviewReadAsync(clientKey);
                    return ApiResponse<QuickSendPreviewContentDto>.BadRequest(
                        "توکن پیش‌نمایش نامعتبر است",
                        errorCode: ErrorCodes.TokenInvalid);
                }

                if (!_cache.TryGetValue(BuildCacheKey(normalizedToken), out PreviewTokenEntry? entry) || entry == null)
                {
                    await _rateLimiter.RecordPreviewReadAsync(clientKey);
                    return ApiResponse<QuickSendPreviewContentDto>.NotFound(
                        "لینک پیش‌نمایش منقضی یا نامعتبر است",
                        errorCode: ErrorCodes.TokenInvalid);
                }

                if (entry.ExpiresAt <= DateTime.UtcNow)
                {
                    _cache.Remove(BuildCacheKey(normalizedToken));
                    await _rateLimiter.RecordPreviewReadAsync(clientKey);
                    return ApiResponse<QuickSendPreviewContentDto>.NotFound(
                        "لینک پیش‌نمایش منقضی شده است",
                        errorCode: ErrorCodes.TokenExpired);
                }

                var itemResult = await _approvalService.GetByIdAsync(entry.ItemType, entry.ItemId);
                if (!itemResult.Success || itemResult.Data == null)
                {
                    return ApiResponse<QuickSendPreviewContentDto>.NotFound("محتوای پیش‌نمایش یافت نشد");
                }

                var item = itemResult.Data;
                var content = new QuickSendPreviewContentDto
                {
                    ItemType = item.ItemType,
                    ItemTypeTitle = item.ItemTypeTitle,
                    Title = item.Title,
                    ApprovalStatus = item.ApprovalStatus,
                    RejectionReason = item.RejectionReason,
                    IsAdminPreview = true
                };

                if (entry.ItemType == QuickSendItemTypes.UserForm)
                {
                    var formResult = await _formPublicService.GetAdminPreviewByIdAsync(entry.ItemId);
                    if (!formResult.Success || formResult.Data == null)
                    {
                        return ApiResponse<QuickSendPreviewContentDto>.Error(
                            formResult.Message,
                            formResult.StatusCode,
                            formResult.Errors,
                            formResult.ErrorCode);
                    }

                    content.Form = formResult.Data;
                }
                else if (entry.ItemType == QuickSendItemTypes.BusinessCard)
                {
                    var cardResult = await _businessCardPublicService.GetAdminPreviewByIdAsync(entry.ItemId);
                    if (!cardResult.Success || cardResult.Data == null)
                    {
                        return ApiResponse<QuickSendPreviewContentDto>.Error(
                            cardResult.Message,
                            cardResult.StatusCode,
                            cardResult.Errors,
                            cardResult.ErrorCode);
                    }

                    content.BusinessCard = cardResult.Data;
                }
                else
                {
                    return ApiResponse<QuickSendPreviewContentDto>.BadRequest(
                        "پیش‌نمایش بصری برای این نوع محتوا پشتیبانی نمی‌شود",
                        errorCode: ErrorCodes.InvalidInput);
                }

                await _rateLimiter.RecordPreviewReadAsync(clientKey);
                return ApiResponse<QuickSendPreviewContentDto>.CreateSuccess(content);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading preview for token");
                return ApiResponse<QuickSendPreviewContentDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        private static bool SupportsVisualPreview(string itemType) =>
            itemType is QuickSendItemTypes.UserForm or QuickSendItemTypes.BusinessCard;

        private static string GenerateToken()
        {
            var bytes = RandomNumberGenerator.GetBytes(PreviewTokenByteLength);
            return Convert.ToBase64String(bytes)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }

        private static bool IsValidTokenFormat(string token)
        {
            if (token.Length != PreviewTokenCharLength)
            {
                return false;
            }

            foreach (var ch in token)
            {
                var isAllowed =
                    (ch >= 'a' && ch <= 'z') ||
                    (ch >= 'A' && ch <= 'Z') ||
                    (ch >= '0' && ch <= '9') ||
                    ch is '-' or '_';
                if (!isAllowed)
                {
                    return false;
                }
            }

            return true;
        }

        private static string BuildCacheKey(string token) => $"quicksend_preview_{token}";

        private string ResolveClientKey()
        {
            var ctx = _httpContextAccessor.HttpContext;
            if (ctx == null)
            {
                return "unknown";
            }

            var forwarded = ctx.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(forwarded))
            {
                var first = forwarded.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(first))
                {
                    return first;
                }
            }

            return ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        }

        private sealed class PreviewTokenEntry
        {
            public string ItemType { get; set; } = string.Empty;

            public int ItemId { get; set; }

            public int CreatedByAdminUserId { get; set; }

            public DateTime ExpiresAt { get; set; }
        }
    }
}
