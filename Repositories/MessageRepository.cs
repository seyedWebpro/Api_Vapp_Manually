using Api_Vapp.Data;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;
using Api_Vapp._Utilities;
using Microsoft.EntityFrameworkCore;

namespace Api_Vapp.Repositories
{
    /// <summary>
    /// پیاده‌سازی Repository برای Message
    /// </summary>
    public class MessageRepository : BaseRepository<Message>, IMessageRepository
    {
        public MessageRepository(Api_Context context) : base(context)
        {
        }

        public async Task<IEnumerable<Message>> GetByUserIdAsync(int userId)
        {
            return await _dbSet
                .Where(m => m.UserId == userId && !m.IsDeleted)
                .OrderByDescending(m => m.CreatedAt)
                .ToListAsync();
        }

        public async Task<IEnumerable<Message>> GetByUserIdAndStatusAsync(int userId, string status)
        {
            return await _dbSet
                .Where(m => m.UserId == userId && m.Status == status && !m.IsDeleted)
                .OrderByDescending(m => m.CreatedAt)
                .ToListAsync();
        }

        public async Task<Message?> GetByIdWithTemplateAsync(int id)
        {
            return await _dbSet
                .Include(m => m.Template)
                .FirstOrDefaultAsync(m => m.Id == id && !m.IsDeleted);
        }

        public override async Task<Message?> GetByIdAsync(int id)
        {
            return await _dbSet
                .FirstOrDefaultAsync(m => m.Id == id && !m.IsDeleted);
        }
    }
}


