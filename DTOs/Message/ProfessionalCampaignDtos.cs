using System.ComponentModel.DataAnnotations;

namespace Api_Vapp.DTOs.Message
{
    public class CreateProfessionalCampaignDto
    {
        [Required(ErrorMessage = "عنوان کمپین الزامی است")]
        [StringLength(200, ErrorMessage = "عنوان کمپین حداکثر ۲۰۰ کاراکتر است")]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "نوع مخاطبان الزامی است")]
        public string TargetType { get; set; } = string.Empty;

        [MinLength(1, ErrorMessage = "حداقل یک دفترچه یا تگ باید انتخاب شود")]
        public List<int> TargetIds { get; set; } = new();

        /// <summary>زمان شروع با offset صریح؛ در سرور به UTC تبدیل می‌شود.</summary>
        public DateTimeOffset? StartAt { get; set; }

        [Required(ErrorMessage = "مراحل کمپین الزامی است")]
        [MinLength(2, ErrorMessage = "کمپین حرفه‌ای باید حداقل دو پیام داشته باشد")]
        [MaxLength(20, ErrorMessage = "هر کمپین حداکثر ۲۰ پیام می‌تواند داشته باشد")]
        public List<ProfessionalCampaignStepInputDto> Steps { get; set; } = new();
    }

    public class ProfessionalCampaignStepInputDto
    {
        [Required(ErrorMessage = "متن پیام الزامی است")]
        [StringLength(4000, ErrorMessage = "متن پیام بیش از حد مجاز است")]
        public string Content { get; set; } = string.Empty;

        [Range(0, 3650, ErrorMessage = "تعداد روز تأخیر نامعتبر است")]
        public int DelayDays { get; set; }

        [Range(0, 23, ErrorMessage = "ساعت تأخیر باید بین صفر تا ۲۳ باشد")]
        public int DelayHours { get; set; }

        [Range(0, 59, ErrorMessage = "دقیقه تأخیر باید بین صفر تا ۵۹ باشد")]
        public int DelayMinutes { get; set; }
    }

    public class ProfessionalCampaignStepResponseDto
    {
        public int Id { get; set; }
        public int StepOrder { get; set; }
        public string Content { get; set; } = string.Empty;
        public int DelayAfterPreviousMinutes { get; set; }
        public DateTime? ScheduledAtUtc { get; set; }
        public string Status { get; set; } = string.Empty;
        public string ApprovalStatus { get; set; } = string.Empty;
        public string? RejectionReason { get; set; }
        public int SentCount { get; set; }
        public int FailedCount { get; set; }
        public DateTime? SentAtUtc { get; set; }
    }

    public class ProfessionalCampaignResponseDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string TargetType { get; set; } = string.Empty;
        public List<int> TargetIds { get; set; } = new();
        public string Status { get; set; } = string.Empty;
        public DateTime? StartAtUtc { get; set; }
        public int RecipientsCount { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<ProfessionalCampaignStepResponseDto> Steps { get; set; } = new();
    }

    public class ProfessionalCampaignListResponseDto
    {
        public List<ProfessionalCampaignResponseDto> Items { get; set; } = new();
        public int TotalCount { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
    }
}
