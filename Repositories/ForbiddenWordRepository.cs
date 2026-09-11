using Api_Vapp.Data;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;
using Microsoft.EntityFrameworkCore;

namespace Api_Vapp.Repositories
{
    public class ForbiddenWordRepository : IForbiddenWordRepository
    {
        private readonly Api_Context _context;

        public ForbiddenWordRepository(Api_Context context)
        {
            _context = context;
        }

        public async Task<List<ForbiddenWord>> GetAllAsync(bool includeInactive, string? search = null)
        {
            var query = _context.ForbiddenWords.AsNoTracking().Where(w => !w.IsDeleted);

            if (!includeInactive)
                query = query.Where(w => w.IsActive);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(w => w.Word.Contains(term) || w.NormalizedWord.Contains(term));
            }

            return await query
                .OrderBy(w => w.Word)
                .ThenBy(w => w.Id)
                .ToListAsync();
        }

        public Task<List<ForbiddenWord>> GetActiveAsync() =>
            _context.ForbiddenWords.AsNoTracking()
                .Where(w => !w.IsDeleted && w.IsActive)
                .OrderByDescending(w => w.NormalizedWord.Length)
                .ThenBy(w => w.Id)
                .ToListAsync();

        public Task<ForbiddenWord?> GetByIdAsync(int id) =>
            _context.ForbiddenWords
                .FirstOrDefaultAsync(w => w.Id == id && !w.IsDeleted);

        public Task<ForbiddenWord?> FindByNormalizedAsync(string normalizedWord, int? excludeId = null)
        {
            var query = _context.ForbiddenWords
                .Where(w => !w.IsDeleted && w.NormalizedWord == normalizedWord);

            if (excludeId.HasValue)
                query = query.Where(w => w.Id != excludeId.Value);

            return query.FirstOrDefaultAsync();
        }

        public async Task<HashSet<string>> GetExistingNormalizedAsync(IEnumerable<string> normalizedWords)
        {
            var list = normalizedWords.Where(w => !string.IsNullOrEmpty(w)).Distinct().ToList();
            if (list.Count == 0)
                return new HashSet<string>(StringComparer.Ordinal);

            var existing = await _context.ForbiddenWords.AsNoTracking()
                .Where(w => !w.IsDeleted && list.Contains(w.NormalizedWord))
                .Select(w => w.NormalizedWord)
                .ToListAsync();

            return existing.ToHashSet(StringComparer.Ordinal);
        }

        public void Add(ForbiddenWord entity) => _context.ForbiddenWords.Add(entity);

        public void AddRange(IEnumerable<ForbiddenWord> entities) => _context.ForbiddenWords.AddRange(entities);

        public Task SaveChangesAsync() => _context.SaveChangesAsync();
    }
}
