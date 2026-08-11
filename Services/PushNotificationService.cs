using Api_Vapp.Configuration;
using Api_Vapp.Constants;
using Api_Vapp.Data;
using Api_Vapp.DTOs.Device;
using Api_Vapp.Interfaces;
using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace Api_Vapp.Services
{
    public class PushNotificationService : IPushNotificationService
    {
        private static readonly TimeSpan DuplicateCooldown = TimeSpan.FromMinutes(10);
        private static readonly ConcurrentDictionary<string, DateTime> LastPushByKey = new();

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly FirebaseOptions _options;
        private readonly IHostEnvironment _environment;
        private readonly ILogger<PushNotificationService> _logger;
        private readonly object _initLock = new();
        private bool _initialized;
        private bool _available;

        public PushNotificationService(
            IServiceScopeFactory scopeFactory,
            IOptions<FirebaseOptions> options,
            IHostEnvironment environment,
            ILogger<PushNotificationService> logger)
        {
            _scopeFactory = scopeFactory;
            _options = options.Value;
            _environment = environment;
            _logger = logger;
        }

        public bool TryInitialize() => EnsureInitialized();

        public async Task<PushDeliveryResultDto> SendToUserAsync(
            int userId,
            string title,
            string body,
            NotificationCategory category,
            CancellationToken cancellationToken = default)
        {
            title = title?.Trim() ?? string.Empty;
            body = body?.Trim() ?? string.Empty;

            _logger.LogInformation(
                "شروع ارسال Push — UserId={UserId}, Category={Category}",
                userId, category);

            var result = new PushDeliveryResultDto
            {
                Category = category.ToString()
            };

            if (!IsAllowedPush(category, title))
            {
                _logger.LogInformation(
                    "Push blocked by global policy — UserId={UserId}, Category={Category}, Title={Title}",
                    userId, category, title);
                return result;
            }

            if (IsDuplicate(userId, category, title, body))
            {
                _logger.LogInformation(
                    "Push suppressed as duplicate by global policy — UserId={UserId}, Category={Category}, Title={Title}",
                    userId, category, title);
                return result;
            }

            // ۱) چک سریع تنظیمات پروفایل — قبل از Firebase و قبل از خواندن دستگاه‌ها
            bool preferenceAllowed;
            using (var scope = _scopeFactory.CreateScope())
            {
                var settingsRepo = scope.ServiceProvider.GetRequiredService<IUserNotificationSettingsRepository>();
                var allowed = await settingsRepo.IsPushAllowedAsync(userId, category, cancellationToken);

                // null = ردیف تنظیمات نیست → پیش‌فرض: PushEnabled=true و فلگ‌های پیش‌فرض مدل
                preferenceAllowed = allowed ?? IsCategoryEnabledByDefault(category);
            }

            result.PreferenceAllowed = preferenceAllowed;
            if (!preferenceAllowed)
            {
                result.SkippedByPreference = true;
                _logger.LogInformation(
                    "ارسال Push رد شد به‌خاطر تنظیمات کاربر — UserId={UserId}, Category={Category}",
                    userId, category);
                return result;
            }

            if (!EnsureInitialized())
            {
                result.FirebaseReady = false;
                _logger.LogWarning("ارسال Push لغو شد — Firebase آماده نیست — UserId={UserId}", userId);
                return result;
            }

            result.FirebaseReady = true;

            List<(int DeviceId, string Token)> devices;
            using (var scope = _scopeFactory.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<Api_Context>();
                devices = await db.UserDevices
                    .AsNoTracking()
                    .Where(d => d.UserId == userId && d.IsActive && !d.IsDeleted)
                    .Select(d => new ValueTuple<int, string>(d.Id, d.FcmToken))
                    .ToListAsync(cancellationToken);
            }

            result.DeviceCount = devices.Count;
            if (devices.Count == 0)
            {
                _logger.LogInformation(
                    "پایان ارسال Push — دستگاه فعالی نیست — UserId={UserId}, Category={Category}",
                    userId, category);
                return result;
            }

            foreach (var (deviceId, token) in devices)
            {
                var ok = await SendToTokenInternalAsync(
                    userId, deviceId, token, title, body, category.ToString(), cancellationToken);
                if (ok)
                    result.SentCount++;
                else
                    result.FailedCount++;
            }

            _logger.LogInformation(
                "پایان ارسال Push — UserId={UserId}, Category={Category}, DeviceCount={DeviceCount}, SentCount={SentCount}, FailedCount={FailedCount}",
                userId, category, result.DeviceCount, result.SentCount, result.FailedCount);

            return result;
        }

        public async Task<bool> SendToTokenAsync(
            string fcmToken,
            string title,
            string body,
            CancellationToken cancellationToken = default)
        {
            return await SendToTokenInternalAsync(
                null, null, fcmToken, title, body, category: null, cancellationToken);
        }

        /// <summary>
        /// پیش‌فرض‌های مدل وقتی ردیف تنظیمات هنوز ساخته نشده
        /// </summary>
        private static bool IsCategoryEnabledByDefault(NotificationCategory category) =>
            category switch
            {
                NotificationCategory.ImportantNotifications => true,
                NotificationCategory.Updates => false,
                NotificationCategory.SystemWarnings => true,
                NotificationCategory.WalletTransaction => true,
                NotificationCategory.CustomerCashback => true,
                NotificationCategory.FinancialReport => false,
                NotificationCategory.NewCustomerRegistration => true,
                NotificationCategory.Suggestions => true,
                NotificationCategory.EducationAndTips => false,
                _ => false
            };

        private async Task<bool> SendToTokenInternalAsync(
            int? userId,
            int? deviceId,
            string fcmToken,
            string title,
            string body,
            string? category,
            CancellationToken cancellationToken)
        {
            if (!EnsureInitialized())
                return false;

            if (string.IsNullOrWhiteSpace(fcmToken))
                return false;

            var tokenPrefix = TokenPrefix(fcmToken);
            var safeTitle = title?.Trim() ?? string.Empty;
            var safeBody = body?.Trim() ?? string.Empty;
            var safeCategory = string.IsNullOrWhiteSpace(category) ? "General" : category.Trim();

            try
            {
                // Android (به‌خصوص وقتی اپ باز است) و iOS به کانال/اولویت و data نیاز دارند
                var message = new Message
                {
                    Token = fcmToken.Trim(),
                    Notification = new Notification
                    {
                        Title = safeTitle,
                        Body = safeBody
                    },
                    Data = new Dictionary<string, string>
                    {
                        ["title"] = safeTitle,
                        ["body"] = safeBody,
                        ["category"] = safeCategory,
                        ["click_action"] = "FLUTTER_NOTIFICATION_CLICK"
                    },
                    Android = new AndroidConfig
                    {
                        Priority = Priority.High,
                        Notification = new AndroidNotification
                        {
                            Title = safeTitle,
                            Body = safeBody,
                            // کانال پیش‌فرض FCM — تا وقتی اپ کانال اختصاصی نساخته، امن‌تر است
                            Sound = "default",
                            DefaultSound = true,
                            DefaultVibrateTimings = true,
                            Priority = NotificationPriority.HIGH,
                            ClickAction = "FLUTTER_NOTIFICATION_CLICK"
                        }
                    },
                    Apns = new ApnsConfig
                    {
                        Headers = new Dictionary<string, string>
                        {
                            ["apns-priority"] = "10"
                        },
                        Aps = new Aps
                        {
                            Alert = new ApsAlert
                            {
                                Title = safeTitle,
                                Body = safeBody
                            },
                            Sound = "default",
                            ContentAvailable = true,
                            MutableContent = true
                        }
                    }
                };

                var messageId = await FirebaseMessaging.DefaultInstance.SendAsync(message, cancellationToken);

                _logger.LogInformation(
                    "Push ارسال شد — UserId={UserId}, DeviceId={DeviceId}, TokenPrefix={TokenPrefix}, FcmMessageId={FcmMessageId}",
                    userId, deviceId, tokenPrefix, messageId);

                return true;
            }
            catch (FirebaseMessagingException ex) when (
                ex.MessagingErrorCode == MessagingErrorCode.Unregistered ||
                ex.MessagingErrorCode == MessagingErrorCode.InvalidArgument)
            {
                _logger.LogWarning(
                    ex,
                    "توکن FCM نامعتبر/حذف‌شده — غیرفعال‌سازی — UserId={UserId}, DeviceId={DeviceId}, TokenPrefix={TokenPrefix}, MessagingErrorCode={MessagingErrorCode}",
                    userId, deviceId, tokenPrefix, ex.MessagingErrorCode);

                await DeactivateTokenAsync(fcmToken);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "خطا در ارسال Push — UserId={UserId}, DeviceId={DeviceId}, TokenPrefix={TokenPrefix}",
                    userId, deviceId, tokenPrefix);
                return false;
            }
        }

        private bool EnsureInitialized()
        {
            if (_initialized)
                return _available;

            lock (_initLock)
            {
                if (_initialized)
                    return _available;

                try
                {
                    if (FirebaseApp.DefaultInstance == null)
                    {
                        GoogleCredential credential;

                        if (!string.IsNullOrWhiteSpace(_options.CredentialsJson))
                        {
                            credential = GoogleCredential.FromJson(_options.CredentialsJson);
                            _logger.LogInformation("اعتبارنامه Firebase از CredentialsJson بارگذاری شد");
                        }
                        else if (!string.IsNullOrWhiteSpace(_options.CredentialsPath))
                        {
                            var path = ResolveCredentialsPath(_options.CredentialsPath);
                            if (path == null)
                            {
                                _logger.LogWarning(
                                    "فایل اعتبارنامه Firebase یافت نشد — ContentRoot={ContentRoot}, Cwd={Cwd}, ConfiguredPath={ConfiguredPath}",
                                    _environment.ContentRootPath,
                                    Directory.GetCurrentDirectory(),
                                    _options.CredentialsPath);
                                _available = false;
                                _initialized = true;
                                return false;
                            }

                            credential = GoogleCredential.FromFile(path);
                            _logger.LogInformation("اعتبارنامه Firebase بارگذاری شد — Path={Path}", path);
                        }
                        else
                        {
                            _logger.LogWarning("Firebase پیکربندی نشده است (CredentialsPath/CredentialsJson خالی است)");
                            _available = false;
                            _initialized = true;
                            return false;
                        }

                        FirebaseApp.Create(new AppOptions { Credential = credential });
                    }

                    _available = true;
                    _logger.LogInformation("Firebase Admin SDK آماده است");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "خطا در راه‌اندازی Firebase Admin SDK");
                    _available = false;
                }

                _initialized = true;
                return _available;
            }
        }

        private string? ResolveCredentialsPath(string configuredPath)
        {
            if (Path.IsPathRooted(configuredPath) && File.Exists(configuredPath))
                return configuredPath;

            var candidates = new[]
            {
                Path.Combine(_environment.ContentRootPath, configuredPath),
                Path.Combine(Directory.GetCurrentDirectory(), configuredPath),
                configuredPath
            };

            return candidates.FirstOrDefault(File.Exists);
        }

        private async Task DeactivateTokenAsync(string fcmToken)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<IUserDeviceRepository>();
                var device = await repo.GetByTokenAsync(fcmToken);
                if (device == null)
                    return;

                device.IsActive = false;
                await repo.UpdateAsync(device);

                _logger.LogInformation(
                    "دستگاه FCM غیرفعال شد — DeviceId={DeviceId}, UserId={UserId}, TokenPrefix={TokenPrefix}",
                    device.Id, device.UserId, TokenPrefix(fcmToken));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در غیرفعال‌سازی توکن FCM — TokenPrefix={TokenPrefix}", TokenPrefix(fcmToken));
            }
        }

        private static string TokenPrefix(string token)
        {
            if (string.IsNullOrEmpty(token))
                return "(empty)";
            return token.Length <= 12 ? token : token[..12];
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
