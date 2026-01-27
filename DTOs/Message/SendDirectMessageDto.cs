using System.ComponentModel.DataAnnotations;

namespace Api_Vapp.DTOs.Message
{
    /// <summary>
    /// DTO برای ارسال مستقیم پیام (بدون ایجاد کمپین)
    /// </summary>
    public class SendDirectMessageDto
    {
        [Required(ErrorMessage = "نوع ارسال الزامی است")]
        public CampaignSendType SendType { get; set; } = CampaignSendType.Quick;

        /// <summary>
        /// تاریخ و زمان ارسال (فقط در صورت SendType = Scheduled الزامی است)
        /// </summary>
        public DateTime? ScheduledAt { get; set; }

        public bool PreventDuplicate { get; set; } = false;

        [Range(1, 168, ErrorMessage = "ساعت جلوگیری از ارسال تکراری باید بین 1 تا 168 باشد")]
        public int DuplicatePreventionHours { get; set; } = 24;

        public bool SendToSpecificTags { get; set; } = false;

        public List<int>? SelectedTagIds { get; set; }
    }
}

