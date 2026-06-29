using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.Sms;

namespace Api_Vapp.Interfaces
{
    /// <summary>
    /// سرویس یکپارچه ثبت و گزارش وضعیت دلیوری پیامک
    /// </summary>
    public interface ISmsDeliveryTrackingService
    {
        Task TrackSuccessfulSendAsync(SmsDeliveryTrackRequestDto request);
        Task<ApiResponse<SmsDeliveryRecordDto>> GetByIdAsync(int userId, int id);
        Task<ApiResponse<SmsDeliveryReportListDto>> GetReportAsync(int userId, SmsDeliveryReportFilterDto filter);
        Task<ApiResponse<SmsDeliverySummaryDto>> GetSummaryAsync(int userId, SmsDeliveryReportFilterDto filter);
        Task<ApiResponse<SmsDeliveryRecordDto>> RefreshRecordAsync(int userId, int id);
        Task SyncPendingDeliveriesAsync(CancellationToken cancellationToken = default);
    }
}
