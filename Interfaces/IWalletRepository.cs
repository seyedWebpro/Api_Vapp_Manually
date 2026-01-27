using Api_Vapp.Models;
using Api_Vapp._Utilities;

namespace Api_Vapp.Interfaces
{
    /// <summary>
    /// رابط Repository برای تراکنش‌های کیف پول
    /// </summary>
    public interface IWalletRepository : IBaseRepository<WalletTransaction>
    {
        /// <summary>
        /// دریافت تراکنش‌های کاربر با صفحه‌بندی
        /// </summary>
        Task<IEnumerable<WalletTransaction>> GetByUserIdAsync(int userId, int pageNumber = 1, int pageSize = 10);

        /// <summary>
        /// دریافت تعداد تراکنش‌های کاربر
        /// </summary>
        Task<int> GetCountByUserIdAsync(int userId);

        /// <summary>
        /// دریافت تراکنش‌های کاربر بر اساس نوع
        /// </summary>
        Task<IEnumerable<WalletTransaction>> GetByTypeAsync(int userId, string transactionType, int pageNumber = 1, int pageSize = 10);

        /// <summary>
        /// دریافت تراکنش‌های کاربر در بازه زمانی
        /// </summary>
        Task<IEnumerable<WalletTransaction>> GetByDateRangeAsync(int userId, DateTime fromDate, DateTime toDate);

        /// <summary>
        /// دریافت آخرین تراکنش‌های کاربر
        /// </summary>
        Task<IEnumerable<WalletTransaction>> GetRecentTransactionsAsync(int userId, int count = 5);

        /// <summary>
        /// دریافت مجموع واریزی‌های کاربر
        /// </summary>
        Task<decimal> GetTotalDepositsAsync(int userId);

        /// <summary>
        /// دریافت مجموع برداشت‌های کاربر
        /// </summary>
        Task<decimal> GetTotalWithdrawalsAsync(int userId);
    }
}




