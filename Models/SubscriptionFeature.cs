namespace Api_Vapp.Models
{
    /// <summary>
    /// امکانات قابل انتخاب برای پلن‌های اشتراک
    /// </summary>
    public class SubscriptionFeature
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int SortOrder { get; set; }
        public bool IsActive { get; set; } = true;
        public bool IsDeleted { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        public virtual ICollection<SubscriptionPlanFeature> PlanFeatures { get; set; } = new List<SubscriptionPlanFeature>();
    }
}
