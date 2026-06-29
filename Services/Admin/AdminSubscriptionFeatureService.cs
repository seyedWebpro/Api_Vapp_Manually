using Api_Vapp.Constants;
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

        public Task<ApiResponse<SubscriptionFeatureResponseDto>> CreateAsync(CreateSubscriptionFeatureDto dto) =>
            Task.FromResult(ApiResponse<SubscriptionFeatureResponseDto>.BadRequest(
                "امکانات اشتراک از طریق کد سیستم تعریف می‌شوند و قابل ایجاد دستی نیستند."));

        public async Task<ApiResponse<SubscriptionFeatureResponseDto>> UpdateAsync(int id, UpdateSubscriptionFeatureDto dto)
        {
            var feature = await _context.SubscriptionFeatures.FirstOrDefaultAsync(f => f.Id == id && !f.IsDeleted);
            if (feature == null)
                return ApiResponse<SubscriptionFeatureResponseDto>.NotFound("امکان اشتراک یافت نشد");

            if (!SubscriptionFeatureCodes.IsKnown(feature.Code))
                return ApiResponse<SubscriptionFeatureResponseDto>.BadRequest("این امکان قابل ویرایش نیست");

            feature.Name = dto.Name.Trim();
            feature.Description = dto.Description?.Trim();
            feature.SortOrder = dto.SortOrder;
            feature.IsActive = dto.IsActive;
            feature.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return ApiResponse<SubscriptionFeatureResponseDto>.CreateSuccess(Map(feature), "امکان اشتراک به‌روزرسانی شد");
        }

        public Task<ApiResponse<bool>> DeleteAsync(int id) =>
            Task.FromResult(ApiResponse<bool>.BadRequest(
                "امکانات اشتراک سیستمی هستند و قابل حذف نیستند. می‌توانید آن‌ها را غیرفعال کنید."));

        private static SubscriptionFeatureResponseDto Map(SubscriptionFeature feature) => new()
        {
            Id = feature.Id,
            Name = feature.Name,
            Code = feature.Code,
            Description = feature.Description,
            SortOrder = feature.SortOrder,
            IsActive = feature.IsActive,
            IsSystemManaged = SubscriptionFeatureCodes.IsKnown(feature.Code),
            CreatedAt = feature.CreatedAt,
            UpdatedAt = feature.UpdatedAt
        };
    }
}
