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

        Task<(List<SmsSendBatchProjection> Items, int TotalCount)> GetSendBatchesAsync(int userId, SmsSendListFilterDto filter);
        Task<SmsSendBatchProjection?> GetSendBatchBySidAsync(int userId, long sid);
        Task<(List<SmsDeliveryRecord> Items, int TotalCount)> GetRecipientsBySidAsync(
            int userId, long sid, SmsSendRecipientFilterDto filter);
        Task<List<SmsDeliveryRecord>> GetAllRecipientsBySidForExportAsync(
            int userId, long sid, SmsSendRecipientFilterDto filter, int maxRows);
        Task<SmsDeliverySummaryDto> GetSummaryBySidAsync(int userId, long sid, SmsSendRecipientFilterDto? filter = null);
        Task<bool> UserOwnsSidAsync(int userId, long sid);
        Task<List<SmsDeliveryRecord>> GetSentRecordsBySidForUserAsync(int userId, long sid);
        Task<string?> GetSampleMessageTextBySidAsync(int userId, long sid);
        Task<Dictionary<long, string?>> GetSampleMessageTextsBySidsAsync(int userId, IEnumerable<long> sids);
        Task<Dictionary<int, int>> GetCampaignPartsCountsAsync(IEnumerable<int> campaignIds);
        Task<string?> ResolveCampaignMessageTextAsync(int campaignId, string mobile);
        Task<string?> ResolveDirectMessageTextAsync(int messageId);
    }
}
