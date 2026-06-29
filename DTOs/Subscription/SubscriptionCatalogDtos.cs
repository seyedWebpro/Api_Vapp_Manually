namespace Api_Vapp.DTOs.Subscription
{
    public class SubscriptionCatalogDto
    {
        public CurrentSubscriptionDto CurrentSubscription { get; set; } = new();
        public string DurationLabel { get; set; } = "اشتراک ۳۰ روزه";
        public List<SubscriptionPlanCatalogItemDto> Plans { get; set; } = new();
    }

    public class CurrentSubscriptionDto
    {
        public int? UserSubscriptionId { get; set; }
        public int PlanId { get; set; }
        public string PlanName { get; set; } = string.Empty;
        public string TierCode { get; set; } = string.Empty;
        public DateTime? StartDate { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public int? RemainingDays { get; set; }
        public bool IsActive { get; set; }
        public bool IsFreePlan { get; set; }
        public List<string> FeatureCodes { get; set; } = new();
    }

    public class SubscriptionPlanCatalogItemDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string TierCode { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal Price { get; set; }
        public string FormattedPrice { get; set; } = string.Empty;
        public int DurationDays { get; set; }
        public bool IsCurrentPlan { get; set; }
        public bool IsFree { get; set; }
        public bool CanPurchase { get; set; }
        public List<SubscriptionFeatureCatalogItemDto> Features { get; set; } = new();
    }

    public class SubscriptionFeatureCatalogItemDto
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
    }
}
