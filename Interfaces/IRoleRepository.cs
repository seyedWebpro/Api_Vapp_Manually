using Api_Vapp.Models;
using Api_Vapp._Utilities;

namespace Api_Vapp.Interfaces
{
    /// <summary>
    /// رابط Repository برای Role
    /// شامل متدهای خاص مربوط به Role
    /// </summary>
    public interface IRoleRepository : IBaseRepository<Role>
    {
        /// <summary>
        /// یافتن نقش بر اساس نام
        /// </summary>
        Task<Role?> GetByNameAsync(string name);

        /// <summary>
        /// بررسی وجود نقش با نام
        /// </summary>
        Task<bool> ExistsByNameAsync(string name);

        /// <summary>
        /// دریافت نقش‌های فعال
        /// </summary>
        Task<IEnumerable<Role>> GetActiveRolesAsync();
    }
}

