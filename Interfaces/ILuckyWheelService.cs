using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.LuckyWheel;

namespace Api_Vapp.Interfaces
{
    public interface ILuckyWheelService
    {
        Task<ApiResponse<LuckyWheelResponseDto>> CreateDraftAsync(int userId, CreateLuckyWheelDto createDto);

        Task<ApiResponse<LuckyWheelResponseDto>> UpdateAsync(int id, int userId, UpdateLuckyWheelDto updateDto);

        Task<ApiResponse<LuckyWheelResponseDto>> PublishAsync(int id, int userId, PublishLuckyWheelDto? publishDto = null);

        Task<ApiResponse<LuckyWheelListResponseDto>> GetWheelsAsync(int userId, int pageNumber = 1, int pageSize = 10);

        Task<ApiResponse<LuckyWheelResponseDto>> GetByIdAsync(int id, int userId);

        Task<ApiResponse<bool>> DeleteAsync(int id, int userId);

        Task<ApiResponse<LuckyWheelResponseDto>> ToggleStatusAsync(int id, int userId);
    }
}
