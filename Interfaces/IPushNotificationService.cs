using Api_Vapp.Constants;
using Api_Vapp.DTOs.Device;

namespace Api_Vapp.Interfaces
{
    public interface IPushNotificationService
    {
        /// <summary>
        /// تلاش برای آماده‌سازی Firebase Admin (بدون ارسال)
        /// </summary>
        bool TryInitialize();

        /// <summary>
        /// ارسال نوتیفیکیشن به دستگاه‌های فعال کاربر — با احترام به تنظیمات پروفایل
        /// </summary>
        Task<PushDeliveryResultDto> SendToUserAsync(
            int userId,
            string title,
            string body,
            NotificationCategory category,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// ارسال مستقیم به یک توکن (بدون چک تنظیمات کاربر — فقط برای موارد خاص)
        /// </summary>
        Task<bool> SendToTokenAsync(
            string fcmToken,
            string title,
            string body,
            CancellationToken cancellationToken = default);
    }
}
