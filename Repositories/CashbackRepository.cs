using Api_Vapp.Data;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;
using Api_Vapp._Utilities;
using Microsoft.EntityFrameworkCore;

namespace Api_Vapp.Repositories
{
    /// <summary>
    /// پیاده‌سازی Repository برای کش‌بک
    /// </summary>
    public class CashbackRepository : BaseRepository<Cashback>, ICashbackRepository
    {
        public CashbackRepository(Api_Context context) : base(context)
        {
        }

        public async Task<IEnumerable<Cashback>> GetByUserIdAsync(int userId, int pageNumber = 1, int pageSize = 10, bool? isActive = null)
        {
            var query = _dbSet
                .Where(c => c.UserId == userId && !c.IsDeleted);

            if (isActive.HasValue)
            {
                query = query.Where(c => c.IsActive == isActive.Value);
            }

            return await query
                .OrderByDescending(c => c.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<int> GetCountByUserIdAsync(int userId, bool? isActive = null)
        {
            var query = _dbSet
                .Where(c => c.UserId == userId && !c.IsDeleted);

            if (isActive.HasValue)
            {
                query = query.Where(c => c.IsActive == isActive.Value);
            }

            return await query.CountAsync();
        }

        public async Task<IEnumerable<Cashback>> GetActiveByUserIdAsync(int userId)
        {
            var now = DateTime.UtcNow;
            return await _dbSet
                .Where(c => c.UserId == userId && 
                       !c.IsDeleted && 
                       c.IsActive &&
                       c.StartDate <= now &&
                       (c.EndDate == null || c.EndDate >= now))
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();
        }

        public async Task<Cashback?> GetByIdAndUserIdAsync(int id, int userId)
        {
            return await _dbSet
                .FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId && !c.IsDeleted);
        }

        public async Task<bool> ExistsByTitleAsync(int userId, string title, int? excludeId = null)
        {
            var query = _dbSet
                .Where(c => c.UserId == userId && 
                       c.Title == title && 
                       !c.IsDeleted);

            if (excludeId.HasValue)
            {
                query = query.Where(c => c.Id != excludeId.Value);
            }

            return await query.AnyAsync();
        }

        public override async Task<Cashback?> GetByIdAsync(int id)
        {
            return await _dbSet
                .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted);
        }
    }

    /// <summary>
    /// پیاده‌سازی Repository برای تراکنش‌های کش‌بک
    /// </summary>
    public class CashbackTransactionRepository : BaseRepository<CashbackTransaction>, ICashbackTransactionRepository
    {
        public CashbackTransactionRepository(Api_Context context) : base(context)
        {
        }

        public async Task<IEnumerable<CashbackTransaction>> GetByCashbackIdAsync(int cashbackId, int pageNumber = 1, int pageSize = 10)
        {
            return await _dbSet
                .Include(ct => ct.Contact)
                .Where(ct => ct.CashbackId == cashbackId)
                .OrderByDescending(ct => ct.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<IEnumerable<CashbackTransaction>> GetByContactIdAsync(int contactId, int pageNumber = 1, int pageSize = 10)
        {
            return await _dbSet
                .Include(ct => ct.Cashback)
                .Where(ct => ct.ContactId == contactId)
                .OrderByDescending(ct => ct.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<IEnumerable<CashbackTransaction>> GetPendingTransactionsAsync()
        {
            return await _dbSet
                .Include(ct => ct.Cashback)
                .Include(ct => ct.Contact)
                .Where(ct => ct.Status == CashbackTransactionStatuses.Pending)
                .OrderBy(ct => ct.CreatedAt)
                .ToListAsync();
        }

        public async Task<IEnumerable<CashbackTransaction>> GetScheduledTransactionsReadyForDepositAsync()
        {
            var now = DateTime.UtcNow;
            return await _dbSet
                .Include(ct => ct.Cashback)
                .Include(ct => ct.Contact)
                .Where(ct => ct.Status == CashbackTransactionStatuses.Scheduled && 
                       ct.ScheduledAt.HasValue && 
                       ct.ScheduledAt.Value <= now)
                .OrderBy(ct => ct.ScheduledAt)
                .ToListAsync();
        }

        public async Task<bool> ExistsForContactInCashbackAsync(int cashbackId, int contactId)
        {
            return await _dbSet
                .AnyAsync(ct => ct.CashbackId == cashbackId && ct.ContactId == contactId);
        }
    }
}




