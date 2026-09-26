using Api_Vapp.Data;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;
using Microsoft.EntityFrameworkCore;

namespace Api_Vapp.Repositories
{
    public class ProfessionalCampaignRepository : IProfessionalCampaignRepository
    {
        private readonly Api_Context _context;

        public ProfessionalCampaignRepository(Api_Context context)
        {
            _context = context;
        }

        public async Task<ProfessionalCampaign?> GetOwnedAsync(
            int userId,
            int id,
            bool tracking = false,
            bool includeRecipients = false)
        {
            IQueryable<ProfessionalCampaign> query = _context.ProfessionalCampaigns
                .Include(c => c.Steps.Where(s => !s.IsDeleted));

            if (includeRecipients)
                query = query.Include(c => c.Recipients.Where(r => !r.IsDeleted));

            if (!tracking)
                query = query.AsNoTracking();

            return await query.FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId && !c.IsDeleted);
        }

        public async Task<(List<ProfessionalCampaign> Items, int TotalCount)> GetPagedOwnedAsync(
            int userId,
            int pageNumber,
            int pageSize)
        {
            var query = _context.ProfessionalCampaigns.AsNoTracking()
                .Include(c => c.Steps.Where(s => !s.IsDeleted))
                .Where(c => c.UserId == userId && !c.IsDeleted)
                .OrderByDescending(c => c.CreatedAt);

            var total = await query.CountAsync();
            var items = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
            return (items, total);
        }

        public async Task AddAsync(ProfessionalCampaign campaign)
        {
            await _context.ProfessionalCampaigns.AddAsync(campaign);
            await _context.SaveChangesAsync();
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
            _context.SaveChangesAsync(cancellationToken);
    }
}
