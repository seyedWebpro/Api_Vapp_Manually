using Api_Vapp.Constants;

namespace Api_Vapp.Interfaces
{
    /// <summary>
    /// ارسال امن Push بدون اختلال در منطق کسب‌وکار
    /// </summary>
    public interface IUserPushNotifier
    {
        /// <summary>
        /// ارسال به یک کاربر — خطا را می‌بلعد و لاگ می‌کند
        /// </summary>
        Task NotifyAsync(
            int userId,
            NotificationCategory category,
            string title,
            string body,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// ارسال به همه کاربرانی که آن دسته اعلان را فعال دارند
        /// </summary>
        Task<int> NotifyBroadcastAsync(
            NotificationCategory category,
            string title,
            string body,
            CancellationToken cancellationToken = default);
    }
}
