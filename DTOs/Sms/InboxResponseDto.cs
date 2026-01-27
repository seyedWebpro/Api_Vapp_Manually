namespace Api_Vapp.DTOs.Sms
{
    /// <summary>
    /// DTO برای پاسخ دریافت پیامک‌های ورودی (Inbox)
    /// </summary>
    public class InboxResponseDto
    {
        public List<InboxItemDto> Inboxs { get; set; } = new List<InboxItemDto>();
        public string Message { get; set; } = string.Empty;
        public int Status { get; set; }
    }

    /// <summary>
    /// DTO برای هر آیتم Inbox
    /// </summary>
    public class InboxItemDto
    {
        public long Id { get; set; }
        public string SenderNumber { get; set; } = string.Empty;
        public string Mobile { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public DateTime Date { get; set; }
    }
}



