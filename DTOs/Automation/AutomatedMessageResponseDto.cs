namespace Api_Vapp.DTOs.Automation
{
    /// <summary>
    /// DTO برای پاسخ پیام خودکار
    /// </summary>
    public class AutomatedMessageResponseDto
    {
        public int Id { get; set; }
        public string AutomationType { get; set; } = string.Empty;
        public string? Title { get; set; }
        public string? Description { get; set; }
        public int? MessageId { get; set; }
        public string? MessageContent { get; set; }
        public string? Icon { get; set; }
        public string Status { get; set; } = "Draft";
        public bool IsActive { get; set; }
        public DateTime? LastExecutedAt { get; set; }
        public int? DaysBeforeEvent { get; set; }
        public TimeSpan? ScheduledTime { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}


