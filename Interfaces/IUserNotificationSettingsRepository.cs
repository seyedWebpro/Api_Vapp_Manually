using Api_Vapp.Models;

namespace Api_Vapp.Interfaces
{
    /// <summary>
    /// رابط Repository برای تنظیمات اعلان‌های کاربر
    /// </summary>
    public interface IUserNotificationSettingsRepository
    {
        /// <summary>
        /// دریافت تنظیمات اعلان‌های کاربر
        /// </summary>
        Task<UserNotificationSettings?> GetByUserIdAsync(int userId);

        /// <summary>
        /// ایجاد تنظیمات جدید
        /// </summary>
        Task<UserNotificationSettings> AddAsync(UserNotificationSettings settings);

        /// <summary>
        /// به‌روزرسانی تنظیمات
        /// </summary>
        Task<UserNotificationSettings> UpdateAsync(UserNotificationSettings settings);

        /// <summary>
        /// دریافت یا ایجاد تنظیمات (اگر وجود نداشت، با مقادیر پیش‌فرض ایجاد می‌کند)
        /// </summary>
        Task<UserNotificationSettings> GetOrCreateAsync(int userId);
    }
}



