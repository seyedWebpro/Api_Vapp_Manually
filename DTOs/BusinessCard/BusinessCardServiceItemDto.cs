using System.ComponentModel.DataAnnotations;

namespace Api_Vapp.DTOs.BusinessCard
{
    public class BusinessCardServiceItemDto
    {
        public int? Id { get; set; }

        [Required(ErrorMessage = "عنوان تعرفه الزامی است")]
        [MaxLength(200, ErrorMessage = "عنوان تعرفه نمی‌تواند بیشتر از 200 کاراکتر باشد")]
        public string Title { get; set; } = string.Empty;

        [Range(0, double.MaxValue, ErrorMessage = "مبلغ تعرفه نمی‌تواند منفی باشد")]
        public decimal Price { get; set; }

        [MaxLength(500, ErrorMessage = "آدرس تصویر نمی‌تواند بیشتر از 500 کاراکتر باشد")]
        public string? ImageUrl { get; set; }

        public int DisplayOrder { get; set; }
    }
}
