using Api_Vapp.Data;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;
using Api_Vapp._Utilities;
using Microsoft.EntityFrameworkCore;

namespace Api_Vapp.Repositories
{
    /// <summary>
    /// پیاده‌سازی Repository برای MessageCampaign
    /// </summary>
    public class MessageCampaignRepository : BaseRepository<MessageCampaign>, IMessageCampaignRepository
    {
        public MessageCampaignRepository(Api_Context context) : base(context)
        {
        }

        public async Task<IEnumerable<MessageCampaign>> GetByUserIdAsync(int userId)
        {
            return await _dbSet
                .Where(mc => mc.UserId == userId && !mc.IsDeleted)
                .OrderByDescending(mc => mc.CreatedAt)
                .ToListAsync();
        }

        public async Task<IEnumerable<MessageCampaign>> GetByUserIdAndStatusAsync(int userId, string status)
        {
            return await _dbSet
                .Where(mc => mc.UserId == userId && mc.Status == status && !mc.IsDeleted)
                .OrderByDescending(mc => mc.CreatedAt)
                .ToListAsync();
        }

        public async Task<IEnumerable<MessageCampaign>> GetScheduledCampaignsAsync(DateTime? beforeDate = null)
        {
            var query = _dbSet
                .Where(mc => mc.SendType == "Scheduled"
                    && mc.IsActive
                    && mc.Status == "Pending"
                    && mc.ScheduledAt.HasValue
                    && !mc.IsDeleted);

            if (beforeDate.HasValue)
            {
                query = query.Where(mc => mc.ScheduledAt <= beforeDate.Value);
            }

            return await query
                .OrderBy(mc => mc.ScheduledAt)
                .ToListAsync();
        }

        public async Task<MessageCampaign?> GetByIdWithRecipientsAsync(int id)
        {
            return await _dbSet
                .Include(mc => mc.Recipients)
                .FirstOrDefaultAsync(mc => mc.Id == id && !mc.IsDeleted);
        }

        public async Task<MessageCampaign?> GetByIdWithMessageAsync(int id)
        {
            return await _dbSet
                .Include(mc => mc.Message)
                .ThenInclude(m => m!.Template)
                .FirstOrDefaultAsync(mc => mc.Id == id && !mc.IsDeleted);
        }

        public override async Task<MessageCampaign?> GetByIdAsync(int id)
        {
            return await _dbSet
                .FirstOrDefaultAsync(mc => mc.Id == id && !mc.IsDeleted);
        }
    }
}


