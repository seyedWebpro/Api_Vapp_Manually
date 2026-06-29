using Api_Vapp.Constants;
using Api_Vapp.Data;
using Api_Vapp.DTOs.Admin;
using Api_Vapp.DTOs.Common;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;
using Microsoft.EntityFrameworkCore;

namespace Api_Vapp.Services
{
    public class SubscriptionDiscountService : ISubscriptionDiscountService
    {
        private readonly Api_Context _context;

        public SubscriptionDiscountService(Api_Context context)
        {
            _context = context;
        }

        public async Task<ApiResponse<List<SubscriptionDiscountCodeResponseDto>>> GetAllAsync(bool includeInactive = true)
        {
            var query = _context.SubscriptionDiscountCodes.AsNoTracking()
                .Include(d => d.SubscriptionPlan)
                .Where(d => !d.IsDeleted);

            if (!includeInactive)
                query = query.Where(d => d.IsActive);

            var items = await query.OrderByDescending(d => d.CreatedAt).ToListAsync();
            return ApiResponse<List<SubscriptionDiscountCodeResponseDto>>.CreateSuccess(items.Select(Map).ToList());
        }

        public async Task<ApiResponse<SubscriptionDiscountCodeResponseDto>> GetByIdAsync(int id)
        {
            var item = await _context.SubscriptionDiscountCodes.AsNoTracking()
                .Include(d => d.SubscriptionPlan)
                .FirstOrDefaultAsync(d => d.Id == id && !d.IsDeleted);

            if (item == null)
                return ApiResponse<SubscriptionDiscountCodeResponseDto>.NotFound("کد تخفیف یافت نشد");

            return ApiResponse<SubscriptionDiscountCodeResponseDto>.CreateSuccess(Map(item));
        }

        public async Task<ApiResponse<SubscriptionDiscountCodeResponseDto>> CreateAsync(CreateSubscriptionDiscountCodeDto dto)
        {
            var code = NormalizeCode(dto.Code);
            var exists = await _context.SubscriptionDiscountCodes.AnyAsync(d => d.Code == code && !d.IsDeleted);
            if (exists)
                return ApiResponse<SubscriptionDiscountCodeResponseDto>.BadRequest("کد تخفیف تکراری است");

            if (dto.SubscriptionPlanId.HasValue)
            {
                var planExists = await _context.SubscriptionPlans.AnyAsync(p =>
                    p.Id == dto.SubscriptionPlanId && p.IsActive && !p.IsDeleted);
                if (!planExists)
                    return ApiResponse<SubscriptionDiscountCodeResponseDto>.BadRequest("پلن اشتراک انتخاب‌شده معتبر نیست");
            }

            var entity = new SubscriptionDiscountCode
            {
                Code = code,
                Title = dto.Title?.Trim(),
                DiscountType = dto.DiscountType,
                Value = dto.Value,
                MaxDiscountAmount = dto.MaxDiscountAmount,
                MinOrderAmount = dto.MinOrderAmount,
                SubscriptionPlanId = dto.SubscriptionPlanId,
                MaxTotalUses = dto.MaxTotalUses,
                MaxUsesPerUser = dto.MaxUsesPerUser,
                ValidFrom = dto.ValidFrom?.ToUniversalTime(),
                ValidUntil = dto.ValidUntil?.ToUniversalTime(),
                IsActive = dto.IsActive,
                CreatedAt = DateTime.UtcNow
            };

            _context.SubscriptionDiscountCodes.Add(entity);
            await _context.SaveChangesAsync();

            await _context.Entry(entity).Reference(e => e.SubscriptionPlan).LoadAsync();
            return ApiResponse<SubscriptionDiscountCodeResponseDto>.CreateSuccess(Map(entity), "کد تخفیف ایجاد شد", 201);
        }

        public async Task<ApiResponse<SubscriptionDiscountCodeResponseDto>> UpdateAsync(int id, UpdateSubscriptionDiscountCodeDto dto)
        {
            var entity = await _context.SubscriptionDiscountCodes
                .Include(d => d.SubscriptionPlan)
                .FirstOrDefaultAsync(d => d.Id == id && !d.IsDeleted);

            if (entity == null)
                return ApiResponse<SubscriptionDiscountCodeResponseDto>.NotFound("کد تخفیف یافت نشد");

            var code = NormalizeCode(dto.Code);
            var duplicate = await _context.SubscriptionDiscountCodes.AnyAsync(d => d.Code == code && d.Id != id && !d.IsDeleted);
            if (duplicate)
                return ApiResponse<SubscriptionDiscountCodeResponseDto>.BadRequest("کد تخفیف تکراری است");

            if (dto.SubscriptionPlanId.HasValue)
            {
                var planExists = await _context.SubscriptionPlans.AnyAsync(p =>
                    p.Id == dto.SubscriptionPlanId && !p.IsDeleted);
                if (!planExists)
                    return ApiResponse<SubscriptionDiscountCodeResponseDto>.BadRequest("پلن اشتراک انتخاب‌شده معتبر نیست");
            }

            entity.Code = code;
            entity.Title = dto.Title?.Trim();
            entity.DiscountType = dto.DiscountType;
            entity.Value = dto.Value;
            entity.MaxDiscountAmount = dto.MaxDiscountAmount;
            entity.MinOrderAmount = dto.MinOrderAmount;
            entity.SubscriptionPlanId = dto.SubscriptionPlanId;
            entity.MaxTotalUses = dto.MaxTotalUses;
            entity.MaxUsesPerUser = dto.MaxUsesPerUser;
            entity.ValidFrom = dto.ValidFrom?.ToUniversalTime();
            entity.ValidUntil = dto.ValidUntil?.ToUniversalTime();
            entity.IsActive = dto.IsActive;
            entity.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return ApiResponse<SubscriptionDiscountCodeResponseDto>.CreateSuccess(Map(entity), "کد تخفیف به‌روزرسانی شد");
        }

