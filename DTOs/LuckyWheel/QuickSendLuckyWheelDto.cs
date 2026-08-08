using System.ComponentModel.DataAnnotations;

namespace Api_Vapp.DTOs.LuckyWheel
{
    /// <summary>
    /// DTO برای ارسال سریع لینک گردونه شانس به یک مخاطب
    /// </summary>
    public class QuickSendLuckyWheelDto
    {
        [Required(ErrorMessage = "شناسه مخاطب الزامی است")]
        public int ContactId { get; set; }

        [Required(ErrorMessage = "شناسه گردونه الزامی است")]
        public int LuckyWheelId { get; set; }
    }
}
