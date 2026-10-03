using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.Message;

namespace Api_Vapp.Interfaces
{
    public interface IProfessionalCampaignService
    {
        Task<ApiResponse<ProfessionalCampaignResponseDto>> CreateAsync(int userId, CreateProfessionalCampaignDto dto);
        Task<ApiResponse<ProfessionalCampaignResponseDto>> UpdateAsync(int userId, int id, UpdateProfessionalCampaignDto dto);
        Task<ApiResponse<bool>> DeleteAsync(int userId, int id);
        Task<ApiResponse<ProfessionalCampaignResponseDto>> GetByIdAsync(int userId, int id);
        Task<ApiResponse<ProfessionalCampaignListResponseDto>> GetListAsync(int userId, int pageNumber, int pageSize);
        /// <summary>
        /// کمپین‌های Ready/Active با متن‌های تأییدشده برای انتخاب در ارسال سریع.
        /// </summary>
        Task<ApiResponse<ProfessionalCampaignListResponseDto>> GetQuickSendOptionsAsync(
            int userId,
            int pageNumber,
            int pageSize);
        Task<ApiResponse<ProfessionalCampaignResponseDto>> ActivateAsync(int userId, int id);
        Task<ApiResponse<ProfessionalCampaignResponseDto>> PauseAsync(int userId, int id);
        Task<ApiResponse<ProfessionalCampaignResponseDto>> ResumeAsync(int userId, int id);
        Task<ApiResponse<bool>> CancelAsync(int userId, int id);
        Task<ApiResponse<ProfessionalCampaignResponseDto>> RetryFailedStepAsync(int userId, int campaignId, int stepId);
        /// <summary>
        /// مخاطب را وارد دنباله کمپین می‌کند: پیام اول فوری، بقیه طبق زمان‌بندی از لحظه ثبت.
        /// </summary>
        Task<ApiResponse<DirectSendResultDto>> QuickSendAsync(
            int userId,
            int campaignId,
            QuickSendProfessionalCampaignDto dto);
        Task ProcessDueStepsAsync(CancellationToken cancellationToken);
    }
}
