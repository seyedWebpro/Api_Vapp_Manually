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
            var settings = await GetByUserIdAsync(userId);
            
            if (settings == null)
            {
                settings = new UserNotificationSettings
                {
                    UserId = userId,
                    ImportantNotifications = true,
                    Updates = false,
                    SystemWarnings = true,
                    WalletTransaction = false,
                    CustomerCashback = true,
                    FinancialReport = false,
                    NewCustomerRegistration = false,
                    Suggestions = true,
                    EducationAndTips = false
                };
                
                settings = await AddAsync(settings);
            }
            
            return settings;
        }
    }
}



