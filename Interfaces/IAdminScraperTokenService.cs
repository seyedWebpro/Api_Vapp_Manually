using Api_Vapp.DTOs.Admin;
using Api_Vapp.DTOs.Common;

namespace Api_Vapp.Interfaces
{
    public interface IAdminScraperTokenService
    {
        Task<ApiResponse<AdminScraperTokensOverviewDto>> GetOverviewAsync();

        Task<ApiResponse<AdminScraperTokenSaveResultDto>> SaveDivarAsync(SaveDivarTokenDto dto);

        Task<ApiResponse<AdminScraperTokenSaveResultDto>> SaveSheypoorAsync(SaveSheypoorTokenDto dto);

        Task<ApiResponse<AdminScraperTokenMaintenanceDto>> RunMaintenanceAsync(
            bool forceSheypoorRefresh = false,
            bool forceDivarRefresh = false);
    }
}
