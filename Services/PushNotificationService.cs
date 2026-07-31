using Api_Vapp.Configuration;
using Api_Vapp.Data;
using Api_Vapp.DTOs.Device;
using Api_Vapp.Interfaces;
using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Api_Vapp.Services
{
    public class PushNotificationService : IPushNotificationService
    {
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
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("شروع ارسال Push — UserId={UserId}", userId);

            var result = new PushDeliveryResultDto();

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
                    "پایان ارسال Push — دستگاه فعالی نیست — UserId={UserId}",
                    userId);
                return result;
            }

            foreach (var (deviceId, token) in devices)
            {
                var ok = await SendToTokenInternalAsync(userId, deviceId, token, title, body, cancellationToken);
                if (ok)
                    result.SentCount++;
                else
                    result.FailedCount++;
            }

            _logger.LogInformation(
                "پایان ارسال Push — UserId={UserId}, DeviceCount={DeviceCount}, SentCount={SentCount}, FailedCount={FailedCount}",
                userId, result.DeviceCount, result.SentCount, result.FailedCount);

            return result;
        }

        public async Task<bool> SendToTokenAsync(
            string fcmToken,
            string title,
            string body,
            CancellationToken cancellationToken = default)
        {
            return await SendToTokenInternalAsync(null, null, fcmToken, title, body, cancellationToken);
        }

        private async Task<bool> SendToTokenInternalAsync(
            int? userId,
            int? deviceId,
            string fcmToken,
            string title,
            string body,
            CancellationToken cancellationToken)
        {
            if (!EnsureInitialized())
                return false;

            if (string.IsNullOrWhiteSpace(fcmToken))
                return false;

            var tokenPrefix = TokenPrefix(fcmToken);

            try
            {
                var message = new Message
                {
                    Token = fcmToken.Trim(),
                    Notification = new Notification
                    {
                        Title = title,
                        Body = body
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
    }
}
