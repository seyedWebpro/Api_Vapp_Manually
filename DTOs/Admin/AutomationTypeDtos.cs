using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace Api_Vapp.DTOs.Admin
{
    public class AutomationTypeAdminResponseDto
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        /// <summary>مسیر فایل آپلودشده یا (legacy) ایموجی متنی.</summary>
        public string? Icon { get; set; }
        public int SortOrder { get; set; }
        public bool IsActive { get; set; }
        public bool IsSystemManaged { get; set; }
        public bool CanChangeCode { get; set; }
        public bool CanDelete { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class UpdateAutomationTypeDto
    {
        [Required(ErrorMessage = "نام الزامی است")]
        [MaxLength(200, ErrorMessage = "نام نمی‌تواند بیشتر از ۲۰۰ کاراکتر باشد")]
        public string Name { get; set; } = string.Empty;

        [MaxLength(1000, ErrorMessage = "توضیحات نمی‌تواند بیشتر از ۱۰۰۰ کاراکتر باشد")]
        public string? Description { get; set; }

        public int SortOrder { get; set; }
        public bool IsActive { get; set; } = true;

        /// <summary>فایل آیکون جدید (اختیاری).</summary>
        public IFormFile? IconFile { get; set; }

        /// <summary>اگر true باشد آیکون فعلی حذف می‌شود (وقتی فایل جدید نیست).</summary>
        public bool ClearIcon { get; set; }
    }
}
