using Api_Vapp._Utilities;
using Api_Vapp.Data;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;
using Microsoft.EntityFrameworkCore;

namespace Api_Vapp.Repositories
{
    public class LuckyWheelRepository : BaseRepository<LuckyWheel>, ILuckyWheelRepository
    {
        public LuckyWheelRepository(Api_Context context) : base(context)
        {
        }

        public override async Task<LuckyWheel?> GetByIdAsync(int id)
        {
            return await _dbSet
                .AsNoTracking()
                .FirstOrDefaultAsync(w => w.Id == id && !w.IsDeleted);
        }

        public async Task<LuckyWheel?> GetByIdWithDetailsReadOnlyAsync(int id)
        {
            return await _dbSet
                .AsNoTracking()
                .AsSplitQuery()
                .Include(w => w.Items.OrderBy(item => item.DisplayOrder))
                .Include(w => w.Notebooks)
                .FirstOrDefaultAsync(w => w.Id == id && !w.IsDeleted);
        }

        public async Task<LuckyWheel?> GetByIdWithDetailsTrackedAsync(int id)
        {
            return await _dbSet
                .AsSplitQuery()
                .Include(w => w.Items.OrderBy(item => item.DisplayOrder))
                .Include(w => w.Notebooks)
                .FirstOrDefaultAsync(w => w.Id == id && !w.IsDeleted);
        }

        public async Task<LuckyWheel?> GetOwnedWheelAsync(int id, int userId, bool tracked = false)
        {
            var query = tracked ? _dbSet.AsQueryable() : _dbSet.AsNoTracking();

            return await query.FirstOrDefaultAsync(w =>
                w.Id == id &&
                w.UserId == userId &&
                !w.IsDeleted);
        }

        public async Task<bool> SlugExistsAsync(string slug, int? excludeWheelId = null)
        {
            var query = _dbSet
                .AsNoTracking()
                .Where(w => w.Slug == slug && !w.IsDeleted);

            if (excludeWheelId.HasValue)
            {
                query = query.Where(w => w.Id != excludeWheelId.Value);
            }

            return await query.AnyAsync();
        }

        public async Task<IReadOnlyList<string>> GetExistingSlugsWithPrefixAsync(string slugPrefix, int? excludeWheelId = null)
        {
            var query = _dbSet
                .AsNoTracking()
                .Where(w =>
                    w.Slug != null &&
                    w.Slug.StartsWith(slugPrefix) &&
                    !w.IsDeleted);

            if (excludeWheelId.HasValue)
            {
                query = query.Where(w => w.Id != excludeWheelId.Value);
            }

            return await query
                .Select(w => w.Slug!)
                .ToListAsync();
        }

        public async Task<(IReadOnlyList<LuckyWheel> Items, int TotalCount)> GetByUserIdPagedAsync(
            int userId,
            int pageNumber,
            int pageSize,
            bool? isActive = null)
        {
            var query = _dbSet
                .AsNoTracking()
                .Where(w => w.UserId == userId && !w.IsDeleted);

            if (isActive.HasValue)
            {
                query = query.Where(w => w.IsActive == isActive.Value);
            }

            var totalCount = await query.CountAsync();

            var items = await query
                .OrderByDescending(w => w.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(w => new LuckyWheel
                {
                    Id = w.Id,
                    Title = w.Title,
                    Slug = w.Slug,
                    Status = w.Status,
                    IsActive = w.IsActive,
                    CreatedAt = w.CreatedAt,
                    PublishedAt = w.PublishedAt
                })
                .ToListAsync();

            return (items, totalCount);
        }

        /// <summary>
        /// گردونه Published و حذف‌نشده — بدون فیلتر IsActive (لایه سرویس تشخیص RESOURCE_INACTIVE می‌دهد).
        /// </summary>
        public async Task<LuckyWheel?> GetBySlugReadOnlyAsync(string slug)
        {
            return await _dbSet
                .AsNoTracking()
                .AsSplitQuery()
                .Include(w => w.Items.OrderBy(item => item.DisplayOrder))
                .Include(w => w.Notebooks)
                .FirstOrDefaultAsync(w =>
                    w.Slug == slug &&
                    !w.IsDeleted &&
                    w.Status == LuckyWheelStatus.Published);
        }

        public async Task<int> GetParticipantCountAsync(int luckyWheelId)
        {
            return await _context.LuckyWheelParticipants
                .AsNoTracking()
                .CountAsync(p => p.LuckyWheelId == luckyWheelId);
        }

        public async Task<bool> HasParticipantWithMobileAsync(int luckyWheelId, string mobile)
        {
            return await _context.LuckyWheelParticipants
                .AsNoTracking()
                .AnyAsync(p =>
                    p.LuckyWheelId == luckyWheelId &&
                    p.ParticipantMobile == mobile);
        }

        public async Task AddParticipantAsync(LuckyWheelParticipant participant)
        {
            await _context.LuckyWheelParticipants.AddAsync(participant);
            await _context.SaveChangesAsync();
        }

        public async Task<bool> PrizeCodeExistsAsync(int luckyWheelId, string prizeCode)
        {
            return await _context.LuckyWheelParticipants
                .AsNoTracking()
                .AnyAsync(p =>
                    p.LuckyWheelId == luckyWheelId &&
                    p.PrizeCode == prizeCode);
        }

        public async Task<(IReadOnlyList<LuckyWheelParticipant> Items, int TotalCount)> GetParticipantsPagedAsync(
            int luckyWheelId,
            int pageNumber,
            int pageSize,
            string? searchTerm = null)
        {
            var query = _context.LuckyWheelParticipants
                .AsNoTracking()
                .Include(p => p.WonItem)
                .Where(p => p.LuckyWheelId == luckyWheelId);

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var term = searchTerm.Trim();
                query = query.Where(p =>
                    p.ParticipantFullName.Contains(term) ||
                    p.ParticipantMobile.Contains(term) ||
                    p.PrizeCode.Contains(term));
            }

            var totalCount = await query.CountAsync();

            var items = await query
                .OrderByDescending(p => p.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, totalCount);
        }

        public async Task<LuckyWheelParticipant?> FindParticipantByMobileAsync(int luckyWheelId, string mobile)
        {
            return await _context.LuckyWheelParticipants
                .AsNoTracking()
                .Include(p => p.WonItem)
                .Include(p => p.LuckyWheel)
                .FirstOrDefaultAsync(p =>
                    p.LuckyWheelId == luckyWheelId &&
                    p.ParticipantMobile == mobile);
        }

        public async Task<LuckyWheelParticipant?> FindParticipantByPrizeCodeAsync(int luckyWheelId, string prizeCode)
        {
            return await _context.LuckyWheelParticipants
                .AsNoTracking()
                .Include(p => p.WonItem)
                .Include(p => p.LuckyWheel)
                .FirstOrDefaultAsync(p =>
                    p.LuckyWheelId == luckyWheelId &&
                    p.PrizeCode == prizeCode);
        }
    }
}
