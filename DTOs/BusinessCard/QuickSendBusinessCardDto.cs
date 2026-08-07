using System.ComponentModel.DataAnnotations;

namespace Api_Vapp.DTOs.BusinessCard
{
    /// <summary>
    /// DTO برای ارسال سریع لینک کارت ویزیت به یک مخاطب
    /// </summary>
    public class QuickSendBusinessCardDto
    {
        /// <summary>
        /// شناسه مخاطب که کارت ویزیت برایش ارسال می‌شود
        /// </summary>
        [Required(ErrorMessage = "شناسه مخاطب الزامی است")]
        public int ContactId { get; set; }

        /// <summary>
        /// شناسه کارت ویزیتی که می‌خواهید ارسال شود
        /// </summary>
        [Required(ErrorMessage = "شناسه کارت ویزیت الزامی است")]
        public int BusinessCardId { get; set; }
    }
}
