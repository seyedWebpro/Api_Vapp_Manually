using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace Api_Vapp.DTOs.BusinessCard
{
    public class UploadBusinessCardImageDto
    {
        [Required(ErrorMessage = "فایل تصویر الزامی است")]
        public IFormFile ImageFile { get; set; } = null!;

        [MaxLength(30, ErrorMessage = "نوع تصویر نمی‌تواند بیشتر از 30 کاراکتر باشد")]
        public string? ImageType { get; set; }
    }
}
