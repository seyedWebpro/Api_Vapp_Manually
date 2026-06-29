using Api_Vapp.DTOs.Subscription;
using Api_Vapp.Models;

namespace Api_Vapp.Interfaces
{
    public interface ISubscriptionEntitlementService
    {
        Task<UserSubscriptionEntitlementSnapshot> GetEntitlementSnapshotAsync(int userId);
        Task<UserSubscription?> GetActiveSubscriptionAsync(int userId);
        Task<SubscriptionPlan> GetEffectivePlanAsync(int userId);
        Task<IReadOnlyList<string>> GetFeatureCodesAsync(int userId);
        Task<bool> HasFeatureAsync(int userId, string featureCode);
    }
}
