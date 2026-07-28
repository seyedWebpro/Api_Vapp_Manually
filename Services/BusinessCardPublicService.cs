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
        private readonly IMemoryCache _cache;
        private readonly ILogger<BusinessCardPublicService> _logger;

        public BusinessCardPublicService(
            IBusinessCardRepository businessCardRepository,
            IMemoryCache cache,
            ILogger<BusinessCardPublicService> logger)
        {
            _businessCardRepository = businessCardRepository;
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

                var cacheKey = BuildCacheKey(normalizedSlug);
                if (_cache.TryGetValue(cacheKey, out BusinessCardPublicDto? cached) && cached != null)
                {
                    _logger.LogInformation("کارت ویزیت عمومی از کش — Slug: {Slug}", normalizedSlug);
                    return ApiResponse<BusinessCardPublicDto>.CreateSuccess(cached);
                }

                var card = await _businessCardRepository.GetBySlugReadOnlyAsync(normalizedSlug);
                if (card == null)
                {
                    return ApiResponse<BusinessCardPublicDto>.NotFound("کارت ویزیت یافت نشد یا غیرفعال است");
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

        private static BusinessCardPublicDto MapToPublicDto(Models.BusinessCard card)
        {
            return new BusinessCardPublicDto
            {
                Title = card.Title,
                LogoUrl = card.LogoUrl,
                TemplateKey = card.TemplateKey,
                SliderEnabled = card.SliderEnabled,
                DescriptionEnabled = card.DescriptionEnabled,
                ServicesEnabled = card.ServicesEnabled,
                MapEnabled = card.MapEnabled,
                ContactEnabled = card.ContactEnabled,
                DescriptionTitle = card.DescriptionTitle,
                DescriptionText = card.DescriptionText,
                MapLatitude = card.MapLatitude,
                MapLongitude = card.MapLongitude,
                MapAddress = card.MapAddress,
                ContactPhone = card.ContactPhone,
                ContactEmail = card.ContactEmail,
                ContactInstagram = card.ContactInstagram,
                SliderImages = card.SliderEnabled
                    ? card.SliderImages
                        .OrderBy(i => i.DisplayOrder)
                        .Select(i => new BusinessCardSliderImageDto
                        {
                            ImageUrl = i.ImageUrl,
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
                            ImageUrl = i.ImageUrl,
                            DisplayOrder = i.DisplayOrder
                        })
                        .ToList()
                    : new List<BusinessCardServiceItemDto>()
            };
        }
    }
}
