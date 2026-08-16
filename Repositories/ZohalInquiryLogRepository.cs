using Api_Vapp.Data;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;

namespace Api_Vapp.Repositories
{
    public sealed class ZohalInquiryLogRepository : IZohalInquiryLogRepository
    {
        private readonly Api_Context _context;

        public ZohalInquiryLogRepository(Api_Context context)
        {
            _context = context;
        }

        public async Task<ZohalInquiryLog> AddAsync(ZohalInquiryLog log, CancellationToken cancellationToken = default)
        {
            _context.ZohalInquiryLogs.Add(log);
            await _context.SaveChangesAsync(cancellationToken);
            return log;
        }
    }
}
