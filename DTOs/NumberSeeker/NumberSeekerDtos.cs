using System.ComponentModel.DataAnnotations;

namespace Api_Vapp.DTOs.NumberSeeker
{
    public class StartNumberSeekerScrapeDto
    {
        [Required(ErrorMessage = "منبع اسکرپ الزامی است")]
        [RegularExpression(
            "^(sheypoor|divar|nshan|balad|googlemaps)$",
            ErrorMessage = "منبع نامعتبر است. مجاز: sheypoor, divar, nshan, balad, googlemaps")]
        public string Source { get; set; } = string.Empty;

        [Required(ErrorMessage = "شهر الزامی است")]
        [StringLength(100, MinimumLength = 1)]
        public string City { get; set; } = "تهران";

        [Required(ErrorMessage = "دسته‌بندی الزامی است")]
        [StringLength(200, MinimumLength = 1)]
        public string Category { get; set; } = string.Empty;

        [Range(1, 1000, ErrorMessage = "تعداد شماره باید بین ۱ تا ۱۰۰۰ باشد")]
        public int MaxPhones { get; set; } = 50;

        public bool? Headless { get; set; }
    }

    public class NumberSeekerTaskCreatedDto
    {
        public string TaskId { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string PollUrl { get; set; } = string.Empty;
        public int? QueuePosition { get; set; }
    }

    public class NumberSeekerTaskStatusDto
    {
        public string TaskId { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public int TargetCount { get; set; }
        public int CurrentCount { get; set; }
        public double ProgressPercent { get; set; }
        public List<string> Phones { get; set; } = new();
        public int? PhonesPreviewLimit { get; set; }
        public string? Message { get; set; }
        public string? ResultCode { get; set; }
        public int PhonesSaved { get; set; }
        public int PhonesDuplicates { get; set; }
        public string? Error { get; set; }
        public string? StartedAt { get; set; }
        public string? CompletedAt { get; set; }
        public double? ElapsedSeconds { get; set; }
        public int? QueuePosition { get; set; }
    }

    public class NumberSeekerTaskSummaryDto
    {
        public string TaskId { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public int CurrentCount { get; set; }
        public int TargetCount { get; set; }
        public double ProgressPercent { get; set; }
        public string? StartedAt { get; set; }
        public string? CreatedAt { get; set; }
        public string? ImportedAt { get; set; }
        public int ImportedCount { get; set; }
    }

    public class NumberSeekerTaskListDto
    {
        public int Count { get; set; }
        public List<NumberSeekerTaskSummaryDto> Tasks { get; set; } = new();
    }

    public class NumberSeekerHealthDto
    {
        public string Status { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string Database { get; set; } = string.Empty;
        public int ActiveTasks { get; set; }
        public int QueuePending { get; set; }
        public string? QueueRunningTaskId { get; set; }
        public double? UptimeSeconds { get; set; }
        public string Timestamp { get; set; } = string.Empty;
        public bool ScraperReachable { get; set; }
    }

    public class NumberSeekerSourcesDto
    {
        public List<NumberSeekerSourceInfoDto> Sources { get; set; } = new();
    }

    public class NumberSeekerSourceInfoDto
    {
        public string Code { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
    }

    public class NumberSeekerCancelResultDto
    {
        public string TaskId { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }

    public class ImportNumberSeekerPhonesDto
    {
        [Required(ErrorMessage = "شناسه دفترچه الزامی است")]
        public int ContactNotebookId { get; set; }

        /// <summary>پیشوند نام مخاطب — مثلاً «رستوران» → «رستوران ۱»</summary>
        [StringLength(100)]
        public string? ContactNamePrefix { get; set; }

        /// <summary>اگر قبلاً import شده، با true دوباره import می‌شود</summary>
        public bool Force { get; set; }
    }

    public class NumberSeekerImportResultDto
    {
        public string TaskId { get; set; } = string.Empty;
        public int ContactNotebookId { get; set; }
        public int TotalPhones { get; set; }
        public int SuccessCount { get; set; }
        public int DuplicateCount { get; set; }
        public int SkippedCount { get; set; }
        public int ErrorCount { get; set; }
        public List<ImportRowErrorDto> Errors { get; set; } = new();
        public DateTime ImportedAt { get; set; }
    }

    public class ImportRowErrorDto
    {
        public int RowNumber { get; set; }
        public string? MobileNumber { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
    }

    public class NumberSeekerWebhookDto
    {
        public string TaskId { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public int CurrentCount { get; set; }
        public string? ResultCode { get; set; }
        public string? Message { get; set; }
    }
}
