using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Api_Vapp.Utilities;

namespace Api_Vapp.DTOs.Message
{
    /// <summary>
    /// DTO برای ایجاد کمپین پیام
    /// </summary>
    public class CreateCampaignDto
    {
        [Required(ErrorMessage = "شناسه پیام الزامی است")]
        public int MessageId { get; set; }

        [MaxLength(200)]
        public string? Title { get; set; }

        [Required(ErrorMessage = "نوع ارسال الزامی است")]
        public CampaignSendType SendType { get; set; } = CampaignSendType.Quick;

        /// <summary>
        /// تاریخ و زمان ارسال (فقط در صورت SendType = Scheduled الزامی است)
        /// </summary>
        [JsonConverter(typeof(NullableDateTimeConverter))]
        public DateTime? ScheduledAt { get; set; }

        /// <summary>
        /// ارسال فوری برای تست (فقط در Development) - اگر true باشد، حتی اگر زمان گذشته باشد، پیام فوراً ارسال می‌شود
        /// </summary>
        public bool ForceSend { get; set; } = false;

        public bool PreventDuplicate { get; set; } = false;

        [Range(1, 168, ErrorMessage = "ساعت جلوگیری از ارسال تکراری باید بین 1 تا 168 باشد")]
        public int DuplicatePreventionHours { get; set; } = 24;

        public bool SendToSpecificTags { get; set; } = false;

        public List<int>? SelectedTagIds { get; set; }
    }
}


