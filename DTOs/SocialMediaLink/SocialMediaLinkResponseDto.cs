namespace Api_Vapp.DTOs.SocialMediaLink
{
    /// <summary>
    /// DTO برای نمایش لینک شبکه‌های اجتماعی
    /// </summary>
    public class SocialMediaLinkResponseDto
    {
        public int Id { get; set; }
        public string Platform { get; set; } = string.Empty;
        public string LinkUrl { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public bool IsDefault { get; set; }
        public DateTime CreatedAt { get; set; }

        /// <summary>Pending / Approved / Rejected — تأیید یک‌باره ارسال سریع</summary>
        public string ApprovalStatus { get; set; } = "Pending";

        public string? RejectionReason { get; set; }

        public DateTime? ApprovedAt { get; set; }
        
        /// <summary>
        /// نوع لینک تشخیص داده شده بر اساس ساختار URL
        /// مقادیر ممکن: Instagram, Telegram, WhatsApp, LinkedIn, Twitter, YouTube, Facebook, TikTok, Snapchat, Rubika, Soroush, Eitaa, Bale, Website, Unknown
        /// </summary>
        public string? LinkType { get; set; }
    }
}





