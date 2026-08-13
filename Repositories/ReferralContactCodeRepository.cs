using Api_Vapp.Data;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;
using Api_Vapp._Utilities;
using Microsoft.EntityFrameworkCore;

namespace Api_Vapp.Repositories
{
    public class ReferralContactCodeRepository : BaseRepository<ReferralContactCode>, IReferralContactCodeRepository
    {
        public ReferralContactCodeRepository(Api_Context context) : base(context)
        {
        }

        public async Task<ReferralContactCode?> GetByCodeAsync(int userId, string code)
        {
            return await _dbSet
                .Include(c => c.Contact)
                .Include(c => c.ReferralProgram)
                .AsNoTracking()
                .FirstOrDefaultAsync(c =>
                    c.UserId == userId &&
                    c.Code == code &&
                    !c.IsDeleted);
        }

        public async Task<bool> ExistsByCodeAsync(int userId, string code)
        {
            // شامل کدهای حذف‌شده تا Unique Index نقض نشود
            return await _dbSet.AnyAsync(c =>
                c.UserId == userId &&
                c.Code == code);
        }

        public async Task<IEnumerable<ReferralContactCode>> GetByProgramIdAsync(
            int programId,
            int userId,
            int pageNumber = 1,
            int pageSize = 20)
        {
            if (pageNumber < 1) pageNumber = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 20;

            return await _dbSet
                .AsNoTracking()
                .Include(c => c.Contact)
                .Where(c => c.ReferralProgramId == programId && c.UserId == userId && !c.IsDeleted)
                .OrderByDescending(c => c.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<int> GetCountByProgramIdAsync(int programId, int userId)
        {
            return await _dbSet.CountAsync(c =>
                c.ReferralProgramId == programId &&
                c.UserId == userId &&
                !c.IsDeleted);
        }

        public async Task SoftDeleteByProgramIdAsync(int programId, int userId)
        {
            var codes = await _dbSet
                .Where(c => c.ReferralProgramId == programId && c.UserId == userId && !c.IsDeleted)
                .ToListAsync();

            if (codes.Count == 0)
            {
                return;
            }

            var now = DateTime.UtcNow;
            foreach (var code in codes)
            {
                code.IsDeleted = true;
                code.UpdatedAt = now;
            }

            await _context.SaveChangesAsync();
        }
    }
}
