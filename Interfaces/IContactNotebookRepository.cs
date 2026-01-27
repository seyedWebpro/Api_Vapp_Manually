using Api_Vapp.Models;
using Api_Vapp._Utilities;

namespace Api_Vapp.Interfaces
{
    /// <summary>
    /// رابط Repository برای ContactNotebook
    /// </summary>
    public interface IContactNotebookRepository : IBaseRepository<ContactNotebook>
    {
        /// <summary>
        /// دریافت تمام دفترچه‌های یک کاربر
        /// </summary>
        Task<IEnumerable<ContactNotebook>> GetByUserIdAsync(int userId, bool? isActive = null);

        /// <summary>
        /// بررسی وجود دفترچه با نام برای کاربر
        /// </summary>
        Task<bool> ExistsByNameForUserAsync(int userId, string name, int? excludeId = null);

        /// <summary>
        /// دریافت دفترچه با مخاطبین
        /// </summary>
        Task<ContactNotebook?> GetByIdWithContactsAsync(int id);
    }
}


