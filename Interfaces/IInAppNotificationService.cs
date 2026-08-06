using Api_Vapp.Constants;
using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.Notification;

namespace Api_Vapp.Interfaces
{
    public interface IInAppNotificationService
    {
        /// <summary>
        /// ایجاد اعلان درون‌برنامه‌ای — خطا را به caller پرتاب نمی‌کند (لاگ می‌کند)
        /// </summary>
        Task CreateSafeAsync(
            int userId,
            string title,
            string body,
            string type,
            NotificationCategory category = NotificationCategory.Suggestions,
            int? relatedEntityId = null,
            string? relatedEntityType = null,
            string? actionUrl = null,
            string? metadataJson = null,
            CancellationToken cancellationToken = default);

        Task<ApiResponse<PagedResponse<InAppNotificationDto>>> GetMyNotificationsAsync(
            int userId,
            int page = 1,
            int pageSize = 20,
            bool? isRead = null,
            string? type = null);

        Task<ApiResponse<UnreadNotificationCountDto>> GetUnreadCountAsync(int userId);

        Task<ApiResponse<bool>> MarkAsReadAsync(int userId, int notificationId);

        Task<ApiResponse<bool>> MarkAllAsReadAsync(int userId);

        Task<ApiResponse<bool>> DeleteAsync(int userId, int notificationId);
    }
}
