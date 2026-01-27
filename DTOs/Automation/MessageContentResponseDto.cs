namespace Api_Vapp.DTOs.Automation
{
    /// <summary>
    /// DTO برای پاسخ ذخیره محتوای پیام خودکار
    /// </summary>
    public class MessageContentResponseDto
    {
        public int AutomatedMessageId { get; set; }
        public int MessageId { get; set; }
        public string Content { get; set; } = string.Empty;
        public int CharacterCount { get; set; }
        public int PartsCount { get; set; }
        public bool IsPersonalized { get; set; }
        public string? Placeholders { get; set; }
    }
}

    