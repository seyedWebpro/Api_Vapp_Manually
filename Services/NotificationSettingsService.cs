using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.User;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;
using Api_Vapp.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api_Vapp.Services
{
    /// <summary>
    /// پیاده‌سازی سرویس تنظیمات اعلان‌های کاربر
    /// </summary>
    public class NotificationSettingsService : INotificationSettingsService
    {
        private readonly IUserNotificationSettingsRepository _repository;
        private readonly IUserRepository _userRepository;
        private readonly ILogger<NotificationSettingsService> _logger;

        public NotificationSettingsService(
            IUserNotificationSettingsRepository repository,
            IUserRepository userRepository,
            ILogger<NotificationSettingsService> logger)
        {
            _repository = repository;
            _userRepository = userRepository;
            _logger = logger;
        }

        public async Task<ApiResponse<NotificationSettingsDto>> GetSettingsAsync(int userId)
        {
            _logger.LogInformation("شروع دریافت تنظیمات اعلان — UserId={UserId}", userId);

            try
            {
                if (userId <= 0)
                {
                    return ApiResponse<NotificationSettingsDto>.BadRequest(
                        ControlledErrorHelper.InvalidInput,
                        errorCode: ErrorCodes.InvalidUserId);
                }

                var user = await _userRepository.GetByIdAsync(userId);
                if (user == null || user.IsDeleted)
                {
                    _logger.LogWarning("دریافت تنظیمات اعلان برای کاربر نامعتبر — UserId={UserId}", userId);
                    return ApiResponse<NotificationSettingsDto>.NotFound(
                        ControlledErrorHelper.NotFound,
                        ErrorCodes.NotFound);
                }

                UserNotificationSettings settings;
                try
                {
                    settings = await _repository.GetOrCreateAsync(userId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "خطا در دریافت/ایجاد تنظیمات اعلان — UserId={UserId}", userId);
                    return ApiResponse<NotificationSettingsDto>.InternalServerError(
                        ControlledErrorHelper.Database,
                        ErrorCodes.DatabaseError);
                }

                _logger.LogInformation(
                    "پایان دریافت تنظیمات اعلان — UserId={UserId}, PushEnabled={PushEnabled}",
                    userId, settings.PushEnabled);

                return ApiResponse<NotificationSettingsDto>.CreateSuccess(MapToDto(settings));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطای غیرمنتظره در دریافت تنظیمات اعلان — UserId={UserId}", userId);
                return ApiResponse<NotificationSettingsDto>.InternalServerError(
                    ControlledErrorHelper.Unexpected,
                    ErrorCodes.Unexpected);
            }
        }

        public async Task<ApiResponse<NotificationSettingsDto>> UpdateSettingsAsync(
            int userId,
            NotificationSettingsDto settingsDto)
        {
            _logger.LogInformation("شروع به‌روزرسانی تنظیمات اعلان — UserId={UserId}", userId);

            try
            {
                if (userId <= 0)
                {
                    return ApiResponse<NotificationSettingsDto>.BadRequest(
                        ControlledErrorHelper.InvalidInput,
                        errorCode: ErrorCodes.InvalidUserId);
                }

                if (settingsDto == null)
                {
                    return ApiResponse<NotificationSettingsDto>.BadRequest(
                        "تنظیمات ارسال نشده است",
                        errorCode: ErrorCodes.ValidationFailed);
                }

                var user = await _userRepository.GetByIdAsync(userId);
                if (user == null || user.IsDeleted)
                {
                    _logger.LogWarning("به‌روزرسانی تنظیمات اعلان برای کاربر نامعتبر — UserId={UserId}", userId);
                    return ApiResponse<NotificationSettingsDto>.NotFound(
                        ControlledErrorHelper.NotFound,
                        ErrorCodes.NotFound);
                }

                UserNotificationSettings settings;
                try
                {
                    settings = await _repository.GetOrCreateAsync(userId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "خطا در دریافت/ایجاد تنظیمات اعلان — UserId={UserId}", userId);
                    return ApiResponse<NotificationSettingsDto>.InternalServerError(
                        ControlledErrorHelper.Database,
                        ErrorCodes.DatabaseError);
                }

                settings.PushEnabled = settingsDto.PushEnabled;
                settings.ImportantNotifications = settingsDto.ImportantNotifications;
                settings.Updates = settingsDto.Updates;
                settings.SystemWarnings = settingsDto.SystemWarnings;
                settings.WalletTransaction = settingsDto.WalletTransaction;
                settings.CustomerCashback = settingsDto.CustomerCashback;
                settings.FinancialReport = settingsDto.FinancialReport;
                settings.NewCustomerRegistration = settingsDto.NewCustomerRegistration;
                settings.Suggestions = settingsDto.Suggestions;
                settings.EducationAndTips = settingsDto.EducationAndTips;

                try
                {
                    settings = await _repository.UpdateAsync(settings);
                }
                catch (DbUpdateException ex)
                {
                    _logger.LogError(ex, "خطای دیتابیس در به‌روزرسانی تنظیمات اعلان — UserId={UserId}", userId);
                    return ApiResponse<NotificationSettingsDto>.InternalServerError(
                        ControlledErrorHelper.Database,
                        ErrorCodes.DatabaseError);
                }

                _logger.LogInformation(
                    "پایان به‌روزرسانی تنظیمات اعلان — UserId={UserId}, PushEnabled={PushEnabled}",
                    userId, settings.PushEnabled);

                return ApiResponse<NotificationSettingsDto>.CreateSuccess(
                    MapToDto(settings),
                    "تنظیمات اعلان‌ها با موفقیت به‌روزرسانی شد");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطای غیرمنتظره در به‌روزرسانی تنظیمات اعلان — UserId={UserId}", userId);
                return ApiResponse<NotificationSettingsDto>.InternalServerError(
                    ControlledErrorHelper.Unexpected,
                    ErrorCodes.Unexpected);
            }
        }

        private static NotificationSettingsDto MapToDto(UserNotificationSettings settings)
        {
            return new NotificationSettingsDto
            {
                PushEnabled = settings.PushEnabled,
                ImportantNotifications = settings.ImportantNotifications,
                Updates = settings.Updates,
                SystemWarnings = settings.SystemWarnings,
                WalletTransaction = settings.WalletTransaction,
                CustomerCashback = settings.CustomerCashback,
                FinancialReport = settings.FinancialReport,
                NewCustomerRegistration = settings.NewCustomerRegistration,
                Suggestions = settings.Suggestions,
                EducationAndTips = settings.EducationAndTips
            };
        }
    }
}
