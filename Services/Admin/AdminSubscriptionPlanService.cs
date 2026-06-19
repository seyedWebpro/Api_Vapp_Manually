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
            var query = _context.SubscriptionPlans.AsNoTracking().Where(p => !p.IsDeleted);
            if (!includeInactive)
                query = query.Where(p => p.IsActive);

            var plans = await query.OrderBy(p => p.SortOrder).ThenBy(p => p.Id).ToListAsync();
            return ApiResponse<List<SubscriptionPlanResponseDto>>.CreateSuccess(plans.Select(MapPlan).ToList());
        }

        public async Task<ApiResponse<SubscriptionPlanResponseDto>> GetByIdAsync(int id)
        {
            var plan = await _context.SubscriptionPlans.AsNoTracking()
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

            var plan = new SubscriptionPlan
            {
                Name = dto.Name.Trim(),
                TierCode = dto.TierCode.Trim(),
                Description = dto.Description?.Trim(),
                Price = dto.Price,
                DurationDays = dto.DurationDays,
                FreeQuickSendEnabled = dto.FreeQuickSendEnabled,
                BusinessCardEnabled = dto.BusinessCardEnabled,
                MonthlySmsLimit = dto.MonthlySmsLimit,
                SortOrder = dto.SortOrder,
                IsActive = dto.IsActive,
                CreatedAt = DateTime.UtcNow
            };

            _context.SubscriptionPlans.Add(plan);
            await _context.SaveChangesAsync();
            return ApiResponse<SubscriptionPlanResponseDto>.CreateSuccess(MapPlan(plan), "پلن اشتراک ایجاد شد", 201);
        }

        public async Task<ApiResponse<SubscriptionPlanResponseDto>> UpdateAsync(int id, UpdateSubscriptionPlanDto dto)
        {
            var plan = await _context.SubscriptionPlans.FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
            if (plan == null)
                return ApiResponse<SubscriptionPlanResponseDto>.NotFound("پلن اشتراک یافت نشد");

            var duplicateCode = await _context.SubscriptionPlans.AnyAsync(p => p.TierCode == dto.TierCode && p.Id != id && !p.IsDeleted);
            if (duplicateCode)
                return ApiResponse<SubscriptionPlanResponseDto>.BadRequest("کد سطح اشتراک تکراری است");

            plan.Name = dto.Name.Trim();
            plan.TierCode = dto.TierCode.Trim();
            plan.Description = dto.Description?.Trim();
            plan.Price = dto.Price;
            plan.DurationDays = dto.DurationDays;
            plan.FreeQuickSendEnabled = dto.FreeQuickSendEnabled;
            plan.BusinessCardEnabled = dto.BusinessCardEnabled;
            plan.MonthlySmsLimit = dto.MonthlySmsLimit;
            plan.SortOrder = dto.SortOrder;
            plan.IsActive = dto.IsActive;
            plan.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
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
                .Where(p => p.IsActive && !p.IsDeleted)
                .OrderBy(p => p.SortOrder)
                .ToListAsync();
            return ApiResponse<List<SubscriptionPlanResponseDto>>.CreateSuccess(plans.Select(MapPlan).ToList());
        }

        private static SubscriptionPlanResponseDto MapPlan(SubscriptionPlan plan) => new()
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
            CreatedAt = plan.CreatedAt,
            UpdatedAt = plan.UpdatedAt
        };
    }
}
