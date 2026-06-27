using Api_Vapp.Data;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;
using Api_Vapp._Utilities;
using Microsoft.EntityFrameworkCore;

namespace Api_Vapp.Repositories
{
    public class ReferralProgramRepository : BaseRepository<ReferralProgram>, IReferralProgramRepository
    {
        public ReferralProgramRepository(Api_Context context) : base(context)
        {
        }

        public async Task<IEnumerable<ReferralProgram>> GetByUserIdAsync(int userId, int pageNumber = 1, int pageSize = 10, bool? isActive = null)
        {
            var query = _dbSet.Where(r => r.UserId == userId && !r.IsDeleted);

            if (isActive.HasValue)
            {
                query = query.Where(r => r.IsActive == isActive.Value);
            }

            return await query
                .AsNoTracking()
                .OrderByDescending(r => r.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<int> GetCountByUserIdAsync(int userId, bool? isActive = null)
        {
            var query = _dbSet.Where(r => r.UserId == userId && !r.IsDeleted);

            if (isActive.HasValue)
            {
                query = query.Where(r => r.IsActive == isActive.Value);
            }

            return await query.CountAsync();
        }

        public async Task<int> GetActiveCountByUserIdAsync(int userId)
        {
            var now = DateTime.UtcNow;
            return await _dbSet.CountAsync(r =>
                r.UserId == userId &&
                !r.IsDeleted &&
                r.IsActive &&
                r.StartDate <= now &&
                (r.EndDate == null || r.EndDate >= now));
        }

        public async Task<ReferralProgram?> GetByIdAndUserIdAsync(int id, int userId)
        {
            return await _dbSet.FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId && !r.IsDeleted);
        }

        public async Task<ReferralProgram?> GetByPublicCodeAsync(int userId, string publicCode)
        {
            return await _dbSet
                .AsNoTracking()
                .FirstOrDefaultAsync(r =>
                r.UserId == userId &&
                r.PublicCode == publicCode &&
                !r.IsDeleted);
        }

        public async Task<bool> ExistsByPublicCodeAsync(int userId, string publicCode, int? excludeId = null)
        {
            var query = _dbSet.Where(r => r.UserId == userId && r.PublicCode == publicCode && !r.IsDeleted);

            if (excludeId.HasValue)
            {
                query = query.Where(r => r.Id != excludeId.Value);
            }

            return await query.AnyAsync();
        }

        public async Task<bool> ExistsByTitleAsync(int userId, string title, int? excludeId = null)
        {
            var query = _dbSet.Where(r => r.UserId == userId && r.Title == title && !r.IsDeleted);

            if (excludeId.HasValue)
            {
                query = query.Where(r => r.Id != excludeId.Value);
            }

            return await query.AnyAsync();
        }

        public override async Task<ReferralProgram?> GetByIdAsync(int id)
        {
            return await _dbSet.FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted);
        }
    }
}
