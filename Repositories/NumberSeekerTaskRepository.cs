using Api_Vapp.Data;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;
using Microsoft.EntityFrameworkCore;

namespace Api_Vapp.Repositories
{
    public class NumberSeekerTaskRepository : INumberSeekerTaskRepository
    {
        private readonly Api_Context _context;

        public NumberSeekerTaskRepository(Api_Context context)
        {
            _context = context;
        }

        public async Task<NumberSeekerTask> AddAsync(NumberSeekerTask task)
        {
            _context.NumberSeekerTasks.Add(task);
            await _context.SaveChangesAsync();
            return task;
        }

        public Task<NumberSeekerTask?> GetByScraperTaskIdAsync(string scraperTaskId)
        {
            return _context.NumberSeekerTasks
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.ScraperTaskId == scraperTaskId);
        }

        public Task<NumberSeekerTask?> GetByScraperTaskIdTrackedAsync(string scraperTaskId)
        {
            return _context.NumberSeekerTasks
                .FirstOrDefaultAsync(t => t.ScraperTaskId == scraperTaskId);
        }

        public Task<NumberSeekerTask?> GetByScraperTaskIdAndUserIdAsync(string scraperTaskId, int userId)
        {
            return _context.NumberSeekerTasks
                .FirstOrDefaultAsync(t => t.ScraperTaskId == scraperTaskId && t.UserId == userId);
        }

        public async Task UpdateAsync(NumberSeekerTask task)
        {
            _context.NumberSeekerTasks.Update(task);
            await _context.SaveChangesAsync();
        }

        public async Task<List<NumberSeekerTask>> GetRecentByUserIdAsync(int userId, int limit = 20)
        {
            if (limit < 1) limit = 1;
            if (limit > 100) limit = 100;

            return await _context.NumberSeekerTasks
                .AsNoTracking()
                .Where(t => t.UserId == userId)
                .OrderByDescending(t => t.CreatedAt)
                .Take(limit)
                .ToListAsync();
        }
    }
}
