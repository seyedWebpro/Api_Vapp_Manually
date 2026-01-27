using Api_Vapp.Data;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;
using Api_Vapp._Utilities;
using Microsoft.EntityFrameworkCore;

namespace Api_Vapp.Repositories
{
    /// <summary>
    /// پیاده‌سازی Repository برای ContactNotebook
    /// </summary>
    public class ContactNotebookRepository : BaseRepository<ContactNotebook>, IContactNotebookRepository
    {
        public ContactNotebookRepository(Api_Context context) : base(context)
        {
        }

        public async Task<IEnumerable<ContactNotebook>> GetByUserIdAsync(int userId, bool? isActive = null)
        {
            var query = _dbSet
                .Where(n => n.UserId == userId && !n.IsDeleted);

            if (isActive.HasValue)
            {
                query = query.Where(n => n.IsActive == isActive.Value);
            }

            return await query
                .OrderByDescending(n => n.CreatedAt)
                .ToListAsync();
        }

        public async Task<bool> ExistsByNameForUserAsync(int userId, string name, int? excludeId = null)
        {
            var query = _dbSet
                .Where(n => n.UserId == userId 
                    && n.Name == name 
                    && !n.IsDeleted);

            if (excludeId.HasValue)
            {
                query = query.Where(n => n.Id != excludeId.Value);
            }

            return await query.AnyAsync();
        }

        public async Task<ContactNotebook?> GetByIdWithContactsAsync(int id)
        {
            return await _dbSet
                .Include(n => n.Contacts.Where(c => !c.IsDeleted))
                .FirstOrDefaultAsync(n => n.Id == id && !n.IsDeleted);
        }

        public override async Task<ContactNotebook?> GetByIdAsync(int id)
        {
            return await _dbSet
                .FirstOrDefaultAsync(n => n.Id == id && !n.IsDeleted);
        }
    }
}


