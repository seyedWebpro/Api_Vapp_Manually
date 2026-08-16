using Api_Vapp.DTOs.BusinessCard;
using Api_Vapp.DTOs.Common;
using Api_Vapp.Interfaces;
using Api_Vapp.Utilities;
using Microsoft.Extensions.Caching.Memory;

namespace Api_Vapp.Services
{
    /// <summary>
    /// سرویس عمومی کارت ویزیت — بدون احراز هویت
    /// </summary>
    public class BusinessCardPublicService : IBusinessCardPublicService
    {
        private static readonly TimeSpan PublicCacheTtl = TimeSpan.FromMinutes(10);

        private readonly IBusinessCardRepository _businessCardRepository;
        private readonly IFileUploadService _fileUploadService;
        private readonly IMemoryCache _cache;
        private readonly ILogger<BusinessCardPublicService> _logger;

        public BusinessCardPublicService(
            IBusinessCardRepository businessCardRepository,
            IFileUploadService fileUploadService,
            IMemoryCache cache,
            ILogger<BusinessCardPublicService> logger)
        {
            _businessCardRepository = businessCardRepository;
            _fileUploadService = fileUploadService;
            _cache = cache;
            _logger = logger;
        }

        public async Task<ApiResponse<BusinessCardPublicDto>> GetPublicCardAsync(string slug)
        {
            try
            {
                _logger.LogInformation("شروع دریافت کارت ویزیت عمومی — Slug: {Slug}", slug);

                var normalizedSlug = BusinessCardSlugHelper.Normalize(slug);
                if (normalizedSlug == null)
                {
                    return ApiResponse<BusinessCardPublicDto>.BadRequest(
                        "لینک نامعتبر است",
                        errorCode: ErrorCodes.InvalidInput);
                }

                var card = await _businessCardRepository.GetBySlugReadOnlyAsync(normalizedSlug);
                if (card == null)
                {
                    return ApiResponse<BusinessCardPublicDto>.NotFound("کارت ویزیت یافت نشد");
                }

                if (!card.IsActive)
                {
                    return ApiResponse<BusinessCardPublicDto>.Forbidden(
                        "این کارت ویزیت در حال حاضر فعال نیست",
                        ErrorCodes.ResourceInactive);
                }

                var approvalError = QuickSendContentApprovalHelper.TryBlockPublicAccess<BusinessCardPublicDto>(
                    card.ApprovalStatus,
                    "کارت ویزیت");
                if (approvalError != null)
                {
                    return approvalError;
                }

                var cacheKey = BuildCacheKey(normalizedSlug);
                if (_cache.TryGetValue(cacheKey, out BusinessCardPublicDto? cached) && cached != null)
                {
                    _logger.LogInformation("کارت ویزیت عمومی از کش — Slug: {Slug}", normalizedSlug);
                    return ApiResponse<BusinessCardPublicDto>.CreateSuccess(cached);
                }

                var dto = MapToPublicDto(card);
                _cache.Set(
                    cacheKey,
                    dto,
                    new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = PublicCacheTtl,
                        Size = 1
                    });

                _logger.LogInformation("پایان دریافت کارت ویزیت عمومی — Slug: {Slug}", normalizedSlug);
                return ApiResponse<BusinessCardPublicDto>.CreateSuccess(dto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading public business card for slug {Slug}", slug);
                return ApiResponse<BusinessCardPublicDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        /// <summary>
        /// حذف کش عمومی بعد از Create/Update/Delete/Publish/Toggle
        /// </summary>
        public static void InvalidatePublicCache(IMemoryCache cache, string? slug)
        {
            var normalized = BusinessCardSlugHelper.Normalize(slug);
            if (normalized == null)
            {
                return;
            }

            cache.Remove(BuildCacheKey(normalized));
        }

        private static string BuildCacheKey(string normalizedSlug) => $"businesscard_public_{normalizedSlug}";

        private BusinessCardPublicDto MapToPublicDto(Models.BusinessCard card)
        {
            var socialLinks = card.SocialLinks
                .OrderBy(i => i.DisplayOrder)
                .Select(i => new BusinessCardSocialLinkDto
                {
                    Id = i.Id,
                    NetworkType = i.NetworkType,
                    Label = BusinessCardSocialNetworkHelper.ResolveDisplayLabel(i.NetworkType, i.Label),
                    Value = i.Value,
                    DisplayOrder = i.DisplayOrder
                })
                .ToList();

            if (socialLinks.Count == 0 && !string.IsNullOrWhiteSpace(card.ContactInstagram))
            {
                socialLinks.Add(new BusinessCardSocialLinkDto
                {
                    NetworkType = "instagram",
                    Label = BusinessCardSocialNetworkHelper.ResolveDisplayLabel("instagram", null),
                    Value = card.ContactInstagram,
                    DisplayOrder = 0
                });
            }

            return new BusinessCardPublicDto
            {
                Title = card.Title,
                LogoUrl = ToPublicFileUrl(card.LogoUrl),
                TemplateKey = card.TemplateKey,
                SliderEnabled = card.SliderEnabled,
                DescriptionEnabled = card.DescriptionEnabled,
                ServicesEnabled = card.ServicesEnabled,
                MapEnabled = card.MapEnabled,
                ContactEnabled = card.ContactEnabled,
                BankingEnabled = card.BankingEnabled,
                DescriptionTitle = card.DescriptionTitle,
                DescriptionText = card.DescriptionText,
                MapLatitude = card.MapLatitude,
                MapLongitude = card.MapLongitude,
                MapAddress = card.MapAddress,
                ContactPhone = card.ContactPhone,
                ContactEmail = card.ContactEmail,
                ContactInstagram = card.ContactInstagram,
                BankAccountNumber = card.BankingEnabled ? card.BankAccountNumber : null,
                BankCardNumber = card.BankingEnabled ? card.BankCardNumber : null,
                BankShebaNumber = card.BankingEnabled ? card.BankShebaNumber : null,
                SliderImages = card.SliderEnabled
                    ? card.SliderImages
                        .OrderBy(i => i.DisplayOrder)
                        .Select(i => new BusinessCardSliderImageDto
                        {
                            ImageUrl = ToPublicFileUrl(i.ImageUrl) ?? i.ImageUrl,
                            DisplayOrder = i.DisplayOrder
                        })
                        .ToList()
                    : new List<BusinessCardSliderImageDto>(),
                ServiceItems = card.ServicesEnabled
                    ? card.ServiceItems
                        .OrderBy(i => i.DisplayOrder)
                        .Select(i => new BusinessCardServiceItemDto
                        {
                            Id = i.Id,
                            Title = i.Title,
                            Price = i.Price,
                            ImageUrl = ToPublicFileUrl(i.ImageUrl),
                            DisplayOrder = i.DisplayOrder
                        })
                        .ToList()
                    : new List<BusinessCardServiceItemDto>(),
                SocialLinks = card.ContactEnabled
                    ? socialLinks
                    : new List<BusinessCardSocialLinkDto>()
            };
        }

        private string? ToPublicFileUrl(string? storedPath)
        {
            if (string.IsNullOrWhiteSpace(storedPath))
            {
                return null;
            }

            return _fileUploadService.GetFileUrl(storedPath);
        }
    }
}
