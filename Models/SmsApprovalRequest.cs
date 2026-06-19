namespace Api_Vapp.Models
{
    /// <summary>
    /// درخواست تأیید ارسال SMS توسط ادمین
    /// </summary>
    public class SmsApprovalRequest
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string RequestType { get; set; } = string.Empty;
        public int? MessageCampaignId { get; set; }
        public int MessageId { get; set; }
        public int? MessageSessionId { get; set; }
        public string ContentPreview { get; set; } = string.Empty;
        public string? TitlePreview { get; set; }
        public int RecipientsCount { get; set; }
        public string Status { get; set; } = "Pending";
        public int? ReviewedByUserId { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public string? RejectionReason { get; set; }
        public string? SendPayloadJson { get; set; }
        public bool IsDeleted { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        public virtual User User { get; set; } = null!;
        public virtual MessageCampaign? MessageCampaign { get; set; }
        public virtual Message Message { get; set; } = null!;
        public virtual MessageSession? MessageSession { get; set; }
        public virtual User? ReviewedByUser { get; set; }
    }
}
