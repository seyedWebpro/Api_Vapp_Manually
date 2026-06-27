using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.ReferralProgram;

namespace Api_Vapp.Interfaces
{
    public interface IReferralProgramService
    {
        Task<ApiResponse<ReferralProgramListDto>> GetProgramsAsync(int userId, int pageNumber = 1, int pageSize = 10, bool? isActive = null);

        Task<ApiResponse<ReferralDashboardStatsDto>> GetDashboardStatsAsync(int userId);

        Task<ApiResponse<ReferralProgramDto>> GetByIdAsync(int id, int userId);

        Task<ApiResponse<ReferralProgramDto>> ToggleStatusAsync(int id, int userId);

        Task<ApiResponse<bool>> DeleteAsync(int id, int userId);

        Task<ApiResponse<List<ReferralNotebookDto>>> GetNotebooksAsync(int userId);

        Task<ApiResponse<ReferralStep1ValidationResponseDto>> ValidateStep1Async(int userId, ReferralStep1Dto step1Dto);

        Task<ApiResponse<ReferralStep2ValidationResponseDto>> ValidateStep2Async(int userId, ReferralStep2Dto step2Dto);

        Task<ApiResponse<ReferralSummaryDto>> GetSummaryAsync(int userId, GetReferralSummaryRequestDto request);

        Task<ApiResponse<ReferralSummaryDto>> SaveStep3SettingsAsync(int userId, SaveReferralStep3RequestDto request);

        Task<ApiResponse<ConfirmReferralProgramResponseDto>> ConfirmAsync(int userId, ConfirmReferralProgramDto request);

        Task<ApiResponse<InquireReferralCodeResponseDto>> InquireCodeAsync(int userId, InquireReferralCodeDto request);

        Task<ApiResponse<RedeemReferralCodeResponseDto>> RedeemCodeAsync(int userId, RedeemReferralCodeDto request);

        Task<ApiResponse<ReferralUsageHistoryListDto>> GetHistoryAsync(
            int programId,
            int userId,
            int pageNumber = 1,
            int pageSize = 10,
            DateTime? fromDate = null,
            DateTime? toDate = null);

        Task<ApiResponse<ReferralProgramDto>> UpdateProgramAsync(int id, int userId, UpdateReferralProgramDto updateDto);
    }
}
