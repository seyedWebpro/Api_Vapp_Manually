namespace Api_Vapp.DTOs.QuickAction
{
    /// <summary>
    /// DTO برای نمایش اقدام سریع
    /// </summary>
    public class QuickActionResponseDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? ActionType { get; set; }
        public string? Content { get; set; }
        public string? Icon { get; set; }
        public int DisplayOrder { get; set; }
        public bool IsActive { get; set; }
        public bool IsDefault { get; set; }
        public DateTime CreatedAt { get; set; }
        
        /// <summary>
        /// نوع لینک تشخیص داده شده بر اساس ساختار URL
        /// مقادیر ممکن: Instagram, Telegram, WhatsApp, LinkedIn, Twitter, YouTube, Facebook, TikTok, Snapchat, Website, Unknown
        /// </summary>
        public string? LinkType { get; set; }
    }
}












