using Api_Vapp.Models;
using Api_Vapp._Utilities;

namespace Api_Vapp.Interfaces
{
    /// <summary>
    /// رابط Repository برای مدیریت پیام‌ها
    /// </summary>
    public interface IMessageRepository : IBaseRepository<Message>
    {
        Task<IEnumerable<Message>> GetByUserIdAsync(int userId);
        Task<IEnumerable<Message>> GetByUserIdAndStatusAsync(int userId, string status);
        Task<Message?> GetByIdWithTemplateAsync(int id);
    }
}

