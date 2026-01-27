using Api_Vapp.Data;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;
using Api_Vapp._Utilities;
using Microsoft.EntityFrameworkCore;

namespace Api_Vapp.Repositories
{
    /// <summary>
    /// پیاده‌سازی Repository برای MessageTemplate
    /// </summary>
    public class MessageTemplateRepository : BaseRepository<MessageTemplate>, IMessageTemplateRepository
    {
        public MessageTemplateRepository(Api_Context context) : base(context)
        {
        }

        public async Task<IEnumerable<MessageTemplate>> GetByUserIdAsync(int userId)
        {
            return await _dbSet
                .Where(mt => mt.UserId == userId && !mt.IsDeleted)
                .OrderByDescending(mt => mt.CreatedAt)
                .ToListAsync();
        }

        public async Task<IEnumerable<MessageTemplate>> GetActiveByUserIdAsync(int userId)
        {
            return await _dbSet
                .Where(mt => mt.UserId == userId && mt.IsActive && !mt.IsDeleted)
                .OrderByDescending(mt => mt.CreatedAt)
                .ToListAsync();
        }

        public async Task<IEnumerable<MessageTemplate>> GetByUserIdAndCategoryAsync(int userId, string category)
        {
            return await _dbSet
                .Where(mt => mt.UserId == userId && mt.Category == category && !mt.IsDeleted)
                .OrderByDescending(mt => mt.CreatedAt)
                .ToListAsync();
        }

        public async Task<IEnumerable<MessageTemplate>> GetAllActiveAsync()
        {
            return await _dbSet
                .Where(mt => mt.IsActive && !mt.IsDeleted)
                .OrderByDescending(mt => mt.CreatedAt)
                .ToListAsync();
        }

        public override async Task<MessageTemplate?> GetByIdAsync(int id)
        {
            return await _dbSet
                .FirstOrDefaultAsync(mt => mt.Id == id && !mt.IsDeleted);
        }
    }
}


