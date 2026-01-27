namespace Api_Vapp.DTOs.Automation
{
    /// <summary>
    /// DTO برای پاسخ مدیریت مناسبت‌های خاص
    /// </summary>
    public class SpecialOccasionManagementResponseDto
    {
        public int AutomatedMessageId { get; set; }
        public List<SpecialOccasionItemDto> Occasions { get; set; } = new List<SpecialOccasionItemDto>();
    }

    public class SpecialOccasionItemDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public int? DaysRemaining { get; set; }
    }
}

