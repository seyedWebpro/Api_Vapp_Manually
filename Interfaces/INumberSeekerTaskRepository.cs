using Api_Vapp.Models;

namespace Api_Vapp.Interfaces
{
    public interface INumberSeekerTaskRepository
    {
        Task<NumberSeekerTask> AddAsync(NumberSeekerTask task);
        Task<NumberSeekerTask?> GetByScraperTaskIdAsync(string scraperTaskId);
        Task<NumberSeekerTask?> GetByScraperTaskIdTrackedAsync(string scraperTaskId);
        Task<NumberSeekerTask?> GetByScraperTaskIdAndUserIdAsync(string scraperTaskId, int userId);
        Task UpdateAsync(NumberSeekerTask task);
        Task<List<NumberSeekerTask>> GetRecentByUserIdAsync(int userId, int limit = 20);
    }
}
