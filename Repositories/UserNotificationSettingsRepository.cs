using Api_Vapp.Constants;
using Api_Vapp.Data;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;
using Microsoft.EntityFrameworkCore;

namespace Api_Vapp.Repositories
{
    /// <summary>
    /// پیاده‌سازی Repository برای تنظیمات اعلان‌های کاربر
    /// </summary>
    public class UserNotificationSettingsRepository : IUserNotificationSettingsRepository
    {
        private readonly Api_Context _context;

        public UserNotificationSettingsRepository(Api_Context context)
        {
            _context = context;
        }

        public async Task<UserNotificationSettings?> GetByUserIdAsync(int userId)
        {
            return await _context.UserNotificationSettings
                .AsNoTracking()
                .FirstOrDefaultAsync(uns => uns.UserId == userId);
        }

        public async Task<UserNotificationSettings> AddAsync(UserNotificationSettings settings)
        {
            settings.CreatedAt = DateTime.UtcNow;
            await _context.UserNotificationSettings.AddAsync(settings);
            await _context.SaveChangesAsync();
            return settings;
        }

        public async Task<UserNotificationSettings> UpdateAsync(UserNotificationSettings settings)
        {
            settings.UpdatedAt = DateTime.UtcNow;
            _context.UserNotificationSettings.Update(settings);
            await _context.SaveChangesAsync();
            return settings;
        }

        public async Task<UserNotificationSettings> GetOrCreateAsync(int userId)
        {
            var settings = await _context.UserNotificationSettings
                .FirstOrDefaultAsync(uns => uns.UserId == userId);

            if (settings == null)
            {
                settings = new UserNotificationSettings
                {
                    UserId = userId,
                    PushEnabled = true,
                    ImportantNotifications = true,
                    Updates = false,
                    SystemWarnings = true,
                    WalletTransaction = true,
                    CustomerCashback = true,
                    FinancialReport = false,
                    NewCustomerRegistration = true,
                    Suggestions = true,
                    EducationAndTips = false
                };

                settings = await AddAsync(settings);
            }

            return settings;
        }

        public async Task<bool?> IsPushAllowedAsync(
            int userId,
            NotificationCategory category,
            CancellationToken cancellationToken = default)
        {
            // یک query سبک: فقط PushEnabled + فلگ دسته — بدون Load کل entity
            var row = await _context.UserNotificationSettings
                .AsNoTracking()
                .Where(s => s.UserId == userId)
                .Select(s => new
                {
                    s.PushEnabled,
                    s.ImportantNotifications,
                    s.Updates,
                    s.SystemWarnings,
                    s.WalletTransaction,
                    s.CustomerCashback,
                    s.FinancialReport,
                    s.NewCustomerRegistration,
                    s.Suggestions,
                    s.EducationAndTips
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (row == null)
                return null;

            if (!row.PushEnabled)
                return false;

            return category switch
            {
                NotificationCategory.ImportantNotifications => row.ImportantNotifications,
                NotificationCategory.Updates => row.Updates,
                NotificationCategory.SystemWarnings => row.SystemWarnings,
                NotificationCategory.WalletTransaction => row.WalletTransaction,
                NotificationCategory.CustomerCashback => row.CustomerCashback,
                NotificationCategory.FinancialReport => row.FinancialReport,
                NotificationCategory.NewCustomerRegistration => row.NewCustomerRegistration,
                NotificationCategory.Suggestions => row.Suggestions,
                NotificationCategory.EducationAndTips => row.EducationAndTips,
                _ => false
            };
        }
    }
}
