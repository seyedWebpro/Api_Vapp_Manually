using System.ComponentModel.DataAnnotations;

namespace Api_Vapp.DTOs.Message
{
    /// <summary>
    /// ثبت مخاطب در دنباله یک کمپین حرفه‌ای از طریق ارسال سریع.
    /// پیام اول همان لحظه ارسال می‌شود؛ بقیه طبق تأخیر مراحل کمپین از همین لحظه زمان‌بندی می‌شوند.
    /// </summary>
    public class QuickSendProfessionalCampaignDto
    {
        [Required(ErrorMessage = "شناسه مخاطب الزامی است")]
        [Range(1, int.MaxValue, ErrorMessage = "شناسه مخاطب نامعتبر است")]
        public int ContactId { get; set; }
    }
}
