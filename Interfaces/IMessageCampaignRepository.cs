using Api_Vapp.Models;
using Api_Vapp._Utilities;

namespace Api_Vapp.Interfaces
{
    /// <summary>
    /// رابط Repository برای مدیریت کمپین‌های پیام
    /// </summary>
    public interface IMessageCampaignRepository : IBaseRepository<MessageCampaign>
    {
        Task<IEnumerable<MessageCampaign>> GetByUserIdAsync(int userId);
        Task<IEnumerable<MessageCampaign>> GetByUserIdAndStatusAsync(int userId, string status);
        Task<IEnumerable<MessageCampaign>> GetScheduledCampaignsAsync(DateTime? beforeDate = null);
        Task<MessageCampaign?> GetByIdWithRecipientsAsync(int id);
        Task<MessageCampaign?> GetByIdWithMessageAsync(int id);
    }
}

