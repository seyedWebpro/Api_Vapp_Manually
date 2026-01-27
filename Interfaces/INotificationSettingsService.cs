using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.User;

namespace Api_Vapp.Interfaces
{
    /// <summary>
    /// رابط سرویس تنظیمات اعلان‌های کاربر
    /// </summary>
    public interface INotificationSettingsService
    {
        /// <summary>
        /// دریافت تنظیمات اعلان‌های کاربر
        /// </summary>
        Task<ApiResponse<NotificationSettingsDto>> GetSettingsAsync(int userId);

        /// <summary>
        /// به‌روزرسانی تنظیمات اعلان‌های کاربر
        /// </summary>
        Task<ApiResponse<NotificationSettingsDto>> UpdateSettingsAsync(int userId, NotificationSettingsDto settingsDto);
    }
}



