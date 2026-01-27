using Api_Vapp.Data;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;
using Api_Vapp._Utilities;
using Microsoft.EntityFrameworkCore;

namespace Api_Vapp.Repositories
{
    /// <summary>
    /// پیاده‌سازی Repository برای SpecialOccasion
    /// </summary>
    public class SpecialOccasionRepository : BaseRepository<SpecialOccasion>, ISpecialOccasionRepository
    {
        public SpecialOccasionRepository(Api_Context context) : base(context)
        {
        }

        public async Task<IEnumerable<SpecialOccasion>> GetByUserIdAsync(int? userId)
        {
            return await _dbSet
                .Where(so => (userId == null ? so.UserId == null : so.UserId == userId) && !so.IsDeleted)
                .OrderBy(so => so.OccasionDate)
                .ToListAsync();
        }

        public async Task<IEnumerable<SpecialOccasion>> GetActiveByUserIdAsync(int? userId)
        {
            return await _dbSet
                .Where(so => (userId == null ? so.UserId == null : so.UserId == userId) && so.IsActive && !so.IsDeleted)
                .OrderBy(so => so.OccasionDate)
                .ToListAsync();
        }

        public async Task<IEnumerable<SpecialOccasion>> GetSystemOccasionsAsync()
        {
            return await _dbSet
                .Where(so => so.IsSystem && so.IsActive && !so.IsDeleted)
                .OrderBy(so => so.OccasionDate)
                .ToListAsync();
        }

        public override async Task<SpecialOccasion?> GetByIdAsync(int id)
        {
            return await _dbSet
                .FirstOrDefaultAsync(so => so.Id == id && !so.IsDeleted);
        }
    }
}

