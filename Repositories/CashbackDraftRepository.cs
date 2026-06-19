using Api_Vapp.Data;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;
using Api_Vapp._Utilities;
using Microsoft.EntityFrameworkCore;

namespace Api_Vapp.Repositories
{
    /// <summary>
    /// پیاده‌سازی Repository برای CashbackDraft
    /// </summary>
    public class CashbackDraftRepository : BaseRepository<CashbackDraft>, ICashbackDraftRepository
    {
        public CashbackDraftRepository(Api_Context context) : base(context)
        {
        }

        public new async Task<CashbackDraft> AddAsync(CashbackDraft draft)
        {
            await _dbSet.AddAsync(draft);
            await _context.SaveChangesAsync();
            return draft;
        }

        public new async Task<CashbackDraft> UpdateAsync(CashbackDraft draft)
        {
            draft.UpdatedAt = DateTime.UtcNow;
            _dbSet.Update(draft);
            await _context.SaveChangesAsync();
            return draft;
        }

        public async Task<CashbackDraft?> GetByDraftIdAsync(string draftId, int userId)
        {
            return await _dbSet
                .Where(cd => cd.DraftId == draftId 
                    && cd.UserId == userId 
                    && !cd.IsDeleted)
                .FirstOrDefaultAsync();
        }

        public async Task<CashbackDraft?> GetActiveByDraftIdAsync(string draftId, int userId)
        {
            var now = DateTime.UtcNow;
            return await _dbSet
                .Where(cd => cd.DraftId == draftId 
                    && cd.UserId == userId 
                    && !cd.IsDeleted
                    && cd.ExpiresAt > now)
                .FirstOrDefaultAsync();
        }

        public async Task<bool> DeleteAsync(string draftId, int userId)
        {
            var draft = await GetByDraftIdAsync(draftId, userId);
            if (draft == null)
            {
                return false;
            }

            draft.IsDeleted = true;
            draft.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<int> DeleteExpiredAsync()
        {
            var now = DateTime.UtcNow;
            var expiredDrafts = await _dbSet
                .Where(cd => !cd.IsDeleted && cd.ExpiresAt <= now)
                .ToListAsync();

            foreach (var draft in expiredDrafts)
            {
                draft.IsDeleted = true;
                draft.UpdatedAt = DateTime.UtcNow;
            }

            if (expiredDrafts.Any())
            {
                await _context.SaveChangesAsync();
            }

            return expiredDrafts.Count;
        }

        public override async Task<CashbackDraft?> GetByIdAsync(int id)
        {
            return await _dbSet
                .FirstOrDefaultAsync(cd => cd.Id == id && !cd.IsDeleted);
        }
    }
}
