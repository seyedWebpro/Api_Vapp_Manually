using Api_Vapp.Data;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;
using Api_Vapp._Utilities;
using Microsoft.EntityFrameworkCore;

namespace Api_Vapp.Repositories
{
    /// <summary>
    /// پیاده‌سازی Repository برای BankAccount
    /// </summary>
    public class BankAccountRepository : BaseRepository<BankAccount>, IBankAccountRepository
    {
        public BankAccountRepository(Api_Context context) : base(context)
        {
        }

        public async Task<(List<BankAccount> Items, int TotalCount)> GetPagedByUserIdAsync(
            int userId,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            var query = _dbSet
                .AsNoTracking()
                .Where(b => b.UserId == userId && !b.IsDeleted);

            var totalCount = await query.CountAsync(cancellationToken);

            var items = await query
                .OrderByDescending(b => b.IsDefault)
                .ThenByDescending(b => b.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            return (items, totalCount);
        }

        public async Task<BankAccount?> GetOwnedByIdAsync(int id, int userId, bool asNoTracking = true)
        {
            IQueryable<BankAccount> query = _dbSet
                .Where(b => b.Id == id && b.UserId == userId && !b.IsDeleted);

            if (asNoTracking)
                query = query.AsNoTracking();

            return await query.FirstOrDefaultAsync();
        }

        public Task<int> CountActiveByUserIdAsync(int userId, CancellationToken cancellationToken = default)
        {
            return _dbSet
                .AsNoTracking()
                .CountAsync(b => b.UserId == userId && b.IsActive && !b.IsDeleted, cancellationToken);
        }

        public override async Task<BankAccount?> GetByIdAsync(int id)
        {
            return await _dbSet
                .FirstOrDefaultAsync(b => b.Id == id && !b.IsDeleted);
        }
    }
}
