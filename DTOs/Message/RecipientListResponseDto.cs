namespace Api_Vapp.DTOs.Message
{
    /// <summary>
    /// DTO برای لیست گیرندگان انتخاب شده
    /// </summary>
    public class RecipientListResponseDto
    {
        public List<RecipientItemDto> Recipients { get; set; } = new List<RecipientItemDto>();
        public int TotalCount { get; set; }
        
        // شناسه Session (فقط اگر MessageId در SelectRecipientsDto ارسال شده باشد)
        public int? SessionId { get; set; }
    }

    public class RecipientItemDto
    {
        public int? ContactId { get; set; }
        public string MobileNumber { get; set; } = string.Empty;
        public string? FullName { get; set; }
    }
}


