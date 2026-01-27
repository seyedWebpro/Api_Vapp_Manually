using System.ComponentModel.DataAnnotations;

namespace Api_Vapp.DTOs.SocialMediaLink
{
    /// <summary>
    /// DTO برای ارسال سریع لینک شبکه‌های اجتماعی
    /// </summary>
    public class QuickSendSocialMediaLinkDto
    {
        /// <summary>
        /// شناسه مخاطب که لینک برایش ارسال می‌شود
        /// </summary>
        [Required(ErrorMessage = "شناسه مخاطب الزامی است")]
        public int ContactId { get; set; }

        /// <summary>
        /// شناسه لینک شبکه‌های اجتماعی که می‌خواهید ارسال شود
        /// </summary>
        [Required(ErrorMessage = "شناسه لینک الزامی است")]
        public int LinkId { get; set; }
    }
}





