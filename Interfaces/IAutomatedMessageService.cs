using Api_Vapp.DTOs.Automation;
using Api_Vapp.DTOs.Common;

namespace Api_Vapp.Interfaces
{
    /// <summary>
    /// رابط سرویس برای مدیریت پیام‌های خودکار
    /// </summary>
    public interface IAutomatedMessageService
    {
        Task<ApiResponse<AutomationTypeListResponseDto>> GetAutomationTypesAsync(int pageNumber = 1, int pageSize = 10);
        Task<ApiResponse<AutomatedMessageResponseDto>> CreateAutomatedMessageDraftAsync(int userId, CreateAutomatedMessageDraftDto createDto);
        Task<ApiResponse<RecipientListForAutomatedMessageResponseDto>> SelectRecipientsForAutomatedMessageAsync(int userId, int automatedMessageId, SelectRecipientsForAutomatedMessageDto selectDto);
        Task<ApiResponse<AutomatedMessageResponseDto>> CreateAutomatedMessageAsync(int userId, CreateAutomatedMessageDto createDto);
        Task<ApiResponse<AutomatedMessageResponseDto>> GetAutomatedMessageByIdAsync(int id, int userId);
        Task<ApiResponse<AutomatedMessageListResponseDto>> GetAutomatedMessagesAsync(int userId, int pageNumber = 1, int pageSize = 10, string? filter = null);
        Task<ApiResponse<AutomatedMessageResponseDto>> UpdateAutomatedMessageAsync(int id, int userId, UpdateAutomatedMessageDto updateDto);

        /// <summary>
        /// تست فوری ارسال پیام خودکار تولد (فقط برای توسعه)
        /// </summary>
        Task<ApiResponse<string>> TestSendBirthdayMessagesNowAsync(int userId);
        Task<ApiResponse<bool>> DeleteAutomatedMessageAsync(int id, int userId);
        Task<ApiResponse<bool>> ToggleAutomatedMessageStatusAsync(int id, int userId, bool isActive);
        
        // تنظیمات پایه (مرحله 3)
        Task<ApiResponse<AutomatedMessageResponseDto>> SaveBirthdaySettingsAsync(int automatedMessageId, int userId, BirthdaySettingsDto settingsDto);
        Task<ApiResponse<AutomatedMessageResponseDto>> SaveCashbackExpirySettingsAsync(int automatedMessageId, int userId, CashbackExpirySettingsDto settingsDto);
        Task<ApiResponse<AutomatedMessageResponseDto>> SaveWelcomeSettingsAsync(int automatedMessageId, int userId);
        Task<ApiResponse<AutomatedMessageResponseDto>> SavePurchaseReminderSettingsAsync(int automatedMessageId, int userId, PurchaseReminderSettingsDto settingsDto);
        Task<ApiResponse<SpecialOccasionManagementResponseDto>> ManageSpecialOccasionsAsync(int automatedMessageId, int userId, SpecialOccasionManagementDto managementDto);
        Task<ApiResponse<AutomatedMessageResponseDto>> SaveCustomAutomationSettingsAsync(int automatedMessageId, int userId, CustomAutomationSettingsDto settingsDto);
        
        // تنظیمات یکپارچه (جایگزین همه endpointهای بالا)
        Task<ApiResponse<object>> SaveUnifiedSettingsAsync(int automatedMessageId, int userId, UnifiedSettingsDto unifiedDto);
        
        // ساخت پیام (مرحله 4)
        Task<ApiResponse<MessageContentResponseDto>> SaveMessageContentAsync(int automatedMessageId, int userId, SaveMessageContentDto contentDto);
        
        // خلاصه و تنظیمات (مرحله 5)
        Task<ApiResponse<AutomatedMessageSummaryResponseDto>> GetAutomatedMessageSummaryAsync(int automatedMessageId, int userId);
        Task<ApiResponse<AutomatedMessageSummaryResponseDto>> CalculateAutomatedMessageSummaryAsync(int automatedMessageId, int userId, CalculateAutomatedMessageSummaryDto summaryDto);
        Task<ApiResponse<AutomatedMessageSummaryResponseDto>> SaveAutomatedMessageSettingsAsync(int automatedMessageId, int userId, SaveAutomatedMessageSettingsDto settingsDto);
    }
}


