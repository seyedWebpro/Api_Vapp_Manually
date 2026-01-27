using Api_Vapp.Models;
using Api_Vapp._Utilities;

namespace Api_Vapp.Interfaces
{
    /// <summary>
    /// رابط Repository برای کش‌بک
    /// </summary>
    public interface ICashbackRepository : IBaseRepository<Cashback>
    {
        /// <summary>
        /// دریافت کش‌بک‌های کاربر با صفحه‌بندی
        /// </summary>
        Task<IEnumerable<Cashback>> GetByUserIdAsync(int userId, int pageNumber = 1, int pageSize = 10, bool? isActive = null);

        /// <summary>
        /// دریافت تعداد کش‌بک‌های کاربر
        /// </summary>
        Task<int> GetCountByUserIdAsync(int userId, bool? isActive = null);

        /// <summary>
        /// دریافت کش‌بک‌های فعال کاربر
        /// </summary>
        Task<IEnumerable<Cashback>> GetActiveByUserIdAsync(int userId);

        /// <summary>
        /// دریافت کش‌بک با شناسه و بررسی مالکیت
        /// </summary>
        Task<Cashback?> GetByIdAndUserIdAsync(int id, int userId);

        /// <summary>
        /// بررسی وجود کش‌بک فعال با عنوان مشابه
        /// </summary>
        Task<bool> ExistsByTitleAsync(int userId, string title, int? excludeId = null);
    }

    /// <summary>
    /// رابط Repository برای تراکنش‌های کش‌بک
    /// </summary>
    public interface ICashbackTransactionRepository : IBaseRepository<CashbackTransaction>
    {
        /// <summary>
        /// دریافت تراکنش‌های یک کش‌بک
        /// </summary>
        Task<IEnumerable<CashbackTransaction>> GetByCashbackIdAsync(int cashbackId, int pageNumber = 1, int pageSize = 10);

        /// <summary>
        /// دریافت تراکنش‌های یک مخاطب
        /// </summary>
        Task<IEnumerable<CashbackTransaction>> GetByContactIdAsync(int contactId, int pageNumber = 1, int pageSize = 10);

        /// <summary>
        /// دریافت تراکنش‌های در انتظار واریز
        /// </summary>
        Task<IEnumerable<CashbackTransaction>> GetPendingTransactionsAsync();

        /// <summary>
        /// دریافت تراکنش‌های زمان‌بندی شده آماده برای واریز
        /// </summary>
        Task<IEnumerable<CashbackTransaction>> GetScheduledTransactionsReadyForDepositAsync();

        /// <summary>
        /// بررسی وجود تراکنش کش‌بک برای مخاطب در کش‌بک مشخص
        /// </summary>
        Task<bool> ExistsForContactInCashbackAsync(int cashbackId, int contactId);
    }
}




