using Api_Vapp.DTOs.Automation;
using Api_Vapp.DTOs.Common;

namespace Api_Vapp.Interfaces
{
    /// <summary>
    /// رابط سرویس مدیریت مناسبت‌های خاص
    /// </summary>
    public interface ISpecialOccasionService
    {
        Task<ApiResponse<SpecialOccasionResponseDto>> CreateSpecialOccasionAsync(int userId, CreateSpecialOccasionDto createDto);
        Task<ApiResponse<List<SpecialOccasionResponseDto>>> GetSpecialOccasionsAsync(int? userId);
        Task<ApiResponse<SpecialOccasionResponseDto>> GetSpecialOccasionByIdAsync(int id);
        Task<ApiResponse<SpecialOccasionResponseDto>> UpdateSpecialOccasionAsync(int id, int? userId, UpdateSpecialOccasionDto updateDto);
        Task<ApiResponse<bool>> DeleteSpecialOccasionAsync(int id, int? userId);
    }
}

