using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.User;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;
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
            try
            {
                if (userId <= 0)
                {
                    return ApiResponse<NotificationSettingsDto>.BadRequest("شناسه کاربر نامعتبر است");
                }

                // بررسی وجود کاربر
                var user = await _userRepository.GetByIdAsync(userId);
                if (user == null || user.IsDeleted)
                {
                    _logger.LogWarning("درخواست تنظیمات اعلان‌ها برای کاربر نامعتبر یا حذف شده: {UserId}", userId);
                    return ApiResponse<NotificationSettingsDto>.NotFound("کاربر یافت نشد");
                }

                // دریافت یا ایجاد تنظیمات
                UserNotificationSettings settings;
                try
                {
                    settings = await _repository.GetOrCreateAsync(userId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "خطا در دریافت یا ایجاد تنظیمات اعلان‌ها برای کاربر {UserId}", userId);
                    return ApiResponse<NotificationSettingsDto>.InternalServerError("خطا در دریافت تنظیمات. لطفاً دوباره تلاش کنید");
                }

                var dto = MapToDto(settings);

                return ApiResponse<NotificationSettingsDto>.CreateSuccess(dto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در دریافت تنظیمات اعلان‌های کاربر {UserId}", userId);
                throw;
            }
        }

        public async Task<ApiResponse<NotificationSettingsDto>> UpdateSettingsAsync(int userId, NotificationSettingsDto settingsDto)
        {
            try
            {
                if (userId <= 0)
                {
                    return ApiResponse<NotificationSettingsDto>.BadRequest("شناسه کاربر نامعتبر است");
                }

                if (settingsDto == null)
                {
                    return ApiResponse<NotificationSettingsDto>.BadRequest("تنظیمات ارسال نشده است");
                }

                // بررسی وجود کاربر
                var user = await _userRepository.GetByIdAsync(userId);
                if (user == null || user.IsDeleted)
                {
                    _logger.LogWarning("درخواست به‌روزرسانی تنظیمات اعلان‌ها برای کاربر نامعتبر یا حذف شده: {UserId}", userId);
                    return ApiResponse<NotificationSettingsDto>.NotFound("کاربر یافت نشد");
                }

                // دریافت یا ایجاد تنظیمات
                UserNotificationSettings settings;
                try
                {
                    settings = await _repository.GetOrCreateAsync(userId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "خطا در دریافت یا ایجاد تنظیمات اعلان‌ها برای کاربر {UserId}", userId);
                    return ApiResponse<NotificationSettingsDto>.InternalServerError("خطا در دریافت تنظیمات. لطفاً دوباره تلاش کنید");
                }

                // به‌روزرسانی تنظیمات
                settings.ImportantNotifications = settingsDto.ImportantNotifications;
                settings.Updates = settingsDto.Updates;
                settings.SystemWarnings = settingsDto.SystemWarnings;
                settings.WalletTransaction = settingsDto.WalletTransaction;
                settings.CustomerCashback = settingsDto.CustomerCashback;
                settings.FinancialReport = settingsDto.FinancialReport;
                settings.NewCustomerRegistration = settingsDto.NewCustomerRegistration;
                settings.Suggestions = settingsDto.Suggestions;
                settings.EducationAndTips = settingsDto.EducationAndTips;

                UserNotificationSettings updatedSettings;
                try
                {
                    updatedSettings = await _repository.UpdateAsync(settings);
                }
                catch (Microsoft.EntityFrameworkCore.DbUpdateException ex)
                {
                    _logger.LogError(ex, "خطا در به‌روزرسانی دیتابیس تنظیمات اعلان‌ها برای کاربر {UserId}", userId);
                    return ApiResponse<NotificationSettingsDto>.InternalServerError("خطا در ذخیره‌سازی تنظیمات. لطفاً دوباره تلاش کنید");
                }

                var dto = MapToDto(updatedSettings);

                _logger.LogInformation("تنظیمات اعلان‌های کاربر {UserId} با موفقیت به‌روزرسانی شد", userId);

                return ApiResponse<NotificationSettingsDto>.CreateSuccess(dto, "تنظیمات اعلان‌ها با موفقیت به‌روزرسانی شد");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در به‌روزرسانی تنظیمات اعلان‌های کاربر {UserId}", userId);
                throw;
            }
        }

        private NotificationSettingsDto MapToDto(UserNotificationSettings settings)
        {
            return new NotificationSettingsDto
            {
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



