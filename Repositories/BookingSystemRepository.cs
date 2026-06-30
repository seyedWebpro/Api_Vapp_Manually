using Api_Vapp.Data;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;
using Api_Vapp._Utilities;
using Microsoft.EntityFrameworkCore;

namespace Api_Vapp.Repositories
{
    public class BookingSystemRepository : BaseRepository<BookingSystem>, IBookingSystemRepository
    {
        public BookingSystemRepository(Api_Context context) : base(context)
        {
        }

        public async Task<IEnumerable<BookingSystem>> GetByUserIdAsync(int userId, int pageNumber, int pageSize, bool? isActive)
        {
            var query = _dbSet.Where(b => b.UserId == userId && !b.IsDeleted);

            if (isActive.HasValue)
            {
                query = query.Where(b => b.IsActive == isActive.Value);
            }

            return await query
                .AsNoTracking()
                .OrderByDescending(b => b.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<int> GetCountByUserIdAsync(int userId, bool? isActive)
        {
            var query = _dbSet.Where(b => b.UserId == userId && !b.IsDeleted);

            if (isActive.HasValue)
            {
                query = query.Where(b => b.IsActive == isActive.Value);
            }

            return await query.CountAsync();
        }

        public async Task<int> GetActiveCountByUserIdAsync(int userId)
        {
            return await _dbSet.CountAsync(b =>
                b.UserId == userId &&
                !b.IsDeleted &&
                b.IsActive);
        }

        public async Task<BookingSystem?> GetByIdAndUserIdAsync(int id, int userId)
        {
            return await _dbSet.FirstOrDefaultAsync(b => b.Id == id && b.UserId == userId && !b.IsDeleted);
        }

        public async Task<BookingSystem?> GetByIdWithDetailsAsync(int id, int userId)
        {
            return await _dbSet
                .Include(b => b.Notebooks)
                .Include(b => b.Services.Where(s => !s.IsDeleted))
                    .ThenInclude(s => s.DaySchedules)
                .Include(b => b.Services.Where(s => !s.IsDeleted))
                    .ThenInclude(s => s.ScheduleExceptions.Where(e => !e.IsDeleted))
                .AsNoTracking()
                .FirstOrDefaultAsync(b => b.Id == id && b.UserId == userId && !b.IsDeleted);
        }

        public async Task<bool> ExistsByTitleAsync(int userId, string title, int? excludeId = null)
        {
            var query = _dbSet.Where(b => b.UserId == userId && b.Title == title && !b.IsDeleted);

            if (excludeId.HasValue)
            {
                query = query.Where(b => b.Id != excludeId.Value);
            }

            return await query.AnyAsync();
        }

        public async Task<bool> ExistsBySlugAsync(string slug, int? excludeId = null)
        {
            var query = _dbSet.Where(b => b.Slug == slug && !b.IsDeleted);

            if (excludeId.HasValue)
            {
                query = query.Where(b => b.Id != excludeId.Value);
            }

            return await query.AnyAsync();
        }
    }
}
