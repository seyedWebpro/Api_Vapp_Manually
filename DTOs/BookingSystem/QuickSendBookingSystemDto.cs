using System.ComponentModel.DataAnnotations;

namespace Api_Vapp.DTOs.BookingSystem
{
    /// <summary>
    /// DTO برای ارسال سریع لینک سیستم رزرو نوبت به یک مخاطب
    /// </summary>
    public class QuickSendBookingSystemDto
    {
        [Required(ErrorMessage = "شناسه مخاطب الزامی است")]
        public int ContactId { get; set; }

        [Required(ErrorMessage = "شناسه سیستم رزرو الزامی است")]
        public int BookingSystemId { get; set; }
    }
}
