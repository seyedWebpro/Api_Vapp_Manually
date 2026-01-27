using System.ComponentModel.DataAnnotations;

namespace Api_Vapp.DTOs.Message
{
    /// <summary>
    /// DTO برای ارسال پیام سریع با قالب پیش‌فرض
    /// </summary>
    public class QuickSendMessageDto
    {
        /// <summary>
        /// شناسه مخاطب که پیام برایش ارسال می‌شود
        /// </summary>
        [Required(ErrorMessage = "شناسه مخاطب الزامی است")]
        public int ContactId { get; set; }
    }
}


