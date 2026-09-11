using Api_Vapp.Data;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;
using Api_Vapp.Utilities;
using Api_Vapp._Utilities;
using Microsoft.EntityFrameworkCore;

namespace Api_Vapp.Repositories
{
    public class SpecialOccasionRepository : BaseRepository<SpecialOccasion>, ISpecialOccasionRepository
    {
        public SpecialOccasionRepository(Api_Context context) : base(context)
        {
        }

        public async Task<IEnumerable<SpecialOccasion>> GetByUserIdAsync(int? userId)
        {
            return await _dbSet.AsNoTracking()
                .Where(so => (userId == null ? so.UserId == null : so.UserId == userId) && !so.IsDeleted)
                .OrderBy(so => so.SortOrder)
                .ThenBy(so => so.Month)
                .ThenBy(so => so.Day)
                .ToListAsync();
        }

        public async Task<IEnumerable<SpecialOccasion>> GetActiveByUserIdAsync(int? userId)
        {
            return await _dbSet.AsNoTracking()
                .Where(so => (userId == null ? so.UserId == null : so.UserId == userId)
                    && so.IsActive && !so.IsDeleted)
                .OrderBy(so => so.SortOrder)
                .ThenBy(so => so.Month)
                .ThenBy(so => so.Day)
                .ToListAsync();
        }

        public async Task<IEnumerable<SpecialOccasion>> GetSystemOccasionsAsync()
        {
            return await _dbSet.AsNoTracking()
                .Where(so => so.IsSystem && so.IsActive && !so.IsDeleted)
                .OrderBy(so => so.SortOrder)
                .ThenBy(so => so.Month)
                .ThenBy(so => so.Day)
                .ToListAsync();
        }

        public async Task<List<SpecialOccasion>> GetCatalogForUserAsync(int userId)
        {
            return await _dbSet.AsNoTracking()
                .Where(so => !so.IsDeleted
                    && so.IsActive
                    && (so.IsSystem || so.UserId == userId))
                .OrderBy(so => so.Category)
                .ThenBy(so => so.SortOrder)
                .ThenBy(so => so.Month)
                .ThenBy(so => so.Day)
                .ToListAsync();
        }

        public async Task<List<SpecialOccasion>> GetOccasionsMatchingTodayAsync(OccasionCalendarHelper.CalendarDayParts today)
        {
            // فیلتر اولیه روی Month/Day برای استفاده از ایندکس؛ تطبیق تقویم در حافظه روی مجموعه کوچک
            var monthCandidates = new HashSet<byte>
            {
                (byte)today.JalaliMonth,
                (byte)today.GregorianMonth,
                (byte)today.HijriMonth
            };

            var candidates = await _dbSet.AsNoTracking()
                .Where(so => !so.IsDeleted
                    && so.IsActive
                    && monthCandidates.Contains(so.Month))
                .ToListAsync();

            return candidates
                .Where(so => OccasionCalendarHelper.IsOccasionToday(so.CalendarType, so.Month, so.Day, today))
                .ToList();
        }

        public async Task<SpecialOccasion?> GetByCodeAsync(string code)
        {
            return await _dbSet.FirstOrDefaultAsync(so => so.Code == code && !so.IsDeleted);
        }

        public override async Task<SpecialOccasion?> GetByIdAsync(int id)
        {
            return await _dbSet.FirstOrDefaultAsync(so => so.Id == id && !so.IsDeleted);
        }
    }
}
