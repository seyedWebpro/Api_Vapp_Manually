using Api_Vapp.Data;
using Api_Vapp.DTOs.Admin;
using Api_Vapp.DTOs.Common;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;
using Microsoft.EntityFrameworkCore;

namespace Api_Vapp.Services.Admin
{
    public class AdminSubscriptionFeatureService : IAdminSubscriptionFeatureService
    {
        private readonly Api_Context _context;

        public AdminSubscriptionFeatureService(Api_Context context)
        {
            _context = context;
        }

        public async Task<ApiResponse<List<SubscriptionFeatureResponseDto>>> GetAllAsync(bool includeInactive = true)
        {
            var query = _context.SubscriptionFeatures.AsNoTracking().Where(f => !f.IsDeleted);
            if (!includeInactive)
                query = query.Where(f => f.IsActive);

            var features = await query.OrderBy(f => f.SortOrder).ThenBy(f => f.Id).ToListAsync();
            return ApiResponse<List<SubscriptionFeatureResponseDto>>.CreateSuccess(features.Select(Map).ToList());
        }

        public async Task<ApiResponse<SubscriptionFeatureResponseDto>> GetByIdAsync(int id)
        {
            var feature = await _context.SubscriptionFeatures.AsNoTracking()
                .FirstOrDefaultAsync(f => f.Id == id && !f.IsDeleted);
            if (feature == null)
                return ApiResponse<SubscriptionFeatureResponseDto>.NotFound("امکان اشتراک یافت نشد");

            return ApiResponse<SubscriptionFeatureResponseDto>.CreateSuccess(Map(feature));
        }

        public async Task<ApiResponse<SubscriptionFeatureResponseDto>> CreateAsync(CreateSubscriptionFeatureDto dto)
        {
            var code = dto.Code.Trim().ToLowerInvariant();
            var exists = await _context.SubscriptionFeatures.AnyAsync(f => f.Code == code && !f.IsDeleted);
            if (exists)
                return ApiResponse<SubscriptionFeatureResponseDto>.BadRequest("کد امکان تکراری است");

            var feature = new SubscriptionFeature
            {
                Name = dto.Name.Trim(),
                Code = code,
                Description = dto.Description?.Trim(),
                SortOrder = dto.SortOrder,
                IsActive = dto.IsActive,
                CreatedAt = DateTime.UtcNow
            };

            _context.SubscriptionFeatures.Add(feature);
            await _context.SaveChangesAsync();
            return ApiResponse<SubscriptionFeatureResponseDto>.CreateSuccess(Map(feature), "امکان اشتراک ایجاد شد", 201);
        }

        public async Task<ApiResponse<SubscriptionFeatureResponseDto>> UpdateAsync(int id, UpdateSubscriptionFeatureDto dto)
        {
            var feature = await _context.SubscriptionFeatures.FirstOrDefaultAsync(f => f.Id == id && !f.IsDeleted);
            if (feature == null)
                return ApiResponse<SubscriptionFeatureResponseDto>.NotFound("امکان اشتراک یافت نشد");

            var code = dto.Code.Trim().ToLowerInvariant();
            var duplicateCode = await _context.SubscriptionFeatures.AnyAsync(f => f.Code == code && f.Id != id && !f.IsDeleted);
            if (duplicateCode)
                return ApiResponse<SubscriptionFeatureResponseDto>.BadRequest("کد امکان تکراری است");

            feature.Name = dto.Name.Trim();
            feature.Code = code;
            feature.Description = dto.Description?.Trim();
            feature.SortOrder = dto.SortOrder;
            feature.IsActive = dto.IsActive;
            feature.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return ApiResponse<SubscriptionFeatureResponseDto>.CreateSuccess(Map(feature), "امکان اشتراک به‌روزرسانی شد");
        }

        public async Task<ApiResponse<bool>> DeleteAsync(int id)
        {
            var feature = await _context.SubscriptionFeatures.FirstOrDefaultAsync(f => f.Id == id && !f.IsDeleted);
            if (feature == null)
                return ApiResponse<bool>.NotFound("امکان اشتراک یافت نشد");

            var inUse = await _context.SubscriptionPlanFeatures.AnyAsync(pf => pf.SubscriptionFeatureId == id);
            if (inUse)
                return ApiResponse<bool>.BadRequest("این امکان در پلن‌های اشتراک استفاده شده و قابل حذف نیست");

            feature.IsDeleted = true;
            feature.IsActive = false;
            feature.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return ApiResponse<bool>.CreateSuccess(true, "امکان اشتراک حذف شد");
        }

        private static SubscriptionFeatureResponseDto Map(SubscriptionFeature feature) => new()
        {
            Id = feature.Id,
            Name = feature.Name,
            Code = feature.Code,
            Description = feature.Description,
            SortOrder = feature.SortOrder,
            IsActive = feature.IsActive,
            CreatedAt = feature.CreatedAt,
            UpdatedAt = feature.UpdatedAt
        };
    }
}
