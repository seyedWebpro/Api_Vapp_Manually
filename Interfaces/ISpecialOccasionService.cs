using Api_Vapp.DTOs.Automation;
using Api_Vapp.DTOs.Common;

namespace Api_Vapp.Interfaces
{
    public interface ISpecialOccasionService
    {
        Task<ApiResponse<SpecialOccasionResponseDto>> CreateSpecialOccasionAsync(int userId, CreateSpecialOccasionDto createDto);
        Task<ApiResponse<List<SpecialOccasionResponseDto>>> GetSpecialOccasionsAsync(int? userId);
        Task<ApiResponse<SpecialOccasionResponseDto>> GetSpecialOccasionByIdAsync(int id);
        Task<ApiResponse<SpecialOccasionResponseDto>> UpdateSpecialOccasionAsync(int id, int? userId, UpdateSpecialOccasionDto updateDto);
        Task<ApiResponse<bool>> DeleteSpecialOccasionAsync(int id, int? userId);

        Task<ApiResponse<OccasionTableResponseDto>> GetOccasionTableAsync(int userId, string? category = null);
        Task<ApiResponse<UserOccasionProfileDto>> GetProfileAsync(int userId);
        Task<ApiResponse<UserOccasionProfileDto>> UpdateProfileAsync(int userId, UpdateUserOccasionProfileDto dto);
        Task<ApiResponse<OccasionTableItemDto>> TogglePreferenceAsync(int userId, int occasionId, ToggleOccasionPreferenceDto dto);
        Task<ApiResponse<UserOccasionProfileDto>> ToggleCategoryAsync(int userId, ToggleOccasionCategoryDto dto);
        Task<ApiResponse<OccasionTableItemDto>> UpdateTemplateAsync(int userId, int occasionId, UpdateOccasionTemplateDto dto);
        Task<ApiResponse<OccasionTableItemDto>> ResetTemplateAsync(int userId, int occasionId);
    }
}
