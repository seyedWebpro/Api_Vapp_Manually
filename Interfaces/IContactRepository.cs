using Api_Vapp.Models;
using Api_Vapp._Utilities;

namespace Api_Vapp.Interfaces
{
    /// <summary>
    /// رابط Repository برای Contact
    /// </summary>
    public interface IContactRepository : IBaseRepository<Contact>
    {
        /// <summary>
        /// دریافت تمام مخاطبین یک دفترچه
        /// </summary>
        Task<IEnumerable<Contact>> GetByNotebookIdAsync(int notebookId);

        /// <summary>
        /// جستجوی مخاطبین بر اساس نام یا شماره
        /// </summary>
        Task<IEnumerable<Contact>> SearchAsync(int notebookId, string searchTerm);

        /// <summary>
        /// بررسی وجود مخاطب با شماره موبایل در دفترچه
        /// </summary>
        Task<bool> ExistsByMobileNumberInNotebookAsync(int notebookId, string mobileNumber, int? excludeId = null);

        /// <summary>
        /// دریافت مخاطب با اطلاعات تکمیلی
        /// </summary>
        Task<Contact?> GetByIdWithAdditionalInfoAsync(int id);

        /// <summary>
        /// دریافت تمام مخاطبین (بدون فیلتر کاربر)
        /// </summary>
        Task<IEnumerable<Contact>> GetAllContactsAsync();

        /// <summary>
        /// جستجوی تمام مخاطبین بر اساس نام یا شماره (بدون فیلتر کاربر)
        /// </summary>
        Task<IEnumerable<Contact>> SearchAllContactsAsync(string searchTerm);
    }
}


