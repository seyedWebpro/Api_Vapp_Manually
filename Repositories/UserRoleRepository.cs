using Api_Vapp.Data;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;
using Api_Vapp._Utilities;
using Microsoft.EntityFrameworkCore;

namespace Api_Vapp.Repositories
{
    /// <summary>
    /// پیاده‌سازی Repository برای UserRole
    /// تمام عملیات مربوط به دسترسی به داده‌های UserRole در اینجا قرار دارد
    /// </summary>
    public class UserRoleRepository : BaseRepository<UserRole>, IUserRoleRepository
    {
        public UserRoleRepository(Api_Context context) : base(context)
        {
        }

        public async Task<IEnumerable<UserRole>> GetUserRolesAsync(int userId)
        {
            return await _dbSet
                .Where(ur => ur.UserId == userId && !ur.IsDeleted)
                .Include(ur => ur.Role)
                .ToListAsync();
        }

        public async Task<IEnumerable<UserRole>> GetRoleUsersAsync(int roleId)
        {
            return await _dbSet
                .Where(ur => ur.RoleId == roleId && !ur.IsDeleted)
                .Include(ur => ur.User)
                .ToListAsync();
        }

        public async Task<bool> ExistsAsync(int userId, int roleId)
        {
            return await _dbSet
                .AnyAsync(ur => ur.UserId == userId && ur.RoleId == roleId && !ur.IsDeleted);
        }

        public async Task<UserRole?> GetUserRoleAsync(int userId, int roleId)
        {
            return await _dbSet
                .FirstOrDefaultAsync(ur => ur.UserId == userId && ur.RoleId == roleId && !ur.IsDeleted);
        }

        public async Task<IEnumerable<UserRole>> GetActiveUserRolesAsync(int userId)
        {
            return await _dbSet
                .Where(ur => ur.UserId == userId && ur.IsActive && !ur.IsDeleted)
                .Include(ur => ur.Role)
                .ToListAsync();
        }

        public override async Task<UserRole?> GetByIdAsync(int id)
        {
            return await _dbSet
                .FirstOrDefaultAsync(ur => ur.Id == id && !ur.IsDeleted);
        }
    }
}

