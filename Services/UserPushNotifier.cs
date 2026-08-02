using Api_Vapp.Constants;
using Api_Vapp.Data;
using Api_Vapp.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api_Vapp.Services
{
    public class UserPushNotifier : IUserPushNotifier
    {
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

                var result = await _push.SendToUserAsync(userId, title.Trim(), body.Trim(), category, cancellationToken);

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
                    var before = sentUsers;
                    var result = await _push.SendToUserAsync(userId, title, body, category, cancellationToken);
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
    }
}
