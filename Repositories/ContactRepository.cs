using Api_Vapp.Data;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;
using Api_Vapp._Utilities;
using Microsoft.EntityFrameworkCore;

namespace Api_Vapp.Repositories
{
    /// <summary>
    /// پیاده‌سازی Repository برای Contact
    /// </summary>
    public class ContactRepository : BaseRepository<Contact>, IContactRepository
    {
        public ContactRepository(Api_Context context) : base(context)
        {
        }

        public async Task<IEnumerable<Contact>> GetByNotebookIdAsync(int notebookId)
        {
            return await _dbSet
                .Include(c => c.AdditionalInfo)
                .Include(c => c.ContactTags)
                    .ThenInclude(ct => ct.Tag)
                .Where(c => c.ContactNotebookId == notebookId && !c.IsDeleted)
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();
        }

        public async Task<IEnumerable<Contact>> SearchAsync(int notebookId, string searchTerm)
        {
            var term = searchTerm.ToLower().Trim();
            
            return await _dbSet
                .Include(c => c.Occasions)
                .Include(c => c.AdditionalInfo)
                .Include(c => c.ContactTags)
                    .ThenInclude(ct => ct.Tag)
                .Where(c => c.ContactNotebookId == notebookId 
                    && !c.IsDeleted
                    && (c.MobileNumber.Contains(term)
                        || (c.FullName != null && c.FullName.ToLower().Contains(term))))
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();
        }

        public async Task<bool> ExistsByMobileNumberInNotebookAsync(int notebookId, string mobileNumber, int? excludeId = null)
        {
            var query = _dbSet
                .Where(c => c.ContactNotebookId == notebookId 
                    && c.MobileNumber == mobileNumber 
                    && !c.IsDeleted);

            if (excludeId.HasValue)
            {
                query = query.Where(c => c.Id != excludeId.Value);
            }

            return await query.AnyAsync();
        }

        public async Task<Contact?> GetByIdWithAdditionalInfoAsync(int id)
        {
            return await _dbSet
                .Include(c => c.AdditionalInfo)
                .Include(c => c.Occasions)
                .Include(c => c.ContactNotebook)
                .Include(c => c.ContactTags)
                    .ThenInclude(ct => ct.Tag)
                .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted);
        }

        public override async Task<Contact?> GetByIdAsync(int id)
        {
            return await _dbSet
                .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted);
        }

        public async Task<IEnumerable<Contact>> GetAllContactsAsync()
        {
            return await _dbSet
                .Include(c => c.Occasions)
                .Include(c => c.AdditionalInfo)
                .Include(c => c.ContactTags)
                    .ThenInclude(ct => ct.Tag)
                .Where(c => !c.IsDeleted)
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();
        }

        public async Task<IEnumerable<Contact>> SearchAllContactsAsync(string searchTerm)
        {
            var term = searchTerm.ToLower().Trim();
            
            return await _dbSet
                .Include(c => c.Occasions)
                .Include(c => c.AdditionalInfo)
                .Include(c => c.ContactTags)
                    .ThenInclude(ct => ct.Tag)
                .Where(c => !c.IsDeleted
                    && (c.MobileNumber.Contains(term)
                        || (c.FullName != null && c.FullName.ToLower().Contains(term))))
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();
        }
    }
}


