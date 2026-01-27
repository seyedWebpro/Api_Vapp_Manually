using Api_Vapp.Models;
using Api_Vapp._Utilities;

namespace Api_Vapp.Interfaces
{
    /// <summary>
    /// رابط Repository برای User
    /// شامل متدهای خاص مربوط به User
    /// </summary>
    public interface IUserRepository : IBaseRepository<User>
    {
        /// <summary>
        /// یافتن کاربر بر اساس شماره تلفن
        /// </summary>
        Task<User?> GetByPhoneNumberAsync(string phoneNumber);

        /// <summary>
        /// یافتن کاربر بر اساس کد ملی
        /// </summary>
        Task<User?> GetByNationalIdAsync(string nationalId);

        /// <summary>
        /// بررسی وجود کاربر با شماره تلفن
        /// </summary>
        Task<bool> ExistsByPhoneNumberAsync(string phoneNumber);

        /// <summary>
        /// بررسی وجود کاربر با کد ملی
        /// </summary>
        Task<bool> ExistsByNationalIdAsync(string nationalId);

        /// <summary>
        /// به‌روزرسانی زمان آخرین ورود
        /// </summary>
        Task UpdateLastLoginAsync(int userId);

        /// <summary>
        /// یافتن کاربر فعال بر اساس شماره تلفن
        /// </summary>
        Task<User?> GetActiveUserByPhoneAsync(string phoneNumber);

        /// <summary>
        /// یافتن اولین کاربر فعال یا ایجاد کاربر پیش‌فرض (برای حالت DisableAuth)
        /// </summary>
        Task<User> GetOrCreateDefaultUserAsync();
    }
}



