namespace Api_Vapp.Models
{
    /// <summary>
    /// کد تخفیف مخصوص خرید اشتراک
    /// </summary>
    public class SubscriptionDiscountCode
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string? Title { get; set; }
        public string DiscountType { get; set; } = SubscriptionDiscountTypes.Fixed;
        public decimal Value { get; set; }
        public decimal? MaxDiscountAmount { get; set; }
        public decimal? MinOrderAmount { get; set; }
        public int? SubscriptionPlanId { get; set; }
        public int? MaxTotalUses { get; set; }
        public int UsedCount { get; set; }
        public int? MaxUsesPerUser { get; set; }
        public DateTime? ValidFrom { get; set; }
        public DateTime? ValidUntil { get; set; }
        public bool IsActive { get; set; } = true;
        public bool IsDeleted { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        public virtual SubscriptionPlan? SubscriptionPlan { get; set; }
        public virtual ICollection<SubscriptionDiscountUsage> Usages { get; set; } = new List<SubscriptionDiscountUsage>();
    }

    public static class SubscriptionDiscountTypes
    {
        public const string Percentage = "Percentage";
        public const string Fixed = "Fixed";
    }
}
