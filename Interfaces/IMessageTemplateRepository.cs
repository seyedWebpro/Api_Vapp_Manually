using Api_Vapp.Models;
using Api_Vapp._Utilities;

namespace Api_Vapp.Interfaces
{
    /// <summary>
    /// رابط Repository برای مدیریت قالب‌های پیام
    /// </summary>
    public interface IMessageTemplateRepository : IBaseRepository<MessageTemplate>
    {
        Task<IEnumerable<MessageTemplate>> GetByUserIdAsync(int userId);
        Task<IEnumerable<MessageTemplate>> GetActiveByUserIdAsync(int userId);
        Task<IEnumerable<MessageTemplate>> GetByUserIdAndCategoryAsync(int userId, string category);
        Task<IEnumerable<MessageTemplate>> GetAllActiveAsync();
    }
}

