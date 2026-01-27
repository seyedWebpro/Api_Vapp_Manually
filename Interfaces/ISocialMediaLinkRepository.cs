using Api_Vapp.Models;
using Api_Vapp._Utilities;

namespace Api_Vapp.Interfaces
{
    /// <summary>
    /// رابط Repository برای مدیریت لینک‌های شبکه‌های اجتماعی
    /// </summary>
    public interface ISocialMediaLinkRepository : IBaseRepository<SocialMediaLink>
    {
        Task<IEnumerable<SocialMediaLink>> GetByUserIdAsync(int userId);
        Task<IEnumerable<SocialMediaLink>> GetActiveByUserIdAsync(int userId);
    }
}





