namespace Api_Vapp.DTOs.Message
{
    /// <summary>
    /// DTO برای پاسخ شخصی‌سازی پیام
    /// </summary>
    public class PersonalizedMessageResponseDto
    {
        public string OriginalContent { get; set; } = string.Empty;
        public string PersonalizedContent { get; set; } = string.Empty;
        public Dictionary<string, string> UsedPlaceholders { get; set; } = new Dictionary<string, string>();
    }
}



