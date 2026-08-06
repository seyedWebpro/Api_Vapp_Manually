using Api_Vapp.Constants;

namespace Api_Vapp.Interfaces
{
    /// <summary>
    /// اعلان کامل برای کاربر: ذخیره در inbox زنگوله + Push FCM
    /// خطا را می‌بلعد تا منطق کسب‌وکار قطع نشود
    /// </summary>
    public interface IUserAppNotifier
    {
        Task NotifyAsync(
            int userId,
            NotificationCategory category,
            string title,
            string body,
            string type,
            int? relatedEntityId = null,
            string? relatedEntityType = null,
            string? actionUrl = null,
            string? metadataJson = null,
            CancellationToken cancellationToken = default);
    }
}
