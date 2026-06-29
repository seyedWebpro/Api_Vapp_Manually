using Api_Vapp.Models;

namespace Api_Vapp.DTOs.Subscription
{
    public class UserSubscriptionEntitlementSnapshot
    {
        public UserSubscription? ActiveSubscription { get; init; }
        public SubscriptionPlan EffectivePlan { get; init; } = null!;
        public IReadOnlyList<string> FeatureCodes { get; init; } = Array.Empty<string>();
    }
}
