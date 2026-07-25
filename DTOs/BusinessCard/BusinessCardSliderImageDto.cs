using System.ComponentModel.DataAnnotations;

namespace Api_Vapp.DTOs.BusinessCard
{
    public class BusinessCardSliderImageDto
    {
        [Required(ErrorMessage = "آدرس تصویر الزامی است")]
        [MaxLength(500, ErrorMessage = "آدرس تصویر نمی‌تواند بیشتر از 500 کاراکتر باشد")]
        public string ImageUrl { get; set; } = string.Empty;

        public int DisplayOrder { get; set; }
    }
}
