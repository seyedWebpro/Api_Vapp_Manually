using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.Device;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;
using Api_Vapp.Utilities;
using Microsoft.Extensions.Logging;

namespace Api_Vapp.Services
{
    public class UserDeviceService : IUserDeviceService
    {
        private readonly IUserDeviceRepository _deviceRepository;
        private readonly IUserRepository _userRepository;
        private readonly ILogger<UserDeviceService> _logger;

        public UserDeviceService(
            IUserDeviceRepository deviceRepository,
            IUserRepository userRepository,
            ILogger<UserDeviceService> logger)
        {
            _deviceRepository = deviceRepository;
            _userRepository = userRepository;
            _logger = logger;
        }

        public async Task<ApiResponse<object>> RegisterFcmTokenAsync(int userId, string token)
        {
            _logger.LogInformation("شروع ثبت توکن FCM — UserId={UserId}", userId);

            try
            {
                if (userId <= 0)
                {
                    return ApiResponse<object>.BadRequest(
                        ControlledErrorHelper.InvalidInput,
                        errorCode: ErrorCodes.InvalidUserId);
                }

                var normalized = token?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(normalized))
                {
                    return ApiResponse<object>.BadRequest(
                        "توکن الزامی است",
                        errorCode: ErrorCodes.ValidationFailed);
                }

                if (normalized.Length > 512)
                {
                    return ApiResponse<object>.BadRequest(
                        "توکن بیش از حد طولانی است",
                        errorCode: ErrorCodes.InvalidInput);
                }

                var user = await _userRepository.GetByIdAsync(userId);
                if (user == null || user.IsDeleted)
                {
                    _logger.LogWarning("ثبت توکن FCM برای کاربر نامعتبر — UserId={UserId}", userId);
                    return ApiResponse<object>.NotFound(ControlledErrorHelper.NotFound, ErrorCodes.NotFound);
                }

                var tokenPrefix = TokenPrefix(normalized);
                var now = DateTime.UtcNow;
                var existing = await _deviceRepository.GetByTokenAsync(normalized);

                if (existing != null)
                {
                    var previousUserId = existing.UserId;
                    existing.UserId = userId;
                    existing.IsActive = true;
                    existing.IsDeleted = false;
                    existing.LastSeenAt = now;

                    await _deviceRepository.UpdateAsync(existing);

                    _logger.LogInformation(
                        "پایان ثبت توکن FCM (به‌روزرسانی) — UserId={UserId}, DeviceId={DeviceId}, TokenPrefix={TokenPrefix}, PreviousUserId={PreviousUserId}",
                        userId, existing.Id, tokenPrefix, previousUserId);

                    return ApiResponse<object>.CreateSuccess(
                        new { id = existing.Id, updated = true },
                        "توکن با موفقیت به‌روزرسانی شد");
                }

                var device = new UserDevice
                {
                    UserId = userId,
                    FcmToken = normalized,
                    IsActive = true,
                    IsDeleted = false,
                    CreatedAt = now,
                    LastSeenAt = now
                };

                await _deviceRepository.AddAsync(device);

                _logger.LogInformation(
                    "پایان ثبت توکن FCM (جدید) — UserId={UserId}, DeviceId={DeviceId}, TokenPrefix={TokenPrefix}",
                    userId, device.Id, tokenPrefix);

                return ApiResponse<object>.CreateSuccess(
                    new { id = device.Id, updated = false },
                    "توکن با موفقیت ثبت شد");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در ثبت توکن FCM — UserId={UserId}", userId);
                return ApiResponse<object>.InternalServerError(
                    ControlledErrorHelper.Database,
                    ErrorCodes.DatabaseError);
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
