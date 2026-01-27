using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace Api_Vapp.DTOs.Message
{
    /// <summary>
    /// DTO برای به‌روزرسانی گروه قالب
    /// </summary>
    public class UpdateTemplateGroupDto
    {
        [MaxLength(200, ErrorMessage = "نام گروه نمی‌تواند بیشتر از 200 کاراکتر باشد")]
        public string? Name { get; set; }

        [MaxLength(500, ErrorMessage = "توضیحات نمی‌تواند بیشتر از 500 کاراکتر باشد")]
        public string? Description { get; set; }

        /// <summary>
        /// آیکون گروه به صورت متن (emoji یا URL) - اختیاری
        /// </summary>
        public string? Icon { get; set; }

        /// <summary>
        /// فایل آیکون برای آپلود - اختیاری
        /// در صورت ارسال، فایل آپلود می‌شود و مسیر آن در فیلد Icon ذخیره می‌شود
        /// </summary>
        public IFormFile? IconFile { get; set; }

        public int? DisplayOrder { get; set; }

        public bool? IsActive { get; set; }
    }
}

