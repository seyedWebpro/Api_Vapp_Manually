using Api_Vapp._Utilities;
using Api_Vapp.Data;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;
using Microsoft.EntityFrameworkCore;

namespace Api_Vapp.Repositories
{
    public class BusinessCardRepository : BaseRepository<BusinessCard>, IBusinessCardRepository
    {
        public BusinessCardRepository(Api_Context context) : base(context)
        {
        }

        public override async Task<BusinessCard?> GetByIdAsync(int id)
        {
            return await _dbSet
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted);
        }

        public async Task<BusinessCard?> GetByIdWithDetailsReadOnlyAsync(int id)
        {
            return await _dbSet
                .AsNoTracking()
                .AsSplitQuery()
                .Include(c => c.SliderImages.OrderBy(i => i.DisplayOrder))
                .Include(c => c.ServiceItems.OrderBy(i => i.DisplayOrder))
                .Include(c => c.SocialLinks.OrderBy(i => i.DisplayOrder))
                .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted);
        }

        public async Task<BusinessCard?> GetByIdWithDetailsTrackedAsync(int id)
        {
            return await _dbSet
                .AsSplitQuery()
                .Include(c => c.SliderImages.OrderBy(i => i.DisplayOrder))
                .Include(c => c.ServiceItems.OrderBy(i => i.DisplayOrder))
                .Include(c => c.SocialLinks.OrderBy(i => i.DisplayOrder))
                .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted);
        }

        public async Task<BusinessCard?> GetByIdWithDetailsTrackedForUserAsync(int id, int userId)
        {
            return await _dbSet
                .AsSplitQuery()
                .Include(c => c.SliderImages.OrderBy(i => i.DisplayOrder))
                .Include(c => c.ServiceItems.OrderBy(i => i.DisplayOrder))
                .Include(c => c.SocialLinks.OrderBy(i => i.DisplayOrder))
                .FirstOrDefaultAsync(c =>
                    c.Id == id &&
                    c.UserId == userId &&
                    !c.IsDeleted);
        }

        public async Task<BusinessCard?> GetOwnedCardAsync(int id, int userId, bool tracked = false)
        {
            var query = tracked ? _dbSet.AsQueryable() : _dbSet.AsNoTracking();

            return await query.FirstOrDefaultAsync(c =>
                c.Id == id &&
                c.UserId == userId &&
                !c.IsDeleted);
        }

        /// <summary>
        /// کارت Published و حذف‌نشده — بدون فیلتر IsActive (لایه سرویس تشخیص RESOURCE_INACTIVE می‌دهد).
        /// </summary>
        public async Task<BusinessCard?> GetBySlugReadOnlyAsync(string slug)
        {
            return await _dbSet
                .AsNoTracking()
                .AsSplitQuery()
                .Include(c => c.SliderImages.OrderBy(i => i.DisplayOrder))
                .Include(c => c.ServiceItems.OrderBy(i => i.DisplayOrder))
                .Include(c => c.SocialLinks.OrderBy(i => i.DisplayOrder))
                .FirstOrDefaultAsync(c =>
                    c.Slug == slug &&
                    !c.IsDeleted &&
                    c.Status == BusinessCardStatus.Published);
        }

        public async Task<bool> SlugExistsAsync(string slug, int? excludeCardId = null)
        {
            var query = _dbSet
                .AsNoTracking()
                .Where(c => c.Slug == slug && !c.IsDeleted);

            if (excludeCardId.HasValue)
            {
                query = query.Where(c => c.Id != excludeCardId.Value);
            }

            return await query.AnyAsync();
        }

        public async Task<IReadOnlyList<string>> GetExistingSlugsWithPrefixAsync(string slugPrefix, int? excludeCardId = null)
        {
            var query = _dbSet
                .AsNoTracking()
                .Where(c =>
                    c.Slug != null &&
                    c.Slug.StartsWith(slugPrefix) &&
                    !c.IsDeleted);

            if (excludeCardId.HasValue)
            {
                query = query.Where(c => c.Id != excludeCardId.Value);
            }

            return await query
                .Select(c => c.Slug!)
                .ToListAsync();
        }

        public async Task<(IReadOnlyList<BusinessCard> Items, int TotalCount)> GetByUserIdPagedAsync(
            int userId,
            int pageNumber,
            int pageSize)
        {
            var query = _dbSet
                .AsNoTracking()
                .Where(c => c.UserId == userId && !c.IsDeleted);

            var totalCount = await query.CountAsync();

            var items = await query
                .OrderByDescending(c => c.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(c => new BusinessCard
                {
                    Id = c.Id,
                    Title = c.Title,
                    LogoUrl = c.LogoUrl,
                    Slug = c.Slug,
                    Status = c.Status,
                    IsActive = c.IsActive,
                    CreatedAt = c.CreatedAt,
                    PublishedAt = c.PublishedAt
                })
                .ToListAsync();

            return (items, totalCount);
        }
    }
}
