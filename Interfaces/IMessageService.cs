using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.Message;
using Api_Vapp.Models;

namespace Api_Vapp.Interfaces
{
    /// <summary>
    /// رابط سرویس برای مدیریت پیام‌ها و کمپین‌ها
    /// </summary>
    public interface IMessageService
    {
        // Message operations
        Task<ApiResponse<MessageResponseDto>> CreateMessageAsync(int userId, CreateMessageDto createDto);
        Task<ApiResponse<MessageResponseDto>> GetMessageByIdAsync(int messageId, int userId);
        Task<ApiResponse<MessageListResponseDto>> GetMessagesAsync(int userId, int pageNumber = 1, int pageSize = 10, string? searchTerm = null);
        Task<ApiResponse<MessageResponseDto>> UpdateMessageAsync(int messageId, int userId, UpdateMessageDto updateDto);
        Task<ApiResponse<bool>> DeleteMessageAsync(int messageId, int userId);

        // Campaign operations
        Task<ApiResponse<CampaignSummaryDto>> GetCampaignSummaryAsync(int userId, int messageId);
        Task<ApiResponse<CampaignSummaryDto>> CalculateCampaignSummaryAsync(int userId, int messageId, CreateCampaignDto campaignDto, string? idempotencyKey = null);
        Task<ApiResponse<DirectSendResultDto>> ConfirmAndSendMessageAsync(int userId, int messageId, string? idempotencyKey = null);
        Task<ApiResponse<CampaignResponseDto>> CreateCampaignAsync(int userId, CreateCampaignDto createDto);
        Task<ApiResponse<CampaignResponseDto>> GetCampaignByIdAsync(int campaignId, int userId);
        Task<ApiResponse<CampaignListResponseDto>> GetCampaignsAsync(int userId, int pageNumber = 1, int pageSize = 10, string? status = null);
        Task<ApiResponse<bool>> ConfirmAndSendCampaignAsync(int campaignId, int userId);
        Task<ApiResponse<bool>> CancelCampaignAsync(int campaignId, int userId);
        Task<ApiResponse<bool>> ToggleCampaignStatusAsync(int campaignId, int userId, bool isActive);

        // Template operations
        Task<ApiResponse<TemplateResponseDto>> CreateTemplateAsync(int userId, CreateTemplateDto createDto);
        Task<ApiResponse<List<TemplateResponseDto>>> GetTemplatesAsync(int userId);
        Task<ApiResponse<List<CategoryGroupDto>>> GetTemplatesGroupedByCategoryAsync(int userId);
        Task<ApiResponse<TemplateResponseDto>> UpdateTemplateAsync(int id, int userId, UpdateTemplateDto updateDto);
        Task<ApiResponse<bool>> DeleteTemplateAsync(int id, int userId);
        Task<ApiResponse<TemplateResponseDto>> SetUserDefaultTemplateAsync(int userId, int templateId);

        // Template Group operations
        Task<ApiResponse<TemplateGroupResponseDto>> CreateTemplateGroupAsync(int userId, CreateTemplateGroupDto createDto);
        Task<ApiResponse<List<TemplateGroupSummaryDto>>> GetTemplateGroupsAsync(int userId);
        Task<ApiResponse<TemplateGroupResponseDto>> GetTemplateGroupByIdAsync(int id, int userId);
        Task<ApiResponse<List<TemplateResponseDto>>> GetTemplatesByGroupIdAsync(int groupId, int userId);
        Task<ApiResponse<TemplateGroupResponseDto>> UpdateTemplateGroupAsync(int id, int userId, UpdateTemplateGroupDto updateDto);
        Task<ApiResponse<bool>> DeleteTemplateGroupAsync(int id, int userId);

        // Recipient operations
        Task<ApiResponse<RecipientListResponseDto>> SelectRecipientsAsync(int userId, SelectRecipientsDto selectDto);

        // Direct send operations (without campaign) - برای استفاده در Background Services
        Task<ApiResponse<DirectSendResultDto>> SendDirectMessageAsync(int userId, int messageId, SendDirectMessageDto sendDto, MessageSession? session = null);
        
        // Quick send operations
        Task<ApiResponse<DirectSendResultDto>> QuickSendMessageAsync(int userId, QuickSendMessageDto quickSendDto);

        // Report operations
        Task<ApiResponse<TodayReportDto>> GetTodayReportAsync(int userId);
        Task<ApiResponse<List<LatestCampaignsDto>>> GetLatestCampaignsAsync(int userId, int count = 5);
        Task<ApiResponse<ComprehensiveReportDto>> GetComprehensiveReportAsync(int userId);

        // Preview and personalization
        Task<ApiResponse<MessagePreviewDto>> GetMessagePreviewAsync(int messageId, int userId);
        Task<ApiResponse<PersonalizedMessageResponseDto>> PersonalizeMessageAsync(int messageId, int userId, Dictionary<string, string> placeholders, bool saveToMessage = true);

        // Tag operations
        Task<ApiResponse<MessageTagResponseDto>> CreateTagAsync(int userId, CreateMessageTagDto createDto);
        Task<ApiResponse<MessageTagListResponseDto>> GetTagsAsync(int userId, int pageNumber = 1, int pageSize = 10);
        Task<ApiResponse<MessageTagWithContactCountListResponseDto>> GetTagsWithContactCountAsync(int userId, int pageNumber = 1, int pageSize = 10);
    }
}


