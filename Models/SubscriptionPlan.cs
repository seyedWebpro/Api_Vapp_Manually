namespace Api_Vapp.Models
{
    /// <summary>
    /// پلن اشتراک (طلایی، برنزی، ...)
    /// </summary>
    public class SubscriptionPlan
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string TierCode { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal Price { get; set; }
        public int DurationDays { get; set; } = 30;
        public bool FreeQuickSendEnabled { get; set; }
        public bool BusinessCardEnabled { get; set; }
        public int? MonthlySmsLimit { get; set; }
        public int SortOrder { get; set; }
        public bool IsActive { get; set; } = true;
        public bool IsDeleted { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        public virtual ICollection<UserSubscription> UserSubscriptions { get; set; } = new List<UserSubscription>();
    }
}
