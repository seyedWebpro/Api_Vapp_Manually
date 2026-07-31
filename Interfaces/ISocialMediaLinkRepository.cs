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

        /// <summary>
        /// لیست صفحه‌بندی‌شده لینک‌های کاربر (فقط خواندنی، در سطح دیتابیس)
        /// </summary>
        Task<(List<SocialMediaLink> Items, int TotalCount)> GetPagedByUserIdAsync(
            int userId,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// دریافت لینک متعلق به کاربر (بدون tracking برای خواندن)
        /// </summary>
        Task<SocialMediaLink?> GetOwnedByIdAsync(int id, int userId, bool asNoTracking = true);

        /// <summary>
        /// تعداد لینک‌های فعال و حذف‌نشده کاربر
        /// </summary>
        Task<int> CountActiveByUserIdAsync(int userId, CancellationToken cancellationToken = default);
    }
}
