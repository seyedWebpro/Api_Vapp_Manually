namespace Api_Vapp.Interfaces
{
    public interface IPushNotificationService
    {
        /// <summary>
        /// ارسال نوتیفیکیشن به همه دستگاه‌های فعال یک کاربر
        /// </summary>
        Task<int> SendToUserAsync(int userId, string title, string body, CancellationToken cancellationToken = default);

        /// <summary>
        /// ارسال نوتیفیکیشن به یک توکن FCM مشخص
        /// </summary>
        Task<bool> SendToTokenAsync(string fcmToken, string title, string body, CancellationToken cancellationToken = default);
    }
}
