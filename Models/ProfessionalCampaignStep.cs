namespace Api_Vapp.Models
{
    public class ProfessionalCampaignStep
    {
        public int Id { get; set; }
        public int ProfessionalCampaignId { get; set; }
        public int StepOrder { get; set; }
        public string Content { get; set; } = string.Empty;
        public int DelayAfterPreviousMinutes { get; set; }
        public DateTime? ScheduledAtUtc { get; set; }
        public string Status { get; set; } = "PendingApproval";
        public string ApprovalStatus { get; set; } = "Pending";
        public int? ReviewedByUserId { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public string? RejectionReason { get; set; }
        public int SentCount { get; set; }
        public int FailedCount { get; set; }
        public string? LastError { get; set; }
        public DateTime? SentAtUtc { get; set; }
        public bool IsDeleted { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        public virtual ProfessionalCampaign ProfessionalCampaign { get; set; } = null!;
        public virtual User? ReviewedByUser { get; set; }
    }
}
