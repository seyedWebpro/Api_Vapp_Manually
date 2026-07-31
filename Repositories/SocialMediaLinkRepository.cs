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
                .AsNoTracking()
                .Where(sml => sml.UserId == userId && !sml.IsDeleted)
                .OrderByDescending(sml => sml.IsDefault)
                .ThenByDescending(sml => sml.CreatedAt)
                .ToListAsync();
        }

        public async Task<IEnumerable<SocialMediaLink>> GetActiveByUserIdAsync(int userId)
        {
            return await _dbSet
                .AsNoTracking()
                .Where(sml => sml.UserId == userId && sml.IsActive && !sml.IsDeleted)
                .OrderByDescending(sml => sml.IsDefault)
                .ThenByDescending(sml => sml.CreatedAt)
                .ToListAsync();
        }

        public async Task<(List<SocialMediaLink> Items, int TotalCount)> GetPagedByUserIdAsync(
            int userId,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            var query = _dbSet
                .AsNoTracking()
                .Where(sml => sml.UserId == userId && !sml.IsDeleted);

            var totalCount = await query.CountAsync(cancellationToken);

            var items = await query
                .OrderByDescending(sml => sml.IsDefault)
                .ThenByDescending(sml => sml.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            return (items, totalCount);
        }

        public async Task<SocialMediaLink?> GetOwnedByIdAsync(int id, int userId, bool asNoTracking = true)
        {
            IQueryable<SocialMediaLink> query = _dbSet
                .Where(sml => sml.Id == id && sml.UserId == userId && !sml.IsDeleted);

            if (asNoTracking)
                query = query.AsNoTracking();

            return await query.FirstOrDefaultAsync();
        }

        public Task<int> CountActiveByUserIdAsync(int userId, CancellationToken cancellationToken = default)
        {
            return _dbSet
                .AsNoTracking()
                .CountAsync(sml => sml.UserId == userId && sml.IsActive && !sml.IsDeleted, cancellationToken);
        }

        public override async Task<SocialMediaLink?> GetByIdAsync(int id)
        {
            return await _dbSet
                .FirstOrDefaultAsync(sml => sml.Id == id && !sml.IsDeleted);
        }
    }
}
