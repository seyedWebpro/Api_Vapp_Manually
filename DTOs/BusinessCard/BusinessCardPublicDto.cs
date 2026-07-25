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

        public string? DescriptionTitle { get; set; }

        public string? DescriptionText { get; set; }

        public double? MapLatitude { get; set; }

        public double? MapLongitude { get; set; }

        public string? MapAddress { get; set; }

        public string? ContactPhone { get; set; }

        public string? ContactEmail { get; set; }

        public string? ContactInstagram { get; set; }

        public List<BusinessCardSliderImageDto> SliderImages { get; set; } = new();

        public List<BusinessCardServiceItemDto> ServiceItems { get; set; } = new();
    }
}
