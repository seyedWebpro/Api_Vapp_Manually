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

        public async Task<(IEnumerable<Contact> Contacts, int TotalCount)> GetByUserIdPagedAsync(
            int userId,
            int pageNumber,
            int pageSize,
            string? searchTerm = null)
        {
            var query = _dbSet
                .AsNoTracking()
                .Include(c => c.AdditionalInfo)
                .Include(c => c.ContactTags)
                    .ThenInclude(ct => ct.Tag)
                .Include(c => c.ContactNotebook)
                .Where(c => !c.IsDeleted
                    && c.ContactNotebook != null
                    && c.ContactNotebook.UserId == userId
                    && !c.ContactNotebook.IsDeleted);

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var term = searchTerm.ToLower().Trim();
                query = query.Where(c => c.MobileNumber.Contains(term)
                    || (c.FullName != null && c.FullName.ToLower().Contains(term)));
            }

            var totalCount = await query.CountAsync();

            var contacts = await query
                .OrderByDescending(c => c.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (contacts, totalCount);
        }

        public async Task<List<Contact>> GetByIdsForUserAsync(int userId, IEnumerable<int> contactIds)
        {
            var idList = contactIds.Where(id => id > 0).Distinct().ToList();
            if (!idList.Any())
            {
                return new List<Contact>();
            }

            return await _dbSet
                .AsNoTracking()
                .Where(c => idList.Contains(c.Id)
                    && !c.IsDeleted
                    && c.ContactNotebook != null
                    && c.ContactNotebook.UserId == userId
                    && !c.ContactNotebook.IsDeleted)
                .ToListAsync();
        }
    }
}


