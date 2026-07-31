using Api_Vapp.Data;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;
using Microsoft.EntityFrameworkCore;

namespace Api_Vapp.Repositories
{
    public class UserDeviceRepository : IUserDeviceRepository
    {
        private readonly Api_Context _context;

        public UserDeviceRepository(Api_Context context)
        {
            _context = context;
        }

        public async Task<UserDevice?> GetByTokenAsync(string fcmToken)
        {
            // شامل soft-deleted تا upsert با unique index روی FcmToken درست کار کند
            return await _context.UserDevices
                .FirstOrDefaultAsync(d => d.FcmToken == fcmToken);
        }

        public async Task<List<UserDevice>> GetActiveByUserIdAsync(int userId)
        {
            return await _context.UserDevices
                .AsNoTracking()
                .Where(d => d.UserId == userId && d.IsActive && !d.IsDeleted)
                .ToListAsync();
        }

        public async Task<UserDevice> AddAsync(UserDevice device)
        {
            device.CreatedAt = DateTime.UtcNow;
            device.LastSeenAt = DateTime.UtcNow;
            await _context.UserDevices.AddAsync(device);
            await _context.SaveChangesAsync();
            return device;
        }

        public async Task<UserDevice> UpdateAsync(UserDevice device)
        {
            device.UpdatedAt = DateTime.UtcNow;
            _context.UserDevices.Update(device);
            await _context.SaveChangesAsync();
            return device;
        }

        public async Task SoftDeleteAsync(UserDevice device)
        {
            device.IsDeleted = true;
            device.IsActive = false;
            device.UpdatedAt = DateTime.UtcNow;
            _context.UserDevices.Update(device);
            await _context.SaveChangesAsync();
        }
    }
}
