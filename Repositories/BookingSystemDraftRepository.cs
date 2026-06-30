using Api_Vapp.Data;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;
using Api_Vapp._Utilities;
using Microsoft.EntityFrameworkCore;

namespace Api_Vapp.Repositories
{
    public class BookingSystemDraftRepository : BaseRepository<BookingSystemDraft>, IBookingSystemDraftRepository
    {
        public BookingSystemDraftRepository(Api_Context context) : base(context)
        {
        }

        public new async Task<BookingSystemDraft> AddAsync(BookingSystemDraft draft)
        {
            await _dbSet.AddAsync(draft);
            await _context.SaveChangesAsync();
            return draft;
        }

        public new async Task<BookingSystemDraft> UpdateAsync(BookingSystemDraft draft)
        {
            draft.UpdatedAt = DateTime.UtcNow;
            _dbSet.Update(draft);
            await _context.SaveChangesAsync();
            return draft;
        }

        public async Task<BookingSystemDraft?> GetActiveByDraftIdAsync(string draftId, int userId)
        {
            var now = DateTime.UtcNow;
            return await _dbSet.FirstOrDefaultAsync(d =>
                d.DraftId == draftId &&
                d.UserId == userId &&
                !d.IsDeleted &&
                d.ExpiresAt > now);
        }

        public async Task<bool> DeleteAsync(string draftId, int userId)
        {
            var draft = await _dbSet.FirstOrDefaultAsync(d =>
                d.DraftId == draftId && d.UserId == userId && !d.IsDeleted);

            if (draft == null)
            {
                return false;
            }

            draft.IsDeleted = true;
            draft.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
