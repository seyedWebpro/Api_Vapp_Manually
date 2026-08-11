namespace Api_Vapp.DTOs.Message
{
    /// <summary>
    /// DTO برای پاسخ پیام
    /// </summary>
    public class MessageResponseDto
    {
        public int Id { get; set; }
        public string Content { get; set; } = string.Empty;
        public int CharacterCount { get; set; }
        public int PartsCount { get; set; }
        public bool IsPersonalized { get; set; }
        public List<string>? Placeholders { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? AdminApprovalStatus { get; set; }
        public string? AdminRejectionReason { get; set; }
        public DateTime? AdminReviewedAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}


