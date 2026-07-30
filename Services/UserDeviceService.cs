using Api_Vapp.Data;
using Api_Vapp.DTOs.Common;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api_Vapp.Services
{
    public class UserDeviceService : IUserDeviceService
    {
        private readonly Api_Context _context;
        private readonly IUserRepository _userRepository;
        private readonly ILogger<UserDeviceService> _logger;

        public UserDeviceService(
            Api_Context context,
            IUserRepository userRepository,
            ILogger<UserDeviceService> logger)
        {
            _context = context;
            _userRepository = userRepository;
            _logger = logger;
        }

        public async Task<ApiResponse<object>> RegisterFcmTokenAsync(int userId, string token)
        {
            if (userId <= 0)
                return ApiResponse<object>.BadRequest("شناسه کاربر نامعتبر است");

            var normalized = token?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalized))
                return ApiResponse<object>.BadRequest("توکن الزامی است");

            if (normalized.Length > 512)
                return ApiResponse<object>.BadRequest("توکن بیش از حد طولانی است");

            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null || user.IsDeleted)
                return ApiResponse<object>.NotFound("کاربر یافت نشد");

            var now = DateTime.UtcNow;

            // اگر همین توکن قبلاً ثبت شده: به کاربر فعلی وصل و فعال کن (بدون ارور)
            var byToken = await _context.UserDevices
                .FirstOrDefaultAsync(d => d.FcmToken == normalized);

            if (byToken != null)
            {
                byToken.UserId = userId;
                byToken.IsActive = true;
                byToken.LastSeenAt = now;
                byToken.UpdatedAt = now;
                await _context.SaveChangesAsync();

                _logger.LogInformation(
                    "FCM token upserted (existing) for user {UserId}, device {DeviceId}",
                    userId, byToken.Id);

                return ApiResponse<object>.CreateSuccess(
                    new { id = byToken.Id },
                    "توکن با موفقیت به‌روزرسانی شد");
            }

            // توکن جدید برای این کاربر
            var device = new UserDevice
            {
                UserId = userId,
                FcmToken = normalized,
                IsActive = true,
                CreatedAt = now,
                LastSeenAt = now
            };

            await _context.UserDevices.AddAsync(device);
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "FCM token registered (new) for user {UserId}, device {DeviceId}",
                userId, device.Id);

            return ApiResponse<object>.CreateSuccess(
                new { id = device.Id },
                "توکن با موفقیت ثبت شد");
        }
    }
}
