using System.ComponentModel.DataAnnotations;

namespace Api_Vapp.DTOs.Automation
{
    /// <summary>
    /// DTO برای ایجاد پیام خودکار جدید
    /// </summary>
    public class CreateAutomatedMessageDto
    {
        [Required(ErrorMessage = "نوع اتوماسیون الزامی است")]
        public string AutomationType { get; set; } = string.Empty;
        // Birthday, CashbackExpiry, Welcome, PurchaseReminder, SpecialOccasion, Custom

        [Required(ErrorMessage = "عنوان الزامی است")]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string? Description { get; set; }

        public int? MessageId { get; set; }

        public string? MessageContent { get; set; }

        // برای CashbackExpiry و PurchaseReminder
        public int? DaysBeforeEvent { get; set; }

        // برای SpecialOccasion
        public int? SpecialOccasionId { get; set; }

        // برای Custom Automation
        public string? ActivationConditions { get; set; }

        // زمان ارسال برنامه‌ریزی شده (ساعت و دقیقه در روز - برای Birthday و سایر اتوماسیون‌ها)
        public TimeSpan? ScheduledTime { get; set; }

        public string? Icon { get; set; }
    }
}


