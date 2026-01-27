using Api_Vapp.Models;
using Api_Vapp._Utilities;

namespace Api_Vapp.Interfaces
{
    /// <summary>
    /// رابط Repository برای مدیریت Session های پیام
    /// </summary>
    public interface IMessageSessionRepository : IBaseRepository<MessageSession>
    {
        Task<MessageSession?> GetByMessageIdAsync(int messageId, int userId);
        Task<MessageSession?> GetActiveSessionByMessageIdAsync(int messageId, int userId);
        Task<MessageSession?> GetActiveSessionBySessionIdAsync(int sessionId, int userId);
        Task<IEnumerable<MessageSession>> GetExpiredSessionsAsync(DateTime? beforeDate = null);
        Task DeleteExpiredSessionsAsync(DateTime? beforeDate = null);
    }
}

