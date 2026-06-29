using Api_Vapp.DTOs.Sms;
using Api_Vapp.Models;
using Api_Vapp._Utilities;

namespace Api_Vapp.Interfaces
{
    public interface ISmsDeliveryRecordRepository : IBaseRepository<SmsDeliveryRecord>
    {
        Task SaveChangesAsync();
        Task<SmsDeliveryRecord?> GetByIdAsync(int id, int userId);
        Task<(List<SmsDeliveryRecord> Items, int TotalCount)> GetByUserAsync(int userId, SmsDeliveryReportFilterDto filter);
        Task<SmsDeliverySummaryDto> GetSummaryAsync(int userId, SmsDeliveryReportFilterDto filter);
        Task<List<long>> GetDistinctPendingSidsAsync(DateTime sentBeforeUtc, int maxAttempts, int take);
        Task<List<SmsDeliveryRecord>> GetActivePendingBySidAsync(long sid, int maxAttempts);
    }
}
