using Api_Vapp.Models;

namespace Api_Vapp.Interfaces
{
    /// <summary>
    /// دسترسی داده کمپین حرفه‌ای (مالکیت، لیست صفحه‌بندی‌شده، ذخیره)
    /// </summary>
    public interface IProfessionalCampaignRepository
    {
        Task<ProfessionalCampaign?> GetOwnedAsync(int userId, int id, bool tracking = false, bool includeRecipients = false);
        Task<(List<ProfessionalCampaign> Items, int TotalCount)> GetPagedOwnedAsync(int userId, int pageNumber, int pageSize);
        Task AddAsync(ProfessionalCampaign campaign);
        Task SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
