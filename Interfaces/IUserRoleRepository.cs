using Api_Vapp.Models;
using Api_Vapp._Utilities;

namespace Api_Vapp.Interfaces
{
    /// <summary>
    /// رابط Repository برای UserRole
    /// شامل متدهای خاص مربوط به UserRole
    /// </summary>
    public interface IUserRoleRepository : IBaseRepository<UserRole>
    {
        /// <summary>
        /// یافتن نقش‌های یک کاربر
        /// </summary>
        Task<IEnumerable<UserRole>> GetUserRolesAsync(int userId);

        /// <summary>
        /// یافتن کاربران یک نقش
        /// </summary>
        Task<IEnumerable<UserRole>> GetRoleUsersAsync(int roleId);

        /// <summary>
        /// بررسی وجود رابطه کاربر-نقش
        /// </summary>
        Task<bool> ExistsAsync(int userId, int roleId);

        /// <summary>
        /// یافتن رابطه کاربر-نقش
        /// </summary>
        Task<UserRole?> GetUserRoleAsync(int userId, int roleId);

        /// <summary>
        /// دریافت نقش‌های فعال یک کاربر
        /// </summary>
        Task<IEnumerable<UserRole>> GetActiveUserRolesAsync(int userId);
    }
}

