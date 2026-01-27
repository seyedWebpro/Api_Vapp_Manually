using Api_Vapp.Data;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;
using Api_Vapp._Utilities;
using Microsoft.EntityFrameworkCore;

namespace Api_Vapp.Repositories
{
    /// <summary>
    /// پیاده‌سازی Repository برای تراکنش‌های کیف پول
    /// </summary>
    public class WalletRepository : BaseRepository<WalletTransaction>, IWalletRepository
    {
        public WalletRepository(Api_Context context) : base(context)
        {
        }

        public async Task<IEnumerable<WalletTransaction>> GetByUserIdAsync(int userId, int pageNumber = 1, int pageSize = 10)
        {
            return await _dbSet
                .Where(wt => wt.UserId == userId)
                .OrderByDescending(wt => wt.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<int> GetCountByUserIdAsync(int userId)
        {
            return await _dbSet
                .Where(wt => wt.UserId == userId)
                .CountAsync();
        }

        public async Task<IEnumerable<WalletTransaction>> GetByTypeAsync(int userId, string transactionType, int pageNumber = 1, int pageSize = 10)
        {
            return await _dbSet
                .Where(wt => wt.UserId == userId && wt.TransactionType == transactionType)
                .OrderByDescending(wt => wt.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<IEnumerable<WalletTransaction>> GetByDateRangeAsync(int userId, DateTime fromDate, DateTime toDate)
        {
            return await _dbSet
                .Where(wt => wt.UserId == userId && 
                       wt.CreatedAt >= fromDate && 
                       wt.CreatedAt <= toDate)
                .OrderByDescending(wt => wt.CreatedAt)
                .ToListAsync();
        }

        public async Task<IEnumerable<WalletTransaction>> GetRecentTransactionsAsync(int userId, int count = 5)
        {
            return await _dbSet
                .Where(wt => wt.UserId == userId)
                .OrderByDescending(wt => wt.CreatedAt)
                .Take(count)
                .ToListAsync();
        }

        public async Task<decimal> GetTotalDepositsAsync(int userId)
        {
            return await _dbSet
                .Where(wt => wt.UserId == userId && 
                       wt.Amount > 0 && 
                       wt.Status == TransactionStatuses.Completed)
                .SumAsync(wt => wt.Amount);
        }

        public async Task<decimal> GetTotalWithdrawalsAsync(int userId)
        {
            return await _dbSet
                .Where(wt => wt.UserId == userId && 
                       wt.Amount < 0 && 
                       wt.Status == TransactionStatuses.Completed)
                .SumAsync(wt => Math.Abs(wt.Amount));
        }
    }
}




