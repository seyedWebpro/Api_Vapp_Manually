using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace Api_Vapp.DTOs.Message
{
    /// <summary>
    /// DTO برای ایجاد قالب پیام
    /// </summary>
    public class CreateTemplateDto
    {
        [Required(ErrorMessage = "نام قالب الزامی است")]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "متن قالب الزامی است")]
        public string Content { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? Category { get; set; }

        [MaxLength(500)]
        public string? Description { get; set; }

        /// <summary>
        /// آیکون قالب به صورت متن (emoji یا URL) - اختیاری
        /// </summary>
        public string? Icon { get; set; }

        /// <summary>
        /// فایل آیکون برای آپلود - اختیاری
        /// در صورت ارسال، فایل آپلود می‌شود و مسیر آن در فیلد Icon ذخیره می‌شود
        /// </summary>
        public IFormFile? IconFile { get; set; }

        // شناسه گروه قالب (اختیاری)
        public int? GroupId { get; set; }
    }
}


