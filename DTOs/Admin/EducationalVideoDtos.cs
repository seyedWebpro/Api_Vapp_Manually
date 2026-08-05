using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace Api_Vapp.DTOs.Admin
{
    public class EducationalVideoResponseDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string VideoUrl { get; set; } = string.Empty;
        public string? ThumbnailUrl { get; set; }
        public int SortOrder { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class CreateEducationalVideoDto
    {
        [Required(ErrorMessage = "عنوان الزامی است")]
        [MaxLength(300, ErrorMessage = "عنوان نمی‌تواند بیشتر از ۳۰۰ کاراکتر باشد")]
        public string Title { get; set; } = string.Empty;

        [MaxLength(1000, ErrorMessage = "توضیحات نمی‌تواند بیشتر از ۱۰۰۰ کاراکتر باشد")]
        public string? Description { get; set; }

        /// <summary>
        /// لینک خارجی ویدیو (اختیاری اگر VideoFile ارسال شود)
        /// </summary>
        [MaxLength(1000, ErrorMessage = "لینک ویدیو نمی‌تواند بیشتر از ۱۰۰۰ کاراکتر باشد")]
        public string? VideoUrl { get; set; }

        [MaxLength(1000, ErrorMessage = "لینک تصویر بندانگشتی نمی‌تواند بیشتر از ۱۰۰۰ کاراکتر باشد")]
        public string? ThumbnailUrl { get; set; }

        public int SortOrder { get; set; }
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// فایل ویدیو (mp4 / mov / avi) — حداکثر ۲ گیگابایت
        /// </summary>
        public IFormFile? VideoFile { get; set; }
    }

    public class UpdateEducationalVideoDto : CreateEducationalVideoDto
    {
    }
}
