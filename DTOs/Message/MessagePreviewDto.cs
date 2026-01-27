namespace Api_Vapp.DTOs.Message
{
    /// <summary>
    /// DTO برای پیش‌نمایش پیام
    /// </summary>
    public class MessagePreviewDto
    {
        public string OriginalContent { get; set; } = string.Empty;
        public string PreviewContent { get; set; } = string.Empty;
        public Dictionary<string, string> SamplePlaceholders { get; set; } = new Dictionary<string, string>();
    }
}


