using Api_Vapp.Data;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;
using Microsoft.EntityFrameworkCore;

namespace Api_Vapp.Repositories
{
    public class AppNewsTickerRepository : IAppNewsTickerRepository
    {
        private readonly Api_Context _context;

        public AppNewsTickerRepository(Api_Context context)
        {
            _context = context;
        }

        public async Task<List<AppNewsTickerMessage>> GetAllAsync(bool includeInactive)
        {
            var query = _context.AppNewsTickerMessages.AsNoTracking().Where(message => !message.IsDeleted);
            if (!includeInactive)
                query = query.Where(message => message.IsActive);

            return await Order(query).ToListAsync();
        }

        public Task<List<AppNewsTickerMessage>> GetActiveAsync() =>
            Order(_context.AppNewsTickerMessages.AsNoTracking()
                    .Where(message => !message.IsDeleted && message.IsActive))
                .ToListAsync();

        public Task<AppNewsTickerMessage?> GetByIdAsync(int id) =>
            _context.AppNewsTickerMessages
                .FirstOrDefaultAsync(message => message.Id == id && !message.IsDeleted);

        public void Add(AppNewsTickerMessage entity) => _context.AppNewsTickerMessages.Add(entity);

        public Task SaveChangesAsync() => _context.SaveChangesAsync();

        private static IOrderedQueryable<AppNewsTickerMessage> Order(IQueryable<AppNewsTickerMessage> query) =>
            query.OrderBy(message => message.SortOrder).ThenBy(message => message.Id);
    }
}
