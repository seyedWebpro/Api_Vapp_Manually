using Api_Vapp.Utilities;

namespace Api_Vapp.DTOs.BusinessCard
{
    /// <summary>
    /// schema عمومی کارت ویزیت برای صفحه وب
    /// </summary>
    public class BusinessCardPublicDto
    {
        public string Title { get; set; } = string.Empty;

        public string? LogoUrl { get; set; }

        public string? TemplateKey { get; set; }

        public bool SliderEnabled { get; set; }

        public bool DescriptionEnabled { get; set; }

        public bool ServicesEnabled { get; set; }

        public bool MapEnabled { get; set; }

        public bool ContactEnabled { get; set; }

        public bool BankingEnabled { get; set; }

        /// <summary>فعال بودن بخش دکمه فروشگاه</summary>
        public bool ShopEnabled { get; set; }

        /// <summary>آدرس فروشگاه — فقط وقتی shopEnabled=true و مقدار دارد</summary>
        public string? ShopUrl { get; set; }

        /// <summary>متن دکمه فروشگاه</summary>
        public string ShopButtonLabel { get; set; } = BusinessCardShopHelper.ButtonLabel;

        public string? DescriptionTitle { get; set; }

        public string? DescriptionText { get; set; }

        public double? MapLatitude { get; set; }

        public double? MapLongitude { get; set; }

        public string? MapAddress { get; set; }

        public string? ContactPhone { get; set; }

        public string? ContactEmail { get; set; }

        public string? ContactInstagram { get; set; }

        public string? BankAccountNumber { get; set; }

        public string? BankCardNumber { get; set; }

        public string? BankShebaNumber { get; set; }

        public List<BusinessCardSliderImageDto> SliderImages { get; set; } = new();

        public List<BusinessCardServiceItemDto> ServiceItems { get; set; } = new();

        public List<BusinessCardSocialLinkDto> SocialLinks { get; set; } = new();
    }
}
