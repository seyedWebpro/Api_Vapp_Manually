namespace Api_Vapp.Models
{
    public class SubscriptionPlanFeature
    {
        public int SubscriptionPlanId { get; set; }
        public int SubscriptionFeatureId { get; set; }

        public virtual SubscriptionPlan Plan { get; set; } = null!;
        public virtual SubscriptionFeature Feature { get; set; } = null!;
    }
}
