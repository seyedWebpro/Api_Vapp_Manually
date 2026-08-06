using Api_Vapp.Constants;
using Api_Vapp.Interfaces;

namespace Api_Vapp.Services
{
    /// <summary>
    /// ذخیره در inbox زنگوله + ارسال Push — هر دو امن (خطا بلعیده می‌شود)
    /// </summary>
    public class UserAppNotifier : IUserAppNotifier
    {
        private readonly IInAppNotificationService _inApp;
        private readonly IUserPushNotifier _push;
        private readonly ILogger<UserAppNotifier> _logger;

        public UserAppNotifier(
            IInAppNotificationService inApp,
            IUserPushNotifier push,
            ILogger<UserAppNotifier> logger)
        {
            _inApp = inApp;
            _push = push;
            _logger = logger;
        }

        public async Task NotifyAsync(
            int userId,
            NotificationCategory category,
            string title,
            string body,
            string type,
            int? relatedEntityId = null,
            string? relatedEntityType = null,
            string? actionUrl = null,
            string? metadataJson = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                if (userId <= 0 || string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(body))
                    return;

                await _inApp.CreateSafeAsync(
                    userId,
                    title,
                    body,
                    type,
                    category,
                    relatedEntityId,
                    relatedEntityType,
                    actionUrl,
                    metadataJson,
                    cancellationToken);

                await _push.NotifyAsync(userId, category, title, body, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "خطا در UserAppNotifier — UserId={UserId}, Type={Type}",
                    userId, type);
            }
        }
    }
}
