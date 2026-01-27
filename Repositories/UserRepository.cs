using Api_Vapp.Data;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;
using Api_Vapp._Utilities;
using Microsoft.EntityFrameworkCore;

namespace Api_Vapp.Repositories
{
    /// <summary>
    /// پیاده‌سازی Repository برای User
    /// تمام عملیات مربوط به دسترسی به داده‌های User در اینجا قرار دارد
    /// </summary>
    public class UserRepository : BaseRepository<User>, IUserRepository
    {
        public UserRepository(Api_Context context) : base(context)
        {
        }

        public async Task<User?> GetByPhoneNumberAsync(string phoneNumber)
        {
            return await _dbSet
                .FirstOrDefaultAsync(u => u.PhoneNumber == phoneNumber);
        }

        public async Task<User?> GetByNationalIdAsync(string nationalId)
        {
            return await _dbSet
                .FirstOrDefaultAsync(u => u.NationalId == nationalId);
        }

        public async Task<bool> ExistsByPhoneNumberAsync(string phoneNumber)
        {
            return await _dbSet
                .AnyAsync(u => u.PhoneNumber == phoneNumber && !u.IsDeleted);
        }

        public async Task<bool> ExistsByNationalIdAsync(string nationalId)
        {
            return await _dbSet
                .AnyAsync(u => u.NationalId == nationalId && !u.IsDeleted);
        }

        public async Task UpdateLastLoginAsync(int userId)
        {
            var user = await GetByIdAsync(userId);
            if (user != null)
            {
                user.LastLoginAt = DateTime.UtcNow;
                await UpdateAsync(user);
            }
        }

        public async Task<User?> GetActiveUserByPhoneAsync(string phoneNumber)
        {
            return await _dbSet
                .FirstOrDefaultAsync(u => u.PhoneNumber == phoneNumber 
                    && u.IsActive 
                    && !u.IsDeleted);
        }

        public async Task<User> GetOrCreateDefaultUserAsync()
        {
            // سعی می‌کنیم اولین کاربر فعال را پیدا کنیم
            var firstActiveUser = await _dbSet
                .FirstOrDefaultAsync(u => u.IsActive && !u.IsDeleted);

            if (firstActiveUser != null)
            {
                return firstActiveUser;
            }

            // اگر کاربری وجود نداشت، یک کاربر پیش‌فرض ایجاد می‌کنیم
            var defaultUser = new User
            {
                PhoneNumber = "09120000000",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("DefaultPassword123!"),
                FullName = "کاربر پیش‌فرض",
                IsActive = true,
                IsPhoneVerified = true,
                CreatedAt = DateTime.UtcNow
            };

            await AddAsync(defaultUser);
            return defaultUser;
        }

        public override async Task<User?> GetByIdAsync(int id)
        {
            return await _dbSet
                .FirstOrDefaultAsync(u => u.Id == id && !u.IsDeleted);
        }
    }
}



