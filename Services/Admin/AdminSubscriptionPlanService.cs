using Api_Vapp.Constants;
using Api_Vapp.Data;
using Api_Vapp.DTOs.Admin;
using Api_Vapp.DTOs.Common;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;
using Microsoft.EntityFrameworkCore;

namespace Api_Vapp.Services.Admin
{
    public class AdminSubscriptionPlanService : IAdminSubscriptionPlanService
    {
        private readonly Api_Context _context;

        public AdminSubscriptionPlanService(Api_Context context)
        {
            _context = context;
        }

        public async Task<ApiResponse<List<SubscriptionPlanResponseDto>>> GetAllAsync(bool includeInactive = true)
        {
            var query = _context.SubscriptionPlans.AsNoTracking()
                .Include(p => p.PlanFeatures)
                .ThenInclude(pf => pf.Feature)
                .Where(p => !p.IsDeleted);
            if (!includeInactive)
                query = query.Where(p => p.IsActive);

            var plans = await query.OrderBy(p => p.SortOrder).ThenBy(p => p.Id).ToListAsync();
            return ApiResponse<List<SubscriptionPlanResponseDto>>.CreateSuccess(plans.Select(MapPlan).ToList());
        }

        public async Task<ApiResponse<SubscriptionPlanResponseDto>> GetByIdAsync(int id)
        {
            var plan = await _context.SubscriptionPlans.AsNoTracking()
                .Include(p => p.PlanFeatures)
                .ThenInclude(pf => pf.Feature)
                .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
            if (plan == null)
                return ApiResponse<SubscriptionPlanResponseDto>.NotFound("پلن اشتراک یافت نشد");

            return ApiResponse<SubscriptionPlanResponseDto>.CreateSuccess(MapPlan(plan));
        }

        public async Task<ApiResponse<SubscriptionPlanResponseDto>> CreateAsync(CreateSubscriptionPlanDto dto)
        {
            var exists = await _context.SubscriptionPlans.AnyAsync(p => p.TierCode == dto.TierCode && !p.IsDeleted);
            if (exists)
                return ApiResponse<SubscriptionPlanResponseDto>.BadRequest("کد سطح اشتراک تکراری است");

            var featureIds = dto.FeatureIds?.Distinct().ToList() ?? new List<int>();
            var featuresResult = await ValidateAndLoadFeaturesAsync(featureIds);
            if (featuresResult.Error != null)
                return featuresResult.Error;

            var features = featuresResult.Features!;
            var plan = new SubscriptionPlan
            {
                Name = dto.Name.Trim(),
                TierCode = dto.TierCode.Trim(),
                Description = dto.Description?.Trim(),
                Price = dto.Price,
                DurationDays = dto.DurationDays,
                FreeQuickSendEnabled = ResolveLegacyFlag(features, SubscriptionFeatureCodes.FreeQuickSend),
                BusinessCardEnabled = ResolveLegacyFlag(features, SubscriptionFeatureCodes.BusinessCard),
                MonthlySmsLimit = dto.MonthlySmsLimit,
                SortOrder = dto.SortOrder,
                IsActive = dto.IsActive,
                CreatedAt = DateTime.UtcNow
            };

            foreach (var feature in features)
            {
                plan.PlanFeatures.Add(new SubscriptionPlanFeature
                {
                    Feature = feature
                });
            }

            _context.SubscriptionPlans.Add(plan);
            await _context.SaveChangesAsync();

            await _context.Entry(plan).Collection(p => p.PlanFeatures).Query()
                .Include(pf => pf.Feature)
                .LoadAsync();

            return ApiResponse<SubscriptionPlanResponseDto>.CreateSuccess(MapPlan(plan), "پلن اشتراک ایجاد شد", 201);
        }

