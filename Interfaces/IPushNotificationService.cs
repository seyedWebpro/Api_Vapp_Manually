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
        /// ارسال نوتیفیکیشن به همه دستگاه‌های فعال یک کاربر
        /// </summary>
        Task<PushDeliveryResultDto> SendToUserAsync(
            int userId,
            string title,
            string body,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// ارسال نوتیفیکیشن به یک توکن FCM مشخص
        /// </summary>
        Task<bool> SendToTokenAsync(
            string fcmToken,
            string title,
            string body,
            CancellationToken cancellationToken = default);
    }
}
