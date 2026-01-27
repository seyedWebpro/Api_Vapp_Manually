using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.QuickAction;
using Api_Vapp.DTOs.Message;

namespace Api_Vapp.Interfaces
{
    /// <summary>
    /// رابط سرویس مدیریت اقدام‌های سریع (لینک‌ها)
    /// </summary>
    public interface IQuickActionService
    {
        Task<ApiResponse<QuickActionResponseDto>> CreateQuickActionAsync(int userId, CreateQuickActionDto createDto);
        Task<ApiResponse<QuickActionListResponseDto>> GetQuickActionsAsync(int userId, int pageNumber = 1, int pageSize = 10);
        Task<ApiResponse<QuickActionResponseDto>> GetQuickActionByIdAsync(int id, int userId);
        Task<ApiResponse<QuickActionResponseDto>> UpdateQuickActionAsync(int id, int userId, UpdateQuickActionDto updateDto);
        Task<ApiResponse<bool>> DeleteQuickActionAsync(int id, int userId);
        Task<ApiResponse<string>> UploadIconAsync(int id, int userId, Microsoft.AspNetCore.Http.IFormFile iconFile);
        Task<ApiResponse<QuickActionResponseDto>> SetUserDefaultActionAsync(int userId, int actionId);
        Task<ApiResponse<DirectSendResultDto>> QuickSendActionAsync(int userId, QuickSendActionDto quickSendDto);
    }
}












