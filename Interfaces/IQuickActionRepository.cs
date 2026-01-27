using Api_Vapp.Models;
using Api_Vapp._Utilities;

namespace Api_Vapp.Interfaces
{
    /// <summary>
    /// رابط Repository برای مدیریت اقدام‌های سریع (لینک‌ها)
    /// </summary>
    public interface IQuickActionRepository : IBaseRepository<QuickAction>
    {
        Task<IEnumerable<QuickAction>> GetByUserIdAsync(int userId);
        Task<IEnumerable<QuickAction>> GetActiveByUserIdAsync(int userId);
    }
}












