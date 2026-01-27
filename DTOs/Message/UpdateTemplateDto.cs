using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace Api_Vapp.DTOs.Message
{
    /// <summary>
    /// DTO برای به‌روزرسانی قالب پیام
    /// </summary>
    public class UpdateTemplateDto
    {
        [MaxLength(100, ErrorMessage = "نام قالب نمی‌تواند بیشتر از 100 کاراکتر باشد")]
        public string? Name { get; set; }

        [MaxLength(1000, ErrorMessage = "متن قالب نمی‌تواند بیشتر از 1000 کاراکتر باشد")]
        public string? Content { get; set; }

        [MaxLength(50)]
        public string? Category { get; set; }

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

        public bool? IsActive { get; set; }

        // شناسه گروه قالب (اختیاری)
        public int? GroupId { get; set; }
    }
}












