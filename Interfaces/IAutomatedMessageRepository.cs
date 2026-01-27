using Api_Vapp.Models;
using Api_Vapp._Utilities;

namespace Api_Vapp.Interfaces
{
    /// <summary>
    /// رابط Repository برای مدیریت پیام‌های خودکار
    /// </summary>
    public interface IAutomatedMessageRepository : IBaseRepository<AutomatedMessage>
    {
        Task<IEnumerable<AutomatedMessage>> GetByUserIdAsync(int userId);
        Task<IEnumerable<AutomatedMessage>> GetActiveByUserIdAsync(int userId);
        Task<IEnumerable<AutomatedMessage>> GetByUserIdAndTypeAsync(int userId, string automationType);
        Task<AutomatedMessage?> GetByIdWithExecutionsAsync(int id);
        Task<AutomatedMessage?> GetByIdWithMessageAsync(int id);
        Task<AutomatedMessage?> GetByIdWithMessageAndOccasionAsync(int id);
    }
}

