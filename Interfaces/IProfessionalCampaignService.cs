using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.Message;

namespace Api_Vapp.Interfaces
{
    public interface IProfessionalCampaignService
    {
        Task<ApiResponse<ProfessionalCampaignResponseDto>> CreateAsync(int userId, CreateProfessionalCampaignDto dto);
        Task<ApiResponse<ProfessionalCampaignResponseDto>> GetByIdAsync(int userId, int id);
        Task<ApiResponse<ProfessionalCampaignListResponseDto>> GetListAsync(int userId, int pageNumber, int pageSize);
        Task<ApiResponse<ProfessionalCampaignResponseDto>> ActivateAsync(int userId, int id);
        Task<ApiResponse<ProfessionalCampaignResponseDto>> PauseAsync(int userId, int id);
        Task<ApiResponse<ProfessionalCampaignResponseDto>> ResumeAsync(int userId, int id);
        Task<ApiResponse<bool>> CancelAsync(int userId, int id);
        Task<ApiResponse<ProfessionalCampaignResponseDto>> RetryFailedStepAsync(int userId, int campaignId, int stepId);
        Task ProcessDueStepsAsync(CancellationToken cancellationToken);
    }
}
