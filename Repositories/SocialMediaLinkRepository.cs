using Api_Vapp.Data;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;
using Api_Vapp._Utilities;
using Microsoft.EntityFrameworkCore;

namespace Api_Vapp.Repositories
{
    /// <summary>
    /// پیاده‌سازی Repository برای SocialMediaLink
    /// </summary>
    public class SocialMediaLinkRepository : BaseRepository<SocialMediaLink>, ISocialMediaLinkRepository
    {
        public SocialMediaLinkRepository(Api_Context context) : base(context)
        {
        }

        public async Task<IEnumerable<SocialMediaLink>> GetByUserIdAsync(int userId)
        {
            return await _dbSet
                .Where(sml => sml.UserId == userId && !sml.IsDeleted)
                .OrderByDescending(sml => sml.IsDefault)
                .ThenByDescending(sml => sml.CreatedAt)
                .ToListAsync();
        }

        public async Task<IEnumerable<SocialMediaLink>> GetActiveByUserIdAsync(int userId)
        {
            return await _dbSet
                .Where(sml => sml.UserId == userId && sml.IsActive && !sml.IsDeleted)
                .OrderByDescending(sml => sml.IsDefault)
                .ThenByDescending(sml => sml.CreatedAt)
                .ToListAsync();
        }

        public override async Task<SocialMediaLink?> GetByIdAsync(int id)
        {
            return await _dbSet
                .FirstOrDefaultAsync(sml => sml.Id == id && !sml.IsDeleted);
        }
    }
}





