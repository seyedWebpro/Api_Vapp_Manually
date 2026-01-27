using Api_Vapp.DTOs.Automation;
using Api_Vapp.DTOs.Common;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;
using Api_Vapp.Utilities;

namespace Api_Vapp.Services
{
    /// <summary>
    /// سرویس مدیریت مناسبت‌های خاص
    /// </summary>
    public class SpecialOccasionService : ISpecialOccasionService
    {
        private readonly ISpecialOccasionRepository _specialOccasionRepository;

        public SpecialOccasionService(ISpecialOccasionRepository specialOccasionRepository)
        {
            _specialOccasionRepository = specialOccasionRepository;
        }

        public async Task<ApiResponse<SpecialOccasionResponseDto>> CreateSpecialOccasionAsync(int userId, CreateSpecialOccasionDto createDto)
        {
            try
            {
                var occasion = new SpecialOccasion
                {
                    UserId = userId,
                    Name = createDto.Name,
                    Type = createDto.Type,
                    // تبدیل تاریخ مناسبت به UTC
                    OccasionDate = createDto.OccasionDate.EnsureUtc(),
                    DefaultMessage = createDto.DefaultMessage,
                    IsActive = true,
                    IsDeleted = false,
                    CreatedAt = DateTime.UtcNow
                };

                await _specialOccasionRepository.AddAsync(occasion);

                return ApiResponse<SpecialOccasionResponseDto>.CreateSuccess(
                    MapToDto(occasion),
                    "مناسبت با موفقیت ایجاد شد",
                    201
                );
            }
            catch (Exception ex)
            {
                return ApiResponse<SpecialOccasionResponseDto>.InternalServerError($"خطا در ایجاد مناسبت: {ex.Message}");
            }
        }

        public async Task<ApiResponse<List<SpecialOccasionResponseDto>>> GetSpecialOccasionsAsync(int? userId)
        {
            try
            {
                var occasions = await _specialOccasionRepository.GetByUserIdAsync(userId);
                var systemOccasions = await _specialOccasionRepository.GetSystemOccasionsAsync();
                
                var allOccasions = occasions.Concat(systemOccasions)
                    .OrderBy(so => so.OccasionDate)
                    .ToList();

                var occasionDtos = allOccasions.Select(MapToDto).ToList();
                return ApiResponse<List<SpecialOccasionResponseDto>>.CreateSuccess(occasionDtos);
            }
            catch (Exception ex)
            {
                return ApiResponse<List<SpecialOccasionResponseDto>>.InternalServerError($"خطا در دریافت لیست مناسبت‌ها: {ex.Message}");
            }
        }

        public async Task<ApiResponse<SpecialOccasionResponseDto>> GetSpecialOccasionByIdAsync(int id)
        {
            try
            {
                var occasion = await _specialOccasionRepository.GetByIdAsync(id);
                if (occasion == null)
                {
                    return ApiResponse<SpecialOccasionResponseDto>.NotFound("مناسبت مورد نظر یافت نشد");
                }

                return ApiResponse<SpecialOccasionResponseDto>.CreateSuccess(MapToDto(occasion));
            }
            catch (Exception ex)
            {
                return ApiResponse<SpecialOccasionResponseDto>.InternalServerError($"خطا در دریافت مناسبت: {ex.Message}");
            }
        }

        public async Task<ApiResponse<SpecialOccasionResponseDto>> UpdateSpecialOccasionAsync(int id, int? userId, UpdateSpecialOccasionDto updateDto)
        {
            try
            {
                var occasion = await _specialOccasionRepository.GetByIdAsync(id);
                if (occasion == null)
                {
                    return ApiResponse<SpecialOccasionResponseDto>.NotFound("مناسبت مورد نظر یافت نشد");
                }

                // فقط کاربر ایجادکننده یا مناسبت‌های سیستمی قابل ویرایش نیستند
                if (occasion.IsSystem)
                {
                    return ApiResponse<SpecialOccasionResponseDto>.Forbidden("مناسبت‌های سیستمی قابل ویرایش نیستند");
                }

                if (occasion.UserId != userId)
                {
                    return ApiResponse<SpecialOccasionResponseDto>.Forbidden("شما مجاز به ویرایش این مناسبت نیستید");
                }

                if (updateDto.Name != null) occasion.Name = updateDto.Name;
                if (updateDto.Type != null) occasion.Type = updateDto.Type;
                // تبدیل تاریخ مناسبت به UTC
                if (updateDto.OccasionDate.HasValue) occasion.OccasionDate = updateDto.OccasionDate.Value.EnsureUtc();
                if (updateDto.DefaultMessage != null) occasion.DefaultMessage = updateDto.DefaultMessage;
                if (updateDto.IsActive.HasValue) occasion.IsActive = updateDto.IsActive.Value;

                occasion.UpdatedAt = DateTime.UtcNow;

                await _specialOccasionRepository.UpdateAsync(occasion);

                return ApiResponse<SpecialOccasionResponseDto>.CreateSuccess(
                    MapToDto(occasion),
                    "مناسبت با موفقیت به‌روزرسانی شد"
                );
            }
            catch (Exception ex)
            {
                return ApiResponse<SpecialOccasionResponseDto>.InternalServerError($"خطا در به‌روزرسانی مناسبت: {ex.Message}");
            }
        }

        public async Task<ApiResponse<bool>> DeleteSpecialOccasionAsync(int id, int? userId)
        {
            try
            {
                var occasion = await _specialOccasionRepository.GetByIdAsync(id);
                if (occasion == null)
                {
                    return ApiResponse<bool>.NotFound("مناسبت مورد نظر یافت نشد");
                }

                if (occasion.IsSystem)
                {
                    return ApiResponse<bool>.Forbidden("مناسبت‌های سیستمی قابل حذف نیستند");
                }

                if (occasion.UserId != userId)
                {
                    return ApiResponse<bool>.Forbidden("شما مجاز به حذف این مناسبت نیستید");
                }

                occasion.IsDeleted = true;
                occasion.UpdatedAt = DateTime.UtcNow;

                await _specialOccasionRepository.UpdateAsync(occasion);

                return ApiResponse<bool>.CreateSuccess(true, "مناسبت با موفقیت حذف شد");
            }
            catch (Exception ex)
            {
                return ApiResponse<bool>.InternalServerError($"خطا در حذف مناسبت: {ex.Message}");
            }
        }

        private SpecialOccasionResponseDto MapToDto(SpecialOccasion occasion)
        {
            return new SpecialOccasionResponseDto
            {
                Id = occasion.Id,
                Name = occasion.Name,
                Type = occasion.Type,
                OccasionDate = occasion.OccasionDate,
                DefaultMessage = occasion.DefaultMessage,
                IsSystem = occasion.IsSystem,
                IsActive = occasion.IsActive,
                CreatedAt = occasion.CreatedAt
            };
        }
    }
}

