using Api_Vapp.Configuration;
using Api_Vapp.Data;
using Api_Vapp.Interfaces;
using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Api_Vapp.Services
{
    public class PushNotificationService : IPushNotificationService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly FirebaseOptions _options;
        private readonly ILogger<PushNotificationService> _logger;
        private readonly object _initLock = new();
        private bool _initialized;
        private bool _available;

        public PushNotificationService(
            IServiceScopeFactory scopeFactory,
            IOptions<FirebaseOptions> options,
            ILogger<PushNotificationService> logger)
        {
            _scopeFactory = scopeFactory;
            _options = options.Value;
            _logger = logger;
        }

        public async Task<int> SendToUserAsync(int userId, string title, string body, CancellationToken cancellationToken = default)
        {
            if (!EnsureInitialized())
                return 0;

            List<string> tokens;
            using (var scope = _scopeFactory.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<Api_Context>();
                tokens = await db.UserDevices
                    .AsNoTracking()
                    .Where(d => d.UserId == userId && d.IsActive)
                    .Select(d => d.FcmToken)
                    .ToListAsync(cancellationToken);
            }

            if (tokens.Count == 0)
            {
                _logger.LogDebug("No active FCM tokens for user {UserId}", userId);
                return 0;
            }

            var sent = 0;
            foreach (var token in tokens)
            {
                if (await SendToTokenAsync(token, title, body, cancellationToken))
                    sent++;
            }

            return sent;
        }

        public async Task<bool> SendToTokenAsync(string fcmToken, string title, string body, CancellationToken cancellationToken = default)
        {
            if (!EnsureInitialized())
                return false;

            if (string.IsNullOrWhiteSpace(fcmToken))
                return false;

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
                _logger.LogInformation("FCM sent successfully: {MessageId}", messageId);
                return true;
            }
            catch (FirebaseMessagingException ex) when (
                ex.MessagingErrorCode == MessagingErrorCode.Unregistered ||
                ex.MessagingErrorCode == MessagingErrorCode.InvalidArgument)
            {
                _logger.LogWarning(ex, "Invalid/unregistered FCM token; deactivating");
                await DeactivateTokenAsync(fcmToken);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send FCM to token");
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
                        }
                        else if (!string.IsNullOrWhiteSpace(_options.CredentialsPath))
                        {
                            var path = _options.CredentialsPath;
                            if (!Path.IsPathRooted(path))
                                path = Path.Combine(Directory.GetCurrentDirectory(), path);

                            if (!File.Exists(path))
                            {
                                _logger.LogWarning("Firebase credentials file not found: {Path}", path);
                                _available = false;
                                _initialized = true;
                                return false;
                            }

                            credential = GoogleCredential.FromFile(path);
                        }
                        else
                        {
                            _logger.LogWarning(
                                "Firebase is not configured. Set Firebase:CredentialsPath or Firebase:CredentialsJson.");
                            _available = false;
                            _initialized = true;
                            return false;
                        }

                        FirebaseApp.Create(new AppOptions { Credential = credential });
                    }

                    _available = true;
                    _logger.LogInformation("Firebase Admin SDK initialized");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to initialize Firebase Admin SDK");
                    _available = false;
                }

                _initialized = true;
                return _available;
            }
        }

        private async Task DeactivateTokenAsync(string fcmToken)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<Api_Context>();
                var devices = await db.UserDevices
                    .Where(d => d.FcmToken == fcmToken && d.IsActive)
                    .ToListAsync();

                if (devices.Count == 0)
                    return;

                var now = DateTime.UtcNow;
                foreach (var device in devices)
                {
                    device.IsActive = false;
                    device.UpdatedAt = now;
                }

                await db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to deactivate invalid FCM token");
            }
        }
    }
}
