using Api_Vapp.Data;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;
using Api_Vapp._Utilities;
using Microsoft.EntityFrameworkCore;

namespace Api_Vapp.Repositories
{
    /// <summary>
    /// پیاده‌سازی Repository برای پرداخت
    /// </summary>
    public class PaymentRepository : BaseRepository<Payment>, IPaymentRepository
    {
        public PaymentRepository(Api_Context context) : base(context)
        {
        }

        public async Task<Payment?> GetByOrderIdAsync(string orderId)
        {
            return await _dbSet
                .FirstOrDefaultAsync(p => p.OrderId == orderId);
        }

        public async Task<Payment?> GetByRefIdAsync(string refId)
        {
            if (string.IsNullOrWhiteSpace(refId))
                return null;

            return await _dbSet
                .FirstOrDefaultAsync(p => p.RefId == refId);
        }

        public async Task<IEnumerable<Payment>> GetByUserIdAsync(int userId, int pageNumber = 1, int pageSize = 10)
        {
            return await _dbSet
                .Where(p => p.UserId == userId)
                .OrderByDescending(p => p.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<int> GetCountByUserIdAsync(int userId)
        {
            return await _dbSet
                .Where(p => p.UserId == userId)
                .CountAsync();
        }

        public async Task<IEnumerable<Payment>> GetByStatusAsync(int userId, string status)
        {
            return await _dbSet
                .Where(p => p.UserId == userId && p.Status == status)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();
        }

        public async Task<IEnumerable<Payment>> GetExpiredPendingPaymentsAsync(TimeSpan timeout)
        {
            var cutoffTime = DateTime.UtcNow.Subtract(timeout);
            return await _dbSet
                .Where(p => p.Status == PaymentStatuses.Pending && 
                       p.CreatedAt < cutoffTime)
                .ToListAsync();
        }

        public async Task<bool> HasPendingPaymentAsync(int userId)
        {
            // هر Pending/Processing باز — بدون پنجره ۱۵ دقیقه‌ای
            // (بعد از صدور Authority، تا NOK/Verify قطعی یا انقضا، قفل بماند)
            return await _dbSet
                .AnyAsync(p => p.UserId == userId &&
                         (p.Status == PaymentStatuses.Pending || p.Status == PaymentStatuses.Processing));
        }

        public async Task ExpireStalePendingPaymentsAsync(TimeSpan timeout)
        {
            var cutoff = DateTime.UtcNow.Subtract(timeout);
            var stale = await _dbSet
                .Where(p =>
                    (p.Status == PaymentStatuses.Pending || p.Status == PaymentStatuses.Processing) &&
                    p.CreatedAt < cutoff)
                .ToListAsync();

            if (stale.Count == 0)
                return;

            foreach (var p in stale)
            {
                p.Status = PaymentStatuses.Failed;
                p.ErrorCode = "EXPIRED";
                p.ErrorMessage = "پرداخت منقضی شد";
            }

            await _context.SaveChangesAsync();
        }

        public async Task<decimal> GetTotalSuccessfulPaymentsAsync(int userId)
        {
            return await _dbSet
                .Where(p => p.UserId == userId && 
                       (p.Status == PaymentStatuses.Verified || p.Status == PaymentStatuses.Paid))
                .SumAsync(p => p.Amount);
        }
    }
}




