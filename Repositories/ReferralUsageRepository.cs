using Api_Vapp.Data;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;
using Api_Vapp._Utilities;
using Microsoft.EntityFrameworkCore;

namespace Api_Vapp.Repositories
{
    public class ReferralUsageRepository : BaseRepository<ReferralUsage>, IReferralUsageRepository
    {
        public ReferralUsageRepository(Api_Context context) : base(context)
        {
        }

        private IQueryable<ReferralUsage> BaseQuery(int userId)
        {
            return _dbSet.Where(u => u.UserId == userId && u.Status == ReferralUsageStatuses.Completed);
        }

        private static IQueryable<ReferralUsage> ApplyDateFilter(
            IQueryable<ReferralUsage> query,
            DateTime? fromDate,
            DateTime? toDate)
        {
            if (fromDate.HasValue)
            {
                query = query.Where(u => u.CreatedAt >= fromDate.Value);
            }

            if (toDate.HasValue)
            {
                query = query.Where(u => u.CreatedAt <= toDate.Value);
            }

            return query;
        }

        public async Task<IEnumerable<ReferralUsage>> GetByProgramIdAsync(
            int programId,
            int userId,
            int pageNumber = 1,
            int pageSize = 10,
            DateTime? fromDate = null,
            DateTime? toDate = null)
        {
            var query = ApplyDateFilter(
                BaseQuery(userId).Where(u => u.ReferralProgramId == programId),
                fromDate,
                toDate);

            return await query
                .Include(u => u.CustomerContact)
                .Include(u => u.ReferrerContact)
                .AsNoTracking()
                .OrderByDescending(u => u.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<int> GetCountByProgramIdAsync(
            int programId,
            int userId,
            DateTime? fromDate = null,
            DateTime? toDate = null)
        {
            var query = ApplyDateFilter(
                BaseQuery(userId).Where(u => u.ReferralProgramId == programId),
                fromDate,
                toDate);

            return await query.CountAsync();
        }

        public async Task<int> GetSuccessfulReferralsCountAsync(int userId)
        {
            return await BaseQuery(userId).CountAsync();
        }

        public async Task<decimal> GetTotalRewardsPaidAsync(int userId)
        {
            return await BaseQuery(userId)
                .SumAsync(u => u.CustomerDiscountAmount + u.ReferrerRewardAmount);
        }

        public async Task<int> GetDistinctActiveContactsCountAsync(int userId)
        {
            var customerIds = BaseQuery(userId)
                .Where(u => u.CustomerContactId.HasValue)
                .Select(u => u.CustomerContactId!.Value);

            var referrerIds = BaseQuery(userId)
                .Where(u => u.ReferrerContactId.HasValue)
                .Select(u => u.ReferrerContactId!.Value);

            return await customerIds
                .Concat(referrerIds)
                .Distinct()
                .CountAsync();
        }

        public async Task<(decimal CustomerTotal, decimal ReferrerTotal)> GetTotalsByProgramIdAsync(
            int programId,
            int userId,
            DateTime? fromDate = null,
            DateTime? toDate = null)
        {
            var query = ApplyDateFilter(
                BaseQuery(userId).Where(u => u.ReferralProgramId == programId),
                fromDate,
                toDate);

            var totals = await query
                .GroupBy(_ => 1)
                .Select(g => new
                {
                    CustomerTotal = g.Sum(u => u.CustomerDiscountAmount),
                    ReferrerTotal = g.Sum(u => u.ReferrerRewardAmount)
                })
                .FirstOrDefaultAsync();

            return (totals?.CustomerTotal ?? 0, totals?.ReferrerTotal ?? 0);
        }
    }
}
