using Api_Vapp.Data;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;
using Api_Vapp._Utilities;
using Microsoft.EntityFrameworkCore;

namespace Api_Vapp.Repositories
{
    /// <summary>
    /// پیاده‌سازی Repository برای MessageSession
    /// </summary>
    public class MessageSessionRepository : BaseRepository<MessageSession>, IMessageSessionRepository
    {
        public MessageSessionRepository(Api_Context context) : base(context)
        {
        }

        public async Task<MessageSession?> GetByMessageIdAsync(int messageId, int userId)
        {
            return await _dbSet
                .Where(ms => ms.MessageId == messageId 
                    && ms.UserId == userId 
                    && !ms.IsDeleted)
                .OrderByDescending(ms => ms.CreatedAt)
                .FirstOrDefaultAsync();
        }

        public async Task<MessageSession?> GetActiveSessionByMessageIdAsync(int messageId, int userId)
        {
            var now = DateTime.UtcNow;
            return await _dbSet
                .Where(ms => ms.MessageId == messageId 
                    && ms.UserId == userId 
                    && !ms.IsDeleted
                    && !ms.IsUsed
                    && (ms.ExpiresAt == null || ms.ExpiresAt > now))
                .OrderByDescending(ms => ms.CreatedAt)
                .FirstOrDefaultAsync();
        }

        public async Task<MessageSession?> GetActiveSessionBySessionIdAsync(int sessionId, int userId)
        {
            var now = DateTime.UtcNow;
            return await _dbSet
                .Where(ms => ms.Id == sessionId 
                    && ms.UserId == userId 
                    && !ms.IsDeleted
                    && !ms.IsUsed
                    && (ms.ExpiresAt == null || ms.ExpiresAt > now))
                .FirstOrDefaultAsync();
        }

        public async Task<IEnumerable<MessageSession>> GetExpiredSessionsAsync(DateTime? beforeDate = null)
        {
            var query = _dbSet
                .Where(ms => !ms.IsDeleted 
                    && ms.ExpiresAt.HasValue 
                    && ms.ExpiresAt <= DateTime.UtcNow);

            if (beforeDate.HasValue)
            {
                query = query.Where(ms => ms.ExpiresAt <= beforeDate.Value);
            }

            return await query.ToListAsync();
        }

        public async Task DeleteExpiredSessionsAsync(DateTime? beforeDate = null)
        {
            var expiredSessions = await GetExpiredSessionsAsync(beforeDate);
            foreach (var session in expiredSessions)
            {
                session.IsDeleted = true;
                session.UpdatedAt = DateTime.UtcNow;
            }
            if (expiredSessions.Any())
            {
                await _context.SaveChangesAsync();
            }
        }

        public override async Task<MessageSession?> GetByIdAsync(int id)
        {
            return await _dbSet
                .FirstOrDefaultAsync(ms => ms.Id == id && !ms.IsDeleted);
        }
    }
}

