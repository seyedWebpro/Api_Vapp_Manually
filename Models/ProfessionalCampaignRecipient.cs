namespace Api_Vapp.Models
{
    public class ProfessionalCampaignRecipient
    {
        public int Id { get; set; }
        public int ProfessionalCampaignId { get; set; }
        public int? ContactId { get; set; }
        public string MobileNumber { get; set; } = string.Empty;
        public string? FullName { get; set; }
        public bool IsDeleted { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        public virtual ProfessionalCampaign ProfessionalCampaign { get; set; } = null!;
        public virtual Contact? Contact { get; set; }
    }
}
