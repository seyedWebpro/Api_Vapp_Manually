using Api_Vapp.Models;
using Api_Vapp._Utilities;

namespace Api_Vapp.Interfaces
{
    /// <summary>
    /// رابط Repository برای مدیریت شماره حساب‌های ارسال سریع
    /// </summary>
    public interface IBankAccountRepository : IBaseRepository<BankAccount>
    {
        Task<(List<BankAccount> Items, int TotalCount)> GetPagedByUserIdAsync(
            int userId,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken = default);

        Task<BankAccount?> GetOwnedByIdAsync(int id, int userId, bool asNoTracking = true);

        Task<int> CountActiveByUserIdAsync(int userId, CancellationToken cancellationToken = default);
    }
}
