using Api_Vapp.DTOs.Admin;
using Api_Vapp.DTOs.Common;

namespace Api_Vapp.Interfaces
{
    public interface IAppNewsTickerService
    {
        Task<ApiResponse<List<AppNewsTickerMessageResponseDto>>> GetAllAsync(bool includeInactive = true);
        Task<ApiResponse<List<AppNewsTickerMessageResponseDto>>> GetActiveAsync();
        Task<ApiResponse<AppNewsTickerMessageResponseDto>> CreateAsync(CreateAppNewsTickerMessageDto dto);
        Task<ApiResponse<AppNewsTickerMessageResponseDto>> UpdateAsync(int id, UpdateAppNewsTickerMessageDto dto);
        Task<ApiResponse<bool>> DeleteAsync(int id);
    }
}
