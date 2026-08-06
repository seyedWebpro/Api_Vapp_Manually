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
        public string SourceDisplayName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string StatusDisplayName { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string PollUrl { get; set; } = string.Empty;
        public int? QueuePosition { get; set; }
    }

    /// <summary>وضعیت زنده تسک — صفحه «در حال جستجو» و «نتایج»</summary>
    public class NumberSeekerTaskStatusDto
    {
        public string TaskId { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public string SourceDisplayName { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;

        /// <summary>مثلاً «تهران - کافه رستوران»</summary>
        public string Subtitle { get; set; } = string.Empty;

        public string Status { get; set; } = string.Empty;
        public string StatusDisplayName { get; set; } = string.Empty;

        /// <summary>success | warning | danger | info</summary>
        public string StatusTone { get; set; } = "info";

        public bool IsTerminal { get; set; }
        public bool IsRunning { get; set; }
        public bool CanCancel { get; set; }
        public bool CanImport { get; set; }
        public bool CanDownload { get; set; }

        public int TargetCount { get; set; }
        public int CurrentCount { get; set; }
        public double ProgressPercent { get; set; }

        /// <summary>مثلاً «۳۸ از ۵۰ شماره»</summary>
        public string ProgressLabel { get; set; } = string.Empty;

        /// <summary>لیست کامل — در حالت running معمولاً خالی/محدود؛ در completed پر</summary>
        public List<string> Phones { get; set; } = new();

        /// <summary>پیش‌نمایش حداکثر ۲۰ شماره — برای صفحه در حال جستجو</summary>
        public List<string> PhonesPreview { get; set; } = new();

        public int PhonesPreviewLimit { get; set; } = 20;

        public string? Message { get; set; }
        public string? ResultCode { get; set; }
        public int PhonesSaved { get; set; }
        public int PhonesDuplicates { get; set; }
        public string? Error { get; set; }
        public string? StartedAt { get; set; }
        public string? CompletedAt { get; set; }
        public string? CreatedAtPersian { get; set; }
        public double? ElapsedSeconds { get; set; }
        public int? QueuePosition { get; set; }

        /// <summary>ثانیه باقی‌مانده تقریبی</summary>
        public int? EstimatedSecondsRemaining { get; set; }

        /// <summary>مثلاً «حدود ۲ دقیقه دیگر»</summary>
        public string? EstimatedRemainingText { get; set; }

        /// <summary>عنوان صفحه نتایج / وضعیت</summary>
        public string ResultTitle { get; set; } = string.Empty;

        /// <summary>مثلاً «۴۵ شماره یافت شد»</summary>
        public string ResultCountLabel { get; set; } = string.Empty;
    }

    /// <summary>آیتم تاریخچه صفحه اول</summary>
    public class NumberSeekerTaskSummaryDto
    {
        public string TaskId { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public string SourceDisplayName { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;

        /// <summary>«تهران - رستوران»</summary>
        public string Subtitle { get; set; } = string.Empty;

        public string Status { get; set; } = string.Empty;
        public string StatusDisplayName { get; set; } = string.Empty;
        public string StatusTone { get; set; } = "info";

        public int CurrentCount { get; set; }
        public int TargetCount { get; set; }
        public double ProgressPercent { get; set; }

        /// <summary>«۸۲/۸۲»</summary>
        public string CountLabel { get; set; } = string.Empty;

        public string? StartedAt { get; set; }
        public string? CreatedAt { get; set; }

        /// <summary>تاریخ شمسی مثل ۱۴۰۵/۰۸/۲۸</summary>
        public string? CreatedAtPersian { get; set; }

        public string? CompletedAt { get; set; }
        public string? CompletedAtPersian { get; set; }
        public string? ImportedAt { get; set; }
        public int ImportedCount { get; set; }

        public bool CanDownload { get; set; }
        public bool CanImport { get; set; }
        public bool IsTerminal { get; set; }
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
        public bool ApiKeyValid { get; set; }
        public bool ApiKeyConfigured { get; set; }
        public bool WebhookConfigured { get; set; }
        public bool NeshanApiKeyConfigured { get; set; }
        public bool IntegrationReady { get; set; }
        public bool TokensReady { get; set; }
        public int TokenAlertsCount { get; set; }
        public List<NumberSeekerTokenAlertDto> TokenAlerts { get; set; } = new();
        public Dictionary<string, NumberSeekerPlatformTokenDto> PlatformTokens { get; set; } = new();
    }

    public class NumberSeekerTokenAlertDto
    {
        public string Platform { get; set; } = string.Empty;
        public string Level { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }

    public class NumberSeekerPlatformTokenDto
    {
        public bool Configured { get; set; }
        public bool Ready { get; set; }
        public bool? IsExpired { get; set; }
        public int? DaysRemaining { get; set; }
        public string AlertLevel { get; set; } = "none";
    }

    public class NumberSeekerSourcesDto
    {
        public List<NumberSeekerSourceInfoDto> Sources { get; set; } = new();
    }

    public class NumberSeekerSourceInfoDto
    {
        public string Code { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>کلید آیکن سمت کلاینت: divar | googlemaps | sheypoor | nshan | balad</summary>
        public string IconKey { get; set; } = string.Empty;

        public int SortOrder { get; set; }
        public bool Enabled { get; set; } = true;
    }

    public class NumberSeekerCitiesDto
    {
        public List<NumberSeekerCityDto> Cities { get; set; } = new();
        public string DefaultCity { get; set; } = "تهران";
    }

    public class NumberSeekerCityDto
    {
        public string Name { get; set; } = string.Empty;
        public int SortOrder { get; set; }
    }

    public class NumberSeekerCategoriesDto
    {
        public List<NumberSeekerCategoryDto> Categories { get; set; } = new();
        public string Placeholder { get; set; } = "مثال : کافه - رستوران و ...";
    }

    public class NumberSeekerCategoryDto
    {
        public string Name { get; set; } = string.Empty;
        public int SortOrder { get; set; }
    }

    /// <summary>همه دادهٔ لازم صفحه «جستجوی جدید» در یک درخواست</summary>
    public class NumberSeekerFormMetaDto
    {
        public List<NumberSeekerSourceInfoDto> Sources { get; set; } = new();
        public List<NumberSeekerCityDto> Cities { get; set; } = new();
        public List<NumberSeekerCategoryDto> Categories { get; set; } = new();
        public string DefaultCity { get; set; } = "تهران";
        public string CategoryPlaceholder { get; set; } = "مثال : کافه - رستوران و ...";
        public int MinPhones { get; set; } = 1;
        public int MaxPhones { get; set; } = 1000;
        public int DefaultPhones { get; set; } = 50;
    }

    public class NumberSeekerCancelResultDto
    {
        public string TaskId { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Status { get; set; } = "cancelled";
        public string StatusDisplayName { get; set; } = "لغو شد";
    }

    public class ImportNumberSeekerPhonesDto
    {
        [Required(ErrorMessage = "شناسه دفترچه الزامی است")]
        public int ContactNotebookId { get; set; }

        [StringLength(100)]
        public string? ContactNamePrefix { get; set; }

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
        public List<string> Phones { get; set; } = new();
    }

    /// <summary>دانلود شماره‌ها (JSON) — برای آیکن دانلود تاریخچه</summary>
    public class NumberSeekerExportDto
    {
        public string TaskId { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public string SourceDisplayName { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public int Count { get; set; }
        public List<string> Phones { get; set; } = new();
        public string Format { get; set; } = "json";
        /// <summary>متن آماده کپی — هر شماره یک خط</summary>
        public string TextContent { get; set; } = string.Empty;
    }
}
