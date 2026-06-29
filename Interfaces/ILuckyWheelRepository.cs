using Api_Vapp.Models;
using Api_Vapp._Utilities;

namespace Api_Vapp.Interfaces
{
    public interface ILuckyWheelRepository : IBaseRepository<LuckyWheel>
    {
        Task<LuckyWheel?> GetByIdWithDetailsReadOnlyAsync(int id);

        Task<LuckyWheel?> GetByIdWithDetailsTrackedAsync(int id);

        Task<LuckyWheel?> GetOwnedWheelAsync(int id, int userId, bool tracked = false);

        Task<bool> SlugExistsAsync(string slug, int? excludeWheelId = null);

        Task<IReadOnlyList<string>> GetExistingSlugsWithPrefixAsync(string slugPrefix, int? excludeWheelId = null);

        Task<(IReadOnlyList<LuckyWheel> Items, int TotalCount)> GetByUserIdPagedAsync(
            int userId,
            int pageNumber,
            int pageSize);
    }
}
