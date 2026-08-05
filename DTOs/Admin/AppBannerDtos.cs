using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace Api_Vapp.DTOs.Admin
{
    public class AppBannerResponseDto
    {
        public int Id { get; set; }
        public string Key { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? ImageUrl { get; set; }
        public string? LinkUrl { get; set; }
        public string LinkType { get; set; } = string.Empty;
        public int SortOrder { get; set; }
        public bool IsActive { get; set; }
        public bool IsSystemManaged { get; set; }
        public bool CanDelete { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class UpdateAppBannerDto
    {
        [Required(ErrorMessage = "عنوان الزامی است")]
        [MaxLength(200, ErrorMessage = "عنوان نمی‌تواند بیشتر از ۲۰۰ کاراکتر باشد")]
        public string Title { get; set; } = string.Empty;

        [MaxLength(1000, ErrorMessage = "توضیحات نمی‌تواند بیشتر از ۱۰۰۰ کاراکتر باشد")]
        public string? Description { get; set; }

        /// <summary>none | app_route | external_url</summary>
        [Required(ErrorMessage = "نوع لینک الزامی است")]
        [MaxLength(30, ErrorMessage = "نوع لینک نمی‌تواند بیشتر از ۳۰ کاراکتر باشد")]
        public string LinkType { get; set; } = "none";

        [MaxLength(1000, ErrorMessage = "لینک نمی‌تواند بیشتر از ۱۰۰۰ کاراکتر باشد")]
        public string? LinkUrl { get; set; }

        public int SortOrder { get; set; }
        public bool IsActive { get; set; } = true;

        /// <summary>فایل تصویر جدید (اختیاری).</summary>
        public IFormFile? ImageFile { get; set; }

        /// <summary>اگر true باشد تصویر فعلی حذف می‌شود (وقتی فایل جدید نیست).</summary>
        public bool ClearImage { get; set; }
    }
}
