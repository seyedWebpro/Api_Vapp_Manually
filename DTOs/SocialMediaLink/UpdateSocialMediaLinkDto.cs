using System.ComponentModel.DataAnnotations;

namespace Api_Vapp.DTOs.SocialMediaLink
{
    /// <summary>
    /// DTO برای به‌روزرسانی لینک شبکه‌های اجتماعی (فقط فیلدهای ارسال‌شده تغییر می‌کنند)
    /// </summary>
    public class UpdateSocialMediaLinkDto
    {
        /// <summary>
        /// نوع پلتفرم (Instagram, Telegram, WhatsApp, Rubika, Soroush, Eitaa, Bale, ...)
        /// </summary>
        [MaxLength(50, ErrorMessage = "نوع پلتفرم نمی‌تواند بیشتر از 50 کاراکتر باشد")]
        public string? Platform { get; set; }

        /// <summary>
        /// آدرس لینک (URL)
        /// </summary>
        [MaxLength(500, ErrorMessage = "آدرس لینک نمی‌تواند بیشتر از 500 کاراکتر باشد")]
        public string? LinkUrl { get; set; }

        /// <summary>
        /// وضعیت فعال/غیرفعال
        /// </summary>
        public bool? IsActive { get; set; }
    }
}
