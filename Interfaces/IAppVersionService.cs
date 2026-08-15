using Api_Vapp.DTOs.AppVersion;
using Api_Vapp.DTOs.Common;

namespace Api_Vapp.Interfaces
{
    public interface IAppVersionService
    {
        Task<ApiResponse<AppVersionCheckResponseDto>> CheckAsync(string platform, string currentVersion);

        Task<ApiResponse<List<AppVersionPolicyResponseDto>>> GetAllPoliciesAsync();

        Task<ApiResponse<AppVersionPolicyResponseDto>> GetPolicyByPlatformAsync(string platform);

        Task<ApiResponse<AppVersionPolicyResponseDto>> UpdatePolicyAsync(string platform, UpdateAppVersionPolicyDto dto);
    }
}
