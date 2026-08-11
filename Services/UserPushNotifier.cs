using Api_Vapp.Constants;
using Api_Vapp.Data;
using Api_Vapp.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;

namespace Api_Vapp.Services
{
    public class UserPushNotifier : IUserPushNotifier
    {
        private static readonly TimeSpan DuplicateCooldown = TimeSpan.FromMinutes(10);
        private static readonly ConcurrentDictionary<string, DateTime> LastPushByKey = new();

        private readonly IPushNotificationService _push;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<UserPushNotifier> _logger;

        public UserPushNotifier(
            IPushNotificationService push,
            IServiceScopeFactory scopeFactory,
            ILogger<UserPushNotifier> logger)
        {
            _push = push;
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        public async Task NotifyAsync(
            int userId,
            NotificationCategory category,
            string title,
            string body,
            CancellationToken cancellationToken = default)
        {
            try
            {
                if (userId <= 0 || string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(body))
                    return;

                var normalizedTitle = title.Trim();
                var normalizedBody = body.Trim();

                if (!IsAllowedPush(category, normalizedTitle))
                {
                    _logger.LogInformation(
                        "Push blocked by policy — UserId={UserId}, Category={Category}, Title={Title}",
                        userId, category, normalizedTitle);
                    return;
                }

                if (IsDuplicate(userId, category, normalizedTitle, normalizedBody))
                {
                    _logger.LogInformation(
                        "Push suppressed as duplicate — UserId={UserId}, Category={Category}, Title={Title}",
                        userId, category, normalizedTitle);
                    return;
                }

                var result = await _push.SendToUserAsync(userId, normalizedTitle, normalizedBody, category, cancellationToken);

                _logger.LogInformation(
                    "Push notify — UserId={UserId}, Category={Category}, Skipped={Skipped}, Sent={Sent}, Devices={Devices}",
                    userId, category, result.SkippedByPreference, result.SentCount, result.DeviceCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "خطا در Notify Push — UserId={UserId}, Category={Category}",
                    userId, category);
            }
        }

        public async Task<int> NotifyBroadcastAsync(
            NotificationCategory category,
            string title,
            string body,
            CancellationToken cancellationToken = default)
        {
            var sentUsers = 0;
            try
            {
                if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(body))
                    return 0;

                var normalizedTitle = title.Trim();
                var normalizedBody = body.Trim();

                if (!IsAllowedPush(category, normalizedTitle))
                {
                    _logger.LogInformation(
                        "Push broadcast blocked by policy — Category={Category}, Title={Title}",
                        category, normalizedTitle);
                    return 0;
                }

                List<int> userIds;
                using (var scope = _scopeFactory.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<Api_Context>();
                    var query = db.UserNotificationSettings.AsNoTracking().Where(s => s.PushEnabled);

                    query = category switch
                    {
                        NotificationCategory.ImportantNotifications => query.Where(s => s.ImportantNotifications),
                        NotificationCategory.Updates => query.Where(s => s.Updates),
                        NotificationCategory.SystemWarnings => query.Where(s => s.SystemWarnings),
                        NotificationCategory.WalletTransaction => query.Where(s => s.WalletTransaction),
                        NotificationCategory.CustomerCashback => query.Where(s => s.CustomerCashback),
                        NotificationCategory.FinancialReport => query.Where(s => s.FinancialReport),
                        NotificationCategory.NewCustomerRegistration => query.Where(s => s.NewCustomerRegistration),
                        NotificationCategory.Suggestions => query.Where(s => s.Suggestions),
                        NotificationCategory.EducationAndTips => query.Where(s => s.EducationAndTips),
                        _ => query.Where(_ => false)
                    };

                    // فقط کاربرانی که حداقل یک دستگاه فعال دارند
                    userIds = await (
                        from s in query
                        join d in db.UserDevices.AsNoTracking()
                            on s.UserId equals d.UserId
                        where d.IsActive && !d.IsDeleted
                        select s.UserId
                    ).Distinct().ToListAsync(cancellationToken);
                }

                _logger.LogInformation(
                    "Push broadcast start — Category={Category}, Candidates={Count}",
                    category, userIds.Count);

                foreach (var userId in userIds)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (IsDuplicate(userId, category, normalizedTitle, normalizedBody))
                        continue;

                    var result = await _push.SendToUserAsync(
                        userId,
                        normalizedTitle,
                        normalizedBody,
                        category,
                        cancellationToken);
                    if (result.SentCount > 0)
                        sentUsers++;
                }

                _logger.LogInformation(
                    "Push broadcast done — Category={Category}, UsersReached={SentUsers}/{Total}",
                    category, sentUsers, userIds.Count);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در Push broadcast — Category={Category}", category);
            }

            return sentUsers;
        }

        private static bool IsAllowedPush(NotificationCategory category, string title)
        {
            if (string.IsNullOrWhiteSpace(title))
                return false;

            return category switch
            {
                NotificationCategory.Updates => string.Equals(title, "به‌روزرسانی وپ", StringComparison.Ordinal),
                NotificationCategory.EducationAndTips => string.Equals(title, "آموزش جدید در وپ", StringComparison.Ordinal),
                NotificationCategory.ImportantNotifications => IsAllowedImportantTitle(title),
                _ => false
            };
        }

        private static bool IsAllowedImportantTitle(string title)
        {
            return string.Equals(title, "اعلان مهم حساب", StringComparison.Ordinal)
                || string.Equals(title, "فعال‌سازی حساب", StringComparison.Ordinal)
                || string.Equals(title, "غیرفعال‌سازی حساب", StringComparison.Ordinal);
        }

        private static bool IsDuplicate(
            int userId,
            NotificationCategory category,
            string title,
            string body)
        {
            var now = DateTime.UtcNow;
            var key = $"{userId}|{(int)category}|{title}|{body}";

            if (LastPushByKey.TryGetValue(key, out var lastSentAt)
                && now - lastSentAt < DuplicateCooldown)
            {
                return true;
            }

            LastPushByKey[key] = now;
            TryCleanupOldEntries(now);
            return false;
        }

        private static void TryCleanupOldEntries(DateTime now)
        {
            if (LastPushByKey.Count < 5000)
                return;

            var threshold = now - (DuplicateCooldown * 2);
            foreach (var item in LastPushByKey)
            {
                if (item.Value < threshold)
                    LastPushByKey.TryRemove(item.Key, out _);
            }
        }
    }
}
