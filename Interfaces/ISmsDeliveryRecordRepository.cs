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
        Task<SmsSendBatchProjection?> GetSendBatchByCampaignAsync(int userId, int campaignId);
        Task<(List<SmsDeliveryRecord> Items, int TotalCount)> GetRecipientsBySidAsync(
            int userId, long sid, SmsSendRecipientFilterDto filter);
        Task<(List<SmsDeliveryRecord> Items, int TotalCount)> GetRecipientsByCampaignAsync(
            int userId, int campaignId, SmsSendRecipientFilterDto filter);
        Task<List<SmsDeliveryRecord>> GetAllRecipientsBySidForExportAsync(
            int userId, long sid, SmsSendRecipientFilterDto filter, int maxRows);
        Task<List<SmsDeliveryRecord>> GetAllRecipientsByCampaignForExportAsync(
            int userId, int campaignId, SmsSendRecipientFilterDto filter, int maxRows);
        Task<SmsDeliverySummaryDto> GetSummaryBySidAsync(int userId, long sid, SmsSendRecipientFilterDto? filter = null);
        Task<SmsDeliverySummaryDto> GetSummaryByCampaignAsync(int userId, int campaignId, SmsSendRecipientFilterDto? filter = null);
        Task<bool> UserOwnsSidAsync(int userId, long sid);
        Task<bool> UserOwnsCampaignAsync(int userId, int campaignId);
        /// <summary>
        /// اگر Sid متعلق به کمپین پیامکی باشد، شناسه کمپین را برمی‌گرداند تا کل دسته expand شود.
        /// </summary>
        Task<int?> TryResolveCampaignIdBySidAsync(int userId, long sid);
        /// <summary>
        /// اگر Sid متعلق به ارسال گروهی (کمپین / پیام مستقیم / پیام خودکار) باشد، ماژول و شناسه موجودیت را برمی‌گرداند.
        /// </summary>
        Task<(string SourceModule, int EntityId)?> TryResolveGroupedBatchBySidAsync(int userId, long sid);
        Task<List<long>> GetDistinctSidsByCampaignAsync(int userId, int campaignId);
        Task<List<long>> GetDistinctSidsByModuleEntityAsync(int userId, string sourceModule, int entityId);
        Task<List<SmsDeliveryRecord>> GetSentRecordsBySidForUserAsync(int userId, long sid);
        Task<string?> GetSampleMessageTextBySidAsync(int userId, long sid);
        Task<string?> GetSampleMessageTextByCampaignAsync(int userId, int campaignId);
        Task<Dictionary<long, string?>> GetSampleMessageTextsBySidsAsync(int userId, IEnumerable<long> sids);
        Task<Dictionary<int, string?>> GetSampleMessageTextsByCampaignIdsAsync(int userId, IEnumerable<int> campaignIds);
        Task<Dictionary<int, int>> GetCampaignPartsCountsAsync(IEnumerable<int> campaignIds);
        Task<string?> ResolveCampaignMessageTextAsync(int campaignId, string mobile);
        Task<string?> ResolveDirectMessageTextAsync(int messageId);
    }
}
