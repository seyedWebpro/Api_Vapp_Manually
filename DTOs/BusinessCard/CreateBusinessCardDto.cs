using System.ComponentModel.DataAnnotations;

namespace Api_Vapp.DTOs.BusinessCard
{
    public class CreateBusinessCardDto
    {
        [MaxLength(100, ErrorMessage = "templateKey نمی‌تواند بیشتر از 100 کاراکتر باشد")]
        public string? TemplateKey { get; set; }

        [MaxLength(200, ErrorMessage = "نام کسب‌وکار نمی‌تواند بیشتر از 200 کاراکتر باشد")]
        public string? Title { get; set; }

        [MaxLength(500, ErrorMessage = "آدرس لوگو نمی‌تواند بیشتر از 500 کاراکتر باشد")]
        public string? LogoUrl { get; set; }

        [MaxLength(100, ErrorMessage = "slug نمی‌تواند بیشتر از 100 کاراکتر باشد")]
        public string? Slug { get; set; }

        public bool? SliderEnabled { get; set; }

        public bool? DescriptionEnabled { get; set; }

        public bool? ServicesEnabled { get; set; }

        public bool? MapEnabled { get; set; }

        public bool? ContactEnabled { get; set; }

        [MaxLength(200, ErrorMessage = "عنوان توضیحات نمی‌تواند بیشتر از 200 کاراکتر باشد")]
        public string? DescriptionTitle { get; set; }

        [MaxLength(4000, ErrorMessage = "متن توضیحات نمی‌تواند بیشتر از ۴۰۰۰ کاراکتر باشد")]
        public string? DescriptionText { get; set; }

        public double? MapLatitude { get; set; }

        public double? MapLongitude { get; set; }

        [MaxLength(500, ErrorMessage = "آدرس نقشه نمی‌تواند بیشتر از 500 کاراکتر باشد")]
        public string? MapAddress { get; set; }

        [MaxLength(20, ErrorMessage = "شماره تماس نمی‌تواند بیشتر از 20 کاراکتر باشد")]
        public string? ContactPhone { get; set; }

        [MaxLength(200, ErrorMessage = "ایمیل نمی‌تواند بیشتر از 200 کاراکتر باشد")]
        public string? ContactEmail { get; set; }

        [MaxLength(100, ErrorMessage = "اینستاگرام نمی‌تواند بیشتر از 100 کاراکتر باشد")]
        public string? ContactInstagram { get; set; }

        public List<BusinessCardSliderImageDto> SliderImages { get; set; } = new();

        public List<BusinessCardServiceItemDto> ServiceItems { get; set; } = new();
    }
}
