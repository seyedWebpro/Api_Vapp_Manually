namespace Api_Vapp.Models
{
    public class SubscriptionPaymentMetadata
    {
        public int SubscriptionPlanId { get; set; }
        public string PlanName { get; set; } = string.Empty;
        public string TierCode { get; set; } = string.Empty;
        public decimal OriginalAmount { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal PayableAmount { get; set; }
        public string? DiscountCode { get; set; }
        public int? DiscountCodeId { get; set; }
        public int DurationDays { get; set; }
    }
}