        public async Task<ApiResponse<bool>> DeleteAsync(int id)
        {
            var entity = await _context.SubscriptionDiscountCodes.FirstOrDefaultAsync(d => d.Id == id && !d.IsDeleted);
            if (entity == null)
                return ApiResponse<bool>.NotFound("کد تخفیف یافت نشد");

            entity.IsDeleted = true;
            entity.IsActive = false;
            entity.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return ApiResponse<bool>.CreateSuccess(true, "کد تخفیف حذف شد");
        }

        public async Task<(SubscriptionDiscountCode? Discount, decimal DiscountAmount, string? ErrorMessage)> CalculateAsync(
            int userId,
            int planId,
            decimal planPrice,
            string? discountCode)
        {
            if (string.IsNullOrWhiteSpace(discountCode))
                return (null, 0, null);

            var code = NormalizeCode(discountCode);
            var discount = await _context.SubscriptionDiscountCodes
                .FirstOrDefaultAsync(d => d.Code == code && !d.IsDeleted && d.IsActive);

            if (discount == null)
                return (null, 0, SubscriptionMessages.DiscountInvalid);

            var now = DateTime.UtcNow;
            if (discount.ValidFrom.HasValue && discount.ValidFrom > now)
                return (null, 0, SubscriptionMessages.DiscountNotStarted);

            if (discount.ValidUntil.HasValue && discount.ValidUntil < now)
                return (null, 0, SubscriptionMessages.DiscountExpired);

            if (discount.SubscriptionPlanId.HasValue && discount.SubscriptionPlanId != planId)
                return (null, 0, SubscriptionMessages.DiscountWrongPlan);

            if (discount.MinOrderAmount.HasValue && planPrice < discount.MinOrderAmount.Value)
                return (null, 0, SubscriptionMessages.DiscountMinAmount);

            if (discount.MaxTotalUses.HasValue && discount.UsedCount >= discount.MaxTotalUses.Value)
                return (null, 0, SubscriptionMessages.DiscountLimitReached);

            if (discount.MaxUsesPerUser.HasValue)
            {
                var userUsageCount = await _context.SubscriptionDiscountUsages.CountAsync(u =>
                    u.SubscriptionDiscountCodeId == discount.Id && u.UserId == userId);
                if (userUsageCount >= discount.MaxUsesPerUser.Value)
                    return (null, 0, SubscriptionMessages.DiscountAlreadyUsed);
            }

            var discountAmount = CalculateDiscountAmount(discount, planPrice);
            if (discountAmount <= 0)
                return (null, 0, SubscriptionMessages.DiscountNotApplicable);

            return (discount, discountAmount, null);
        }

        private static decimal CalculateDiscountAmount(SubscriptionDiscountCode discount, decimal planPrice)
        {
            decimal amount = discount.DiscountType switch
            {
                SubscriptionDiscountTypes.Percentage => Math.Round(planPrice * discount.Value / 100m, 0, MidpointRounding.AwayFromZero),
                _ => discount.Value
            };

            if (discount.MaxDiscountAmount.HasValue)
                amount = Math.Min(amount, discount.MaxDiscountAmount.Value);

            return Math.Min(amount, planPrice);
        }

        private static string NormalizeCode(string code) => code.Trim().ToUpperInvariant();

        private static SubscriptionDiscountCodeResponseDto Map(SubscriptionDiscountCode entity) => new()
        {
            Id = entity.Id,
            Code = entity.Code,
            Title = entity.Title,
            DiscountType = entity.DiscountType,
            Value = entity.Value,
            MaxDiscountAmount = entity.MaxDiscountAmount,
            MinOrderAmount = entity.MinOrderAmount,
            SubscriptionPlanId = entity.SubscriptionPlanId,
            SubscriptionPlanName = entity.SubscriptionPlan?.Name,
            MaxTotalUses = entity.MaxTotalUses,
            UsedCount = entity.UsedCount,
            MaxUsesPerUser = entity.MaxUsesPerUser,
            ValidFrom = entity.ValidFrom,
            ValidUntil = entity.ValidUntil,
            IsActive = entity.IsActive,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }
}
