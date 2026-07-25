using Api_Vapp.DTOs.BusinessCard;
using Api_Vapp.DTOs.Common;
using Api_Vapp.Interfaces;
using Api_Vapp.Utilities;

namespace Api_Vapp.Services
{
    /// <summary>
    /// سرویس عمومی کارت ویزیت — بدون احراز هویت
    /// </summary>
    public class BusinessCardPublicService : IBusinessCardPublicService
    {
        private readonly IBusinessCardRepository _businessCardRepository;
        private readonly ILogger<BusinessCardPublicService> _logger;

        public BusinessCardPublicService(
            IBusinessCardRepository businessCardRepository,
            ILogger<BusinessCardPublicService> logger)
        {
            _businessCardRepository = businessCardRepository;
            _logger = logger;
        }

        public async Task<ApiResponse<BusinessCardPublicDto>> GetPublicCardAsync(string slug)
        {
            try
            {
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
                    return ApiResponse<BusinessCardPublicDto>.NotFound("کارت ویزیت یافت نشد یا غیرفعال است");
                }

                return ApiResponse<BusinessCardPublicDto>.CreateSuccess(MapToPublicDto(card));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading public business card for slug {Slug}", slug);
                return ApiResponse<BusinessCardPublicDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

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
