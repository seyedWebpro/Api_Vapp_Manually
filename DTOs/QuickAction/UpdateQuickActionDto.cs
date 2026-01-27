using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace Api_Vapp.DTOs.QuickAction
{
    /// <summary>
    /// DTO برای به‌روزرسانی اقدام سریع
    /// </summary>
    public class UpdateQuickActionDto
    {
        [MaxLength(100, ErrorMessage = "نام لینک نمی‌تواند بیشتر از 100 کاراکتر باشد")]
        public string? Name { get; set; }

        [MaxLength(50)]
        public string? ActionType { get; set; }

        public string? Content { get; set; }

        public bool? IsActive { get; set; }

        // فایل آیکون برای آپلود (اختیاری)
        public IFormFile? IconFile { get; set; }
    }
}












