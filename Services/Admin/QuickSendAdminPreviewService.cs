using System.Security.Cryptography;
using Api_Vapp.Constants;
using Api_Vapp.DTOs.Admin;
using Api_Vapp.DTOs.Common;
using Api_Vapp.Interfaces;
using Api_Vapp.Utilities;
using Microsoft.Extensions.Caching.Memory;

namespace Api_Vapp.Services.Admin
{
    /// <summary>
    /// پیش‌نمایش ادمین برای محتوای ارسال سریع — توکن کوتاه‌عمر بدون انتشار عمومی.
    /// </summary>
    public class QuickSendAdminPreviewService : IQuickSendAdminPreviewService
    {
        private static readonly TimeSpan PreviewTokenTtl = TimeSpan.FromMinutes(30);

        private readonly IMemoryCache _cache;
        private readonly IAdminQuickSendApprovalService _approvalService;
        private readonly IUserFormPublicService _formPublicService;
        private readonly IBusinessCardPublicService _businessCardPublicService;
        private readonly ILogger<QuickSendAdminPreviewService> _logger;

        public QuickSendAdminPreviewService(
            IMemoryCache cache,
            IAdminQuickSendApprovalService approvalService,
            IUserFormPublicService formPublicService,
            IBusinessCardPublicService businessCardPublicService,
            ILogger<QuickSendAdminPreviewService> logger)
        {
            _cache = cache;
            _approvalService = approvalService;
            _formPublicService = formPublicService;
            _businessCardPublicService = businessCardPublicService;
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
                var normalizedToken = token?.Trim();
                if (string.IsNullOrWhiteSpace(normalizedToken))
                {
                    return ApiResponse<QuickSendPreviewContentDto>.BadRequest(
                        "توکن پیش‌نمایش نامعتبر است",
                        errorCode: ErrorCodes.InvalidInput);
                }

                if (!_cache.TryGetValue(BuildCacheKey(normalizedToken), out PreviewTokenEntry? entry) || entry == null)
                {
                    return ApiResponse<QuickSendPreviewContentDto>.NotFound(
                        "لینک پیش‌نمایش منقضی یا نامعتبر است");
                }

                if (entry.ExpiresAt <= DateTime.UtcNow)
                {
                    _cache.Remove(BuildCacheKey(normalizedToken));
                    return ApiResponse<QuickSendPreviewContentDto>.NotFound(
                        "لینک پیش‌نمایش منقضی شده است");
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
            var bytes = RandomNumberGenerator.GetBytes(24);
            return Convert.ToBase64String(bytes)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }

        private static string BuildCacheKey(string token) => $"quicksend_preview_{token}";

        private sealed class PreviewTokenEntry
        {
            public string ItemType { get; set; } = string.Empty;

            public int ItemId { get; set; }

            public int CreatedByAdminUserId { get; set; }

            public DateTime ExpiresAt { get; set; }
        }
    }
}
