using Api_Vapp.DTOs.Admin;
using Api_Vapp.DTOs.Common;

namespace Api_Vapp.Interfaces
{
    public interface IAdminSpecialOccasionService
    {
        Task<ApiResponse<List<SpecialOccasionAdminResponseDto>>> GetAllAsync(bool includeInactive = true);
        Task<ApiResponse<SpecialOccasionAdminResponseDto>> GetByIdAsync(int id);
        Task<ApiResponse<SpecialOccasionAdminResponseDto>> CreateAsync(CreateSpecialOccasionAdminDto dto);
        Task<ApiResponse<SpecialOccasionAdminResponseDto>> UpdateAsync(int id, UpdateSpecialOccasionAdminDto dto);
        Task<ApiResponse<bool>> DeleteAsync(int id);
    }
}
