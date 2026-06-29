using Api_Vapp.Constants;
using Api_Vapp.Data;
using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.Subscription;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;
using Api_Vapp.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api_Vapp.Services
{
    public class UserSubscriptionService : IUserSubscriptionService
    {
        private readonly Api_Context _context;
        private readonly ISubscriptionEntitlementService _entitlementService;
        private readonly ILogger<UserSubscriptionService> _logger;

        public UserSubscriptionService(
            Api_Context context,
            ISubscriptionEntitlementService entitlementService,
            ILogger<UserSubscriptionService> logger)
        {
            _context = context;
            _entitlementService = entitlementService;
            _logger = logger;
        }

        public async Task<ApiResponse<SubscriptionCatalogDto>> GetCatalogAsync(int userId)
        {
            try
            {
                var entitlement = await _entitlementService.GetEntitlementSnapshotAsync(userId);

                var plans = await _context.SubscriptionPlans
                    .AsNoTracking()
                    .Include(p => p.PlanFeatures)
                        .ThenInclude(pf => pf.Feature)
                    .Where(p => p.IsActive && !p.IsDeleted)
                    .OrderBy(p => p.SortOrder)
                    .ThenBy(p => p.Id)
                    .ToListAsync();

                var catalog = new SubscriptionCatalogDto
                {
                    DurationLabel = "اشتراک ۳۰ روزه",
                    CurrentSubscription = MapCurrentSubscription(
                        entitlement.ActiveSubscription,
                        entitlement.EffectivePlan,
                        entitlement.FeatureCodes),
                    Plans = plans.Select(plan => MapPlanItem(plan, entitlement.EffectivePlan.Id)).ToList()
                };

                return ApiResponse<SubscriptionCatalogDto>.CreateSuccess(catalog);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading subscription catalog for user {UserId}", userId);
                return ApiResponse<SubscriptionCatalogDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        private static CurrentSubscriptionDto MapCurrentSubscription(
            UserSubscription? activeSubscription,
            SubscriptionPlan effectivePlan,
            IReadOnlyList<string> featureCodes)
        {
            var isFree = IsFreePlan(effectivePlan);
            var expiresAt = activeSubscription?.ExpiresAt;
            var remainingDays = expiresAt.HasValue
                ? Math.Max(0, (int)Math.Ceiling((expiresAt.Value - DateTime.UtcNow).TotalDays))
                : (int?)null;

            return new CurrentSubscriptionDto
            {
                UserSubscriptionId = activeSubscription?.Id,
                PlanId = effectivePlan.Id,
                PlanName = effectivePlan.Name,
                TierCode = effectivePlan.TierCode,
                StartDate = activeSubscription?.StartDate,
                ExpiresAt = expiresAt,
                RemainingDays = isFree ? null : remainingDays,
                IsActive = activeSubscription != null || isFree,
                IsFreePlan = isFree,
                FeatureCodes = featureCodes.ToList()
            };
        }

        private static SubscriptionPlanCatalogItemDto MapPlanItem(SubscriptionPlan plan, int currentPlanId)
        {
            var isFree = IsFreePlan(plan);
            var isCurrent = plan.Id == currentPlanId;
            var features = plan.PlanFeatures
                .Where(pf => pf.Feature is { IsActive: true, IsDeleted: false })
                .Select(pf => pf.Feature!)
                .OrderBy(f => f.SortOrder)
                .ThenBy(f => f.Id)
                .Select(f => new SubscriptionFeatureCatalogItemDto
                {
                    Code = f.Code,
                    Name = f.Name,
                    Description = f.Description
                })
                .ToList();

            return new SubscriptionPlanCatalogItemDto
            {
                Id = plan.Id,
                Name = plan.Name,
                TierCode = plan.TierCode,
                Description = plan.Description,
                Price = plan.Price,
                FormattedPrice = FormatPrice(plan.Price),
                DurationDays = plan.DurationDays,
                IsCurrentPlan = isCurrent,
                IsFree = isFree,
                CanPurchase = !isFree && !isCurrent,
                Features = features
            };
        }

        private static bool IsFreePlan(SubscriptionPlan plan) =>
            plan.Price <= 0
            || string.Equals(plan.TierCode, SubscriptionPlanTierCodes.Free, StringComparison.OrdinalIgnoreCase);

        private static string FormatPrice(decimal price) =>
            price <= 0 ? "رایگان" : $"{price:N0} تومان";
    }
}
