using Api_Vapp.Data;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;
using Api_Vapp._Utilities;
using Microsoft.EntityFrameworkCore;

namespace Api_Vapp.Repositories
{
    /// <summary>
    /// پیاده‌سازی Repository برای Role
    /// تمام عملیات مربوط به دسترسی به داده‌های Role در اینجا قرار دارد
    /// </summary>
    public class RoleRepository : BaseRepository<Role>, IRoleRepository
    {
        public RoleRepository(Api_Context context) : base(context)
        {
        }

        public async Task<Role?> GetByNameAsync(string name)
        {
            return await _dbSet
                .FirstOrDefaultAsync(r => r.Name == name);
        }

        public async Task<bool> ExistsByNameAsync(string name)
        {
            return await _dbSet
                .AnyAsync(r => r.Name == name && !r.IsDeleted);
        }

        public async Task<IEnumerable<Role>> GetActiveRolesAsync()
        {
            return await _dbSet
                .Where(r => r.IsActive && !r.IsDeleted)
                .OrderBy(r => r.Name)
                .ToListAsync();
        }

        public override async Task<Role?> GetByIdAsync(int id)
        {
            return await _dbSet
                .FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted);
        }
    }
}

