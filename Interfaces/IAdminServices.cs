using Api_Vapp.DTOs.Admin;
using Api_Vapp.DTOs.Common;
using Microsoft.AspNetCore.Http;

namespace Api_Vapp.Interfaces
{
    public interface IAdminSubscriptionPlanService
    {
        Task<ApiResponse<List<SubscriptionPlanResponseDto>>> GetAllAsync(bool includeInactive = true);
        Task<ApiResponse<SubscriptionPlanResponseDto>> GetByIdAsync(int id);
        Task<ApiResponse<SubscriptionPlanResponseDto>> CreateAsync(CreateSubscriptionPlanDto dto);
        Task<ApiResponse<SubscriptionPlanResponseDto>> UpdateAsync(int id, UpdateSubscriptionPlanDto dto);
        Task<ApiResponse<bool>> DeleteAsync(int id);
        Task<ApiResponse<List<SubscriptionPlanResponseDto>>> GetActivePlansAsync();
    }

    public interface IAdminSubscriptionFeatureService
    {
        Task<ApiResponse<List<SubscriptionFeatureResponseDto>>> GetAllAsync(bool includeInactive = true);
        Task<ApiResponse<SubscriptionFeatureResponseDto>> GetByIdAsync(int id);
        Task<ApiResponse<SubscriptionFeatureResponseDto>> CreateAsync(CreateSubscriptionFeatureDto dto);
        Task<ApiResponse<SubscriptionFeatureResponseDto>> UpdateAsync(int id, UpdateSubscriptionFeatureDto dto);
        Task<ApiResponse<bool>> DeleteAsync(int id);
    }

    public interface IAdminUserSubscriptionService
    {
        Task<ApiResponse<List<UserSubscriptionResponseDto>>> GetAllAsync(int? userId = null, string? status = null);
        Task<ApiResponse<UserSubscriptionResponseDto>> AssignAsync(AssignUserSubscriptionDto dto);
        Task<ApiResponse<bool>> CancelAsync(int id);
    }

    public interface IAdminSupportTicketService
    {
        Task<ApiResponse<PagedResponse<SupportTicketResponseDto>>> GetAllAsync(string? status = null, int page = 1, int pageSize = 20);
        Task<ApiResponse<SupportTicketResponseDto>> GetByIdAsync(int id);
        Task<ApiResponse<SupportTicketResponseDto>> ReplyAsync(int id, int adminUserId, ReplySupportTicketDto dto, IFormFile? imageFile = null);
        Task<ApiResponse<SupportTicketResponseDto>> UpdateStatusAsync(int id, UpdateSupportTicketStatusDto dto);
    }

    public interface IUserSupportTicketService
    {
        Task<ApiResponse<SupportTicketResponseDto>> CreateAsync(int userId, CreateSupportTicketDto dto);
        Task<ApiResponse<List<SupportTicketResponseDto>>> GetMyTicketsAsync(int userId);
        Task<ApiResponse<SupportTicketResponseDto>> GetMyTicketByIdAsync(int userId, int ticketId);
        Task<ApiResponse<SupportTicketResponseDto>> ReplyAsync(int userId, int ticketId, ReplySupportTicketDto dto);
    }

    public interface IAdminEducationalVideoService
    {
        Task<ApiResponse<List<EducationalVideoResponseDto>>> GetAllAsync(bool includeInactive = true);
        Task<ApiResponse<EducationalVideoResponseDto>> GetByIdAsync(int id);
        Task<ApiResponse<EducationalVideoResponseDto>> CreateAsync(CreateEducationalVideoDto dto);
        Task<ApiResponse<EducationalVideoResponseDto>> UpdateAsync(int id, UpdateEducationalVideoDto dto);
        Task<ApiResponse<bool>> DeleteAsync(int id);
        Task<ApiResponse<List<EducationalVideoResponseDto>>> GetActiveVideosAsync();
    }

    public interface IAdminMessageApprovalService
    {
        Task<ApiResponse<PagedResponse<SmsApprovalRequestResponseDto>>> GetPendingAsync(int page = 1, int pageSize = 20);
        Task<ApiResponse<PagedResponse<SmsApprovalRequestResponseDto>>> GetAllAsync(string? status = null, int page = 1, int pageSize = 20);
        Task<ApiResponse<SmsApprovalRequestResponseDto>> GetByIdAsync(int id);
        Task<ApiResponse<bool>> ApproveAsync(int id, int adminUserId);
        Task<ApiResponse<bool>> RejectAsync(int id, int adminUserId, RejectApprovalDto dto);
    }

    public interface IAdminTemplateApprovalService
    {
        Task<ApiResponse<PagedResponse<TemplateApprovalResponseDto>>> GetPendingAsync(int page = 1, int pageSize = 20);
        Task<ApiResponse<PagedResponse<TemplateApprovalResponseDto>>> GetAllAsync(string? status = null, int page = 1, int pageSize = 20);
        Task<ApiResponse<TemplateApprovalResponseDto>> GetByIdAsync(int id);
        Task<ApiResponse<bool>> ApproveAsync(int id, int adminUserId);
        Task<ApiResponse<bool>> RejectAsync(int id, int adminUserId, RejectApprovalDto dto);
    }

    public interface IAdminDashboardService
    {
        Task<ApiResponse<AdminDashboardStatsDto>> GetStatsAsync();
        Task<ApiResponse<AdminDashboardChartsDto>> GetChartsAsync();
    }
}
