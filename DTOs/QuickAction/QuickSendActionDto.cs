using System.ComponentModel.DataAnnotations;

namespace Api_Vapp.DTOs.QuickAction
{
    /// <summary>
    /// DTO برای ارسال سریع لینک با اکشن پیش‌فرض
    /// </summary>
    public class QuickSendActionDto
    {
        /// <summary>
        /// شناسه مخاطب که لینک برایش ارسال می‌شود
        /// </summary>
        [Required(ErrorMessage = "شناسه مخاطب الزامی است")]
        public int ContactId { get; set; }
    }
}






