using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.Contact;
using Api_Vapp.DTOs.Sms;

namespace Api_Vapp.Interfaces
{
    /// <summary>
    /// سرویس گزارش‌گیری ارسال پیامک (لیست ارسال‌ها، جزئیات، اکسل)
    /// </summary>
    public interface ISmsReportService
    {
        Task<ApiResponse<SmsReportFilterOptionsDto>> GetFilterOptionsAsync();
        Task<ApiResponse<SmsSendBatchListDto>> GetSendBatchesAsync(int userId, SmsSendListFilterDto filter);
        Task<ApiResponse<SmsSendBatchDetailDto>> GetSendBatchDetailAsync(int userId, long sid);
        Task<ApiResponse<SmsSendBatchDetailDto>> GetSendBatchDetailByCampaignAsync(int userId, int campaignId);
        Task<ApiResponse<SmsSendRecipientListDto>> GetRecipientsAsync(int userId, long sid, SmsSendRecipientFilterDto filter);
        Task<ApiResponse<SmsSendRecipientListDto>> GetRecipientsByCampaignAsync(int userId, int campaignId, SmsSendRecipientFilterDto filter);
        Task<ApiResponse<SmsMessageDetailDto>> GetMessageDetailAsync(int userId, int recordId);
        Task<ApiResponse<SmsDeliverySummaryDto>> RefreshSendBatchAsync(int userId, long sid);
        Task<ApiResponse<SmsDeliverySummaryDto>> RefreshSendBatchByCampaignAsync(int userId, int campaignId);
        Task<ApiResponse<ExportExcelResultDto>> ExportRecipientsToExcelAsync(int userId, long sid, SmsSendRecipientFilterDto filter);
        Task<ApiResponse<ExportExcelResultDto>> ExportRecipientsByCampaignToExcelAsync(int userId, int campaignId, SmsSendRecipientFilterDto filter);
    }
}
