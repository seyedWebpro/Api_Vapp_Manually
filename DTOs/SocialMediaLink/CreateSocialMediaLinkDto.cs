using System.ComponentModel.DataAnnotations;

namespace Api_Vapp.DTOs.SocialMediaLink
{
    /// <summary>
    /// DTO برای ایجاد لینک شبکه‌های اجتماعی جدید
    /// </summary>
    public class CreateSocialMediaLinkDto
    {
        /// <summary>
        /// نوع پلتفرم (Instagram, Telegram, WhatsApp, Rubika, Soroush, Eitaa, Bale, ...)
        /// </summary>
        [Required(ErrorMessage = "نوع پلتفرم الزامی است")]
        [MaxLength(50, ErrorMessage = "نوع پلتفرم نمی‌تواند بیشتر از 50 کاراکتر باشد")]
        public string Platform { get; set; } = string.Empty;

        /// <summary>
        /// آدرس لینک (URL)
        /// </summary>
        [Required(ErrorMessage = "آدرس لینک الزامی است")]
        [MaxLength(500, ErrorMessage = "آدرس لینک نمی‌تواند بیشتر از 500 کاراکتر باشد")]
        [Url(ErrorMessage = "آدرس لینک باید معتبر باشد")]
        public string LinkUrl { get; set; } = string.Empty;
    }
}





