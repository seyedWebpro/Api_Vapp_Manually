using Api_Vapp.Constants;
using Api_Vapp.Models;

namespace Api_Vapp.Interfaces
{
    /// <summary>
    /// رابط Repository برای تنظیمات اعلان‌های کاربر
    /// </summary>
    public interface IUserNotificationSettingsRepository
    {
        Task<UserNotificationSettings?> GetByUserIdAsync(int userId);

        Task<UserNotificationSettings> AddAsync(UserNotificationSettings settings);

        Task<UserNotificationSettings> UpdateAsync(UserNotificationSettings settings);

        Task<UserNotificationSettings> GetOrCreateAsync(int userId);

        /// <summary>
        /// بررسی سریع اجازه ارسال Push برای یک دسته — فقط دو ستون لازم را می‌خواند
        /// </summary>
        /// <returns>
        /// true = مجاز؛ false = غیرمجاز؛ null = ردیف تنظیمات وجود ندارد (پیش‌فرض: مجاز)
        /// </returns>
        Task<bool?> IsPushAllowedAsync(int userId, NotificationCategory category, CancellationToken cancellationToken = default);
    }
}
