namespace Api_Vapp.Models
{
    public class ProfessionalCampaign
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string TargetType { get; set; } = string.Empty;
        public string TargetIdsJson { get; set; } = "[]";
        public string Status { get; set; } = "PendingApproval";
        public DateTime? StartAtUtc { get; set; }
        public int RecipientsCount { get; set; }
        public bool IsActive { get; set; }
        public bool IsDeleted { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        public virtual User User { get; set; } = null!;
        public virtual ICollection<ProfessionalCampaignStep> Steps { get; set; } = new List<ProfessionalCampaignStep>();
        public virtual ICollection<ProfessionalCampaignRecipient> Recipients { get; set; } = new List<ProfessionalCampaignRecipient>();
    }
}
