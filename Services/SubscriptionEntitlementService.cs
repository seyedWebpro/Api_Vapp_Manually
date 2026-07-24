using Api_Vapp.Constants;
using Api_Vapp.Data;
using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.Subscription;
using Api_Vapp.Exceptions;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;
using Api_Vapp.Utilities;
using Microsoft.EntityFrameworkCore;

namespace Api_Vapp.Services
{
    public class SubscriptionEntitlementService : ISubscriptionEntitlementService
    {
        private readonly Api_Context _context;

        public SubscriptionEntitlementService(Api_Context context)
        {
            _context = context;
        }

        public async Task<UserSubscriptionEntitlementSnapshot> GetEntitlementSnapshotAsync(int userId)
        {
            var active = await GetActiveSubscriptionAsync(userId);
            // IsActive فقط فروش/کاتالوگ را کنترل می‌کند؛ اشتراک فعال کاربر قطع نمی‌شود.
            if (active?.Plan is { IsDeleted: false })
            {
                return new UserSubscriptionEntitlementSnapshot
                {
                    ActiveSubscription = active,
                    EffectivePlan = active.Plan,
                    FeatureCodes = ExtractFeatureCodes(active.Plan)
                };
            }

            var freePlan = await GetDefaultFreePlanAsync();
            return new UserSubscriptionEntitlementSnapshot
            {
                ActiveSubscription = null,
                EffectivePlan = freePlan,
                FeatureCodes = ExtractFeatureCodes(freePlan)
            };
        }

        public async Task<UserSubscription?> GetActiveSubscriptionAsync(int userId)
        {
            return await _context.UserSubscriptions
                .AsNoTracking()
                .Include(us => us.Plan)
                    .ThenInclude(p => p.PlanFeatures)
                    .ThenInclude(pf => pf.Feature)
                .Where(us =>
                    us.UserId == userId
                    && !us.IsDeleted
                    && us.Status == "Active"
                    && us.ExpiresAt > DateTime.UtcNow)
                .OrderByDescending(us => us.ExpiresAt)
                .FirstOrDefaultAsync();
        }

        public async Task<SubscriptionPlan> GetEffectivePlanAsync(int userId)
        {
            var snapshot = await GetEntitlementSnapshotAsync(userId);
            return snapshot.EffectivePlan;
        }

        public async Task<IReadOnlyList<string>> GetFeatureCodesAsync(int userId)
        {
            var snapshot = await GetEntitlementSnapshotAsync(userId);
            return snapshot.FeatureCodes;
        }

        public async Task<bool> HasFeatureAsync(int userId, string featureCode)
        {
            if (!SubscriptionFeatureCodes.IsKnown(featureCode))
                return false;

            var normalizedCode = featureCode.Trim();
            var now = DateTime.UtcNow;

            var hasViaActiveSubscription = await _context.UserSubscriptions
                .AsNoTracking()
                .Where(us =>
                    us.UserId == userId
                    && !us.IsDeleted
                    && us.Status == "Active"
                    && us.ExpiresAt > now
                    && !us.Plan.IsDeleted)
                .SelectMany(us => us.Plan.PlanFeatures)
                .AnyAsync(pf =>
                    pf.Feature!.Code == normalizedCode
                    && !pf.Feature.IsDeleted);

            if (hasViaActiveSubscription)
                return true;

            return await _context.SubscriptionPlans
                .AsNoTracking()
                .Where(p =>
                    p.TierCode == SubscriptionPlanTierCodes.Free
                    && p.IsActive
                    && !p.IsDeleted)
                .SelectMany(p => p.PlanFeatures)
                .AnyAsync(pf =>
                    pf.Feature!.Code == normalizedCode
                    && !pf.Feature.IsDeleted);
        }

        private async Task<SubscriptionPlan> GetDefaultFreePlanAsync()
        {
            var freePlan = await _context.SubscriptionPlans
                .AsNoTracking()
                .Include(p => p.PlanFeatures)
                    .ThenInclude(pf => pf.Feature)
                .Where(p => p.TierCode == SubscriptionPlanTierCodes.Free && p.IsActive && !p.IsDeleted)
                .OrderBy(p => p.SortOrder)
                .FirstOrDefaultAsync();

            if (freePlan != null)
                return freePlan;

            throw AppException.Internal(ErrorCodes.Unexpected, SubscriptionMessages.FreePlanNotConfigured);
        }

        private static IReadOnlyList<string> ExtractFeatureCodes(SubscriptionPlan plan) =>
            plan.PlanFeatures
                .Where(pf => pf.Feature is { IsDeleted: false })
                .Select(pf => pf.Feature!.Code)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
    }
}