        public async Task<ApiResponse<SubscriptionPlanResponseDto>> UpdateAsync(int id, UpdateSubscriptionPlanDto dto)
        {
            var plan = await _context.SubscriptionPlans
                .Include(p => p.PlanFeatures)
                .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
            if (plan == null)
                return ApiResponse<SubscriptionPlanResponseDto>.NotFound("پلن اشتراک یافت نشد");

            var duplicateCode = await _context.SubscriptionPlans.AnyAsync(p => p.TierCode == dto.TierCode && p.Id != id && !p.IsDeleted);
            if (duplicateCode)
                return ApiResponse<SubscriptionPlanResponseDto>.BadRequest("کد سطح اشتراک تکراری است");

            var featureIds = dto.FeatureIds?.Distinct().ToList() ?? new List<int>();
            var featuresResult = await ValidateAndLoadFeaturesAsync(featureIds);
            if (featuresResult.Error != null)
                return featuresResult.Error;

            var features = featuresResult.Features!;
            plan.Name = dto.Name.Trim();
            plan.TierCode = dto.TierCode.Trim();
            plan.Description = dto.Description?.Trim();
            plan.Price = dto.Price;
            plan.DurationDays = dto.DurationDays;
            plan.FreeQuickSendEnabled = ResolveLegacyFlag(features, SubscriptionFeatureCodes.FreeQuickSend);
            plan.BusinessCardEnabled = ResolveLegacyFlag(features, SubscriptionFeatureCodes.BusinessCard);
            plan.MonthlySmsLimit = dto.MonthlySmsLimit;
            plan.SortOrder = dto.SortOrder;
            plan.IsActive = dto.IsActive;
            plan.UpdatedAt = DateTime.UtcNow;

            _context.SubscriptionPlanFeatures.RemoveRange(plan.PlanFeatures);
            plan.PlanFeatures.Clear();
            foreach (var feature in features)
            {
                plan.PlanFeatures.Add(new SubscriptionPlanFeature
                {
                    SubscriptionPlanId = plan.Id,
                    SubscriptionFeatureId = feature.Id
                });
            }

            await _context.SaveChangesAsync();

            await _context.Entry(plan).Collection(p => p.PlanFeatures).Query()
                .Include(pf => pf.Feature)
                .LoadAsync();

            return ApiResponse<SubscriptionPlanResponseDto>.CreateSuccess(MapPlan(plan), "پلن اشتراک به‌روزرسانی شد");
        }

        public async Task<ApiResponse<bool>> DeleteAsync(int id)
        {
            var plan = await _context.SubscriptionPlans.FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
            if (plan == null)
                return ApiResponse<bool>.NotFound("پلن اشتراک یافت نشد");

            plan.IsDeleted = true;
            plan.IsActive = false;
            plan.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return ApiResponse<bool>.CreateSuccess(true, "پلن اشتراک حذف شد");
        }

        public async Task<ApiResponse<List<SubscriptionPlanResponseDto>>> GetActivePlansAsync()
        {
            var plans = await _context.SubscriptionPlans.AsNoTracking()
                .Include(p => p.PlanFeatures)
                .ThenInclude(pf => pf.Feature)
                .Where(p => p.IsActive && !p.IsDeleted)
                .OrderBy(p => p.SortOrder)
                .ToListAsync();
            return ApiResponse<List<SubscriptionPlanResponseDto>>.CreateSuccess(plans.Select(MapPlan).ToList());
        }

        private async Task<(List<SubscriptionFeature>? Features, ApiResponse<SubscriptionPlanResponseDto>? Error)> ValidateAndLoadFeaturesAsync(List<int> featureIds)
        {
            if (featureIds.Count == 0)
                return (new List<SubscriptionFeature>(), null);

            var features = await _context.SubscriptionFeatures
                .Where(f => featureIds.Contains(f.Id) && f.IsActive && !f.IsDeleted)
                .ToListAsync();

            if (features.Count != featureIds.Count)
                return (null, ApiResponse<SubscriptionPlanResponseDto>.BadRequest("یک یا چند امکان انتخاب‌شده معتبر نیست"));

            features = featureIds
                .Select(id => features.First(f => f.Id == id))
                .ToList();

            return (features, null);
        }

        private static bool ResolveLegacyFlag(IEnumerable<SubscriptionFeature> features, string code)
        {
            return features.Any(f => f.Code == code);
        }

        private static SubscriptionPlanResponseDto MapPlan(SubscriptionPlan plan)
        {
            var activeFeatures = plan.PlanFeatures
                .Where(pf => pf.Feature != null && !pf.Feature.IsDeleted)
                .Select(pf => pf.Feature!)
                .OrderBy(f => f.SortOrder)
                .ThenBy(f => f.Id)
                .ToList();

            return new SubscriptionPlanResponseDto
            {
                Id = plan.Id,
                Name = plan.Name,
                TierCode = plan.TierCode,
                Description = plan.Description,
                Price = plan.Price,
                DurationDays = plan.DurationDays,
                FreeQuickSendEnabled = plan.FreeQuickSendEnabled,
                BusinessCardEnabled = plan.BusinessCardEnabled,
                MonthlySmsLimit = plan.MonthlySmsLimit,
                SortOrder = plan.SortOrder,
                IsActive = plan.IsActive,
                FeatureIds = activeFeatures.Select(f => f.Id).ToList(),
                Features = activeFeatures.Select(f => new SubscriptionFeatureSummaryDto
                {
                    Id = f.Id,
                    Name = f.Name,
                    Code = f.Code
                }).ToList(),
                CreatedAt = plan.CreatedAt,
                UpdatedAt = plan.UpdatedAt
            };
        }
    }
}
