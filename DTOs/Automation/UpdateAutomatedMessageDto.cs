using System.ComponentModel.DataAnnotations;

namespace Api_Vapp.DTOs.Automation
{
    /// <summary>
    /// DTO برای به‌روزرسانی پیام خودکار
    /// </summary>
    public class UpdateAutomatedMessageDto
    {
        [MaxLength(200)]
        public string? Title { get; set; }

        [MaxLength(1000)]
        public string? Description { get; set; }

        public int? MessageId { get; set; }

        public string? MessageContent { get; set; }

        public int? DaysBeforeEvent { get; set; }

        public int? SpecialOccasionId { get; set; }

        public string? ActivationConditions { get; set; }

        public TimeSpan? ScheduledTime { get; set; }

        public string? Icon { get; set; }

        public bool? IsActive { get; set; }
    }
}


