using Api_Vapp.Models;

namespace Api_Vapp.Interfaces
{
    public interface IForbiddenWordRepository
    {
        Task<List<ForbiddenWord>> GetAllAsync(bool includeInactive, string? search = null);
        Task<List<ForbiddenWord>> GetActiveAsync();
        Task<ForbiddenWord?> GetByIdAsync(int id);
        Task<ForbiddenWord?> FindByNormalizedAsync(string normalizedWord, int? excludeId = null);
        Task<HashSet<string>> GetExistingNormalizedAsync(IEnumerable<string> normalizedWords);
        void Add(ForbiddenWord entity);
        void AddRange(IEnumerable<ForbiddenWord> entities);
        Task SaveChangesAsync();
    }
}
