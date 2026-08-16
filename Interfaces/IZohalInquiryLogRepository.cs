using Api_Vapp.Models;

namespace Api_Vapp.Interfaces
{
    public interface IZohalInquiryLogRepository
    {
        Task<ZohalInquiryLog> AddAsync(ZohalInquiryLog log, CancellationToken cancellationToken = default);
    }
}
