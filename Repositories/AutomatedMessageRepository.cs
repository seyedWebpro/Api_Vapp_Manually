using Api_Vapp.Data;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;
using Api_Vapp._Utilities;
using Microsoft.EntityFrameworkCore;

namespace Api_Vapp.Repositories
{
    /// <summary>
    /// پیاده‌سازی Repository برای AutomatedMessage
    /// </summary>
    public class AutomatedMessageRepository : BaseRepository<AutomatedMessage>, IAutomatedMessageRepository
    {
        public AutomatedMessageRepository(Api_Context context) : base(context)
        {
        }

        public async Task<IEnumerable<AutomatedMessage>> GetByUserIdAsync(int userId)
        {
            return await _dbSet
                .Where(am => am.UserId == userId && !am.IsDeleted)
                .OrderByDescending(am => am.CreatedAt)
                .ToListAsync();
        }

        public async Task<IEnumerable<AutomatedMessage>> GetActiveByUserIdAsync(int userId)
        {
            return await _dbSet
                .Where(am => am.UserId == userId && am.IsActive && !am.IsDeleted)
                .OrderByDescending(am => am.CreatedAt)
                .ToListAsync();
        }

        public async Task<IEnumerable<AutomatedMessage>> GetByUserIdAndTypeAsync(int userId, string automationType)
        {
            return await _dbSet
                .Where(am => am.UserId == userId && am.AutomationType == automationType && !am.IsDeleted)
                .OrderByDescending(am => am.CreatedAt)
                .ToListAsync();
        }

        public async Task<AutomatedMessage?> GetByIdWithExecutionsAsync(int id)
        {
            return await _dbSet
                .Include(am => am.Executions.OrderByDescending(e => e.ExecutedAt))
                .FirstOrDefaultAsync(am => am.Id == id && !am.IsDeleted);
        }

        public override async Task<AutomatedMessage?> GetByIdAsync(int id)
        {
            return await _dbSet
                .FirstOrDefaultAsync(am => am.Id == id && !am.IsDeleted);
        }

        public async Task<AutomatedMessage?> GetByIdWithMessageAsync(int id)
        {
            return await _dbSet
                .Include(am => am.Message)
                .FirstOrDefaultAsync(am => am.Id == id && !am.IsDeleted);
        }

        public async Task<AutomatedMessage?> GetByIdWithMessageAndOccasionAsync(int id)
        {
            return await _dbSet
                .Include(am => am.Message)
                .Include(am => am.SpecialOccasion)
                .FirstOrDefaultAsync(am => am.Id == id && !am.IsDeleted);
        }
    }
}

