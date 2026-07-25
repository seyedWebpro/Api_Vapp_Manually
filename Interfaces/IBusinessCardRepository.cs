using Api_Vapp.Models;
using Api_Vapp._Utilities;

namespace Api_Vapp.Interfaces
{
    public interface IBusinessCardRepository : IBaseRepository<BusinessCard>
    {
        Task<BusinessCard?> GetByIdWithDetailsReadOnlyAsync(int id);

        Task<BusinessCard?> GetByIdWithDetailsTrackedAsync(int id);

        Task<BusinessCard?> GetByIdWithDetailsTrackedForUserAsync(int id, int userId);

        Task<BusinessCard?> GetOwnedCardAsync(int id, int userId, bool tracked = false);

        Task<BusinessCard?> GetBySlugReadOnlyAsync(string slug);

        Task<bool> SlugExistsAsync(string slug, int? excludeCardId = null);

        Task<IReadOnlyList<string>> GetExistingSlugsWithPrefixAsync(string slugPrefix, int? excludeCardId = null);

        Task<(IReadOnlyList<BusinessCard> Items, int TotalCount)> GetByUserIdPagedAsync(
            int userId,
            int pageNumber,
            int pageSize);
    }
}
