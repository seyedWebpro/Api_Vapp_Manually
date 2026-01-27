namespace Api_Vapp.DTOs.Automation
{
    /// <summary>
    /// DTO برای لیست پیام‌های خودکار
    /// </summary>
    public class AutomatedMessageListResponseDto
    {
        public List<AutomatedMessageResponseDto> AutomatedMessages { get; set; } = new List<AutomatedMessageResponseDto>();
        public int TotalCount { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
    }
}


