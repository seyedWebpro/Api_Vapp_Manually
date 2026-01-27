using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.SocialMediaLink;
using Api_Vapp.DTOs.Message;

namespace Api_Vapp.Interfaces
{
    /// <summary>
    /// رابط سرویس مدیریت لینک‌های شبکه‌های اجتماعی
    /// </summary>
    public interface ISocialMediaLinkService
    {
        Task<ApiResponse<SocialMediaLinkResponseDto>> CreateSocialMediaLinkAsync(int userId, CreateSocialMediaLinkDto createDto);
        Task<ApiResponse<SocialMediaLinkListResponseDto>> GetSocialMediaLinksAsync(int userId, int pageNumber = 1, int pageSize = 10);
        Task<ApiResponse<SocialMediaLinkResponseDto>> GetSocialMediaLinkByIdAsync(int id, int userId);
        Task<ApiResponse<SocialMediaLinkResponseDto>> UpdateSocialMediaLinkAsync(int id, int userId, UpdateSocialMediaLinkDto updateDto);
        Task<ApiResponse<bool>> DeleteSocialMediaLinkAsync(int id, int userId);
        Task<ApiResponse<SocialMediaLinkResponseDto>> SetUserDefaultSocialMediaLinkAsync(int userId, int linkId);
        Task<ApiResponse<DirectSendResultDto>> QuickSendSocialMediaLinkAsync(int userId, QuickSendSocialMediaLinkDto quickSendDto);
    }
}





