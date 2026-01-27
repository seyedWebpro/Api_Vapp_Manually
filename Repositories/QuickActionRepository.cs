using Api_Vapp.Data;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;
using Api_Vapp._Utilities;
using Microsoft.EntityFrameworkCore;

namespace Api_Vapp.Repositories
{
    /// <summary>
    /// پیاده‌سازی Repository برای QuickAction
    /// </summary>
    public class QuickActionRepository : BaseRepository<QuickAction>, IQuickActionRepository
    {
        public QuickActionRepository(Api_Context context) : base(context)
        {
        }

        public async Task<IEnumerable<QuickAction>> GetByUserIdAsync(int userId)
        {
            return await _dbSet
                .Where(qa => qa.UserId == userId && !qa.IsDeleted)
                .OrderBy(qa => qa.DisplayOrder)
                .ThenByDescending(qa => qa.CreatedAt)
                .ToListAsync();
        }

        public async Task<IEnumerable<QuickAction>> GetActiveByUserIdAsync(int userId)
        {
            return await _dbSet
                .Where(qa => qa.UserId == userId && qa.IsActive && !qa.IsDeleted)
                .OrderBy(qa => qa.DisplayOrder)
                .ThenByDescending(qa => qa.CreatedAt)
                .ToListAsync();
        }

        public override async Task<QuickAction?> GetByIdAsync(int id)
        {
            return await _dbSet
                .FirstOrDefaultAsync(qa => qa.Id == id && !qa.IsDeleted);
        }
    }
}

