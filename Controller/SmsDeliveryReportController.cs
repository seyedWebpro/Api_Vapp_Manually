using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.Contact;
using Api_Vapp.DTOs.Sms;
using Api_Vapp.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Api_Vapp.Attributes;
using Api_Vapp.Constants;

namespace Api_Vapp.Controller
{
    /// <summary>
    /// گزارش یکپارچه وضعیت دلیوری پیامک — لیست ارسال‌ها، جزئیات، اکسل
    /// </summary>
    [ApiController]
    [Route("api/sms/delivery-reports")]
    [Authorize]
    [RequireSubscriptionFeature(SubscriptionFeatureCodes.AdvancedAnalytics)]
    [Produces("application/json")]
    public class SmsDeliveryReportController : VappControllerBase
    {
        private readonly ISmsDeliveryTrackingService _deliveryTrackingService;
        private readonly ISmsReportService _smsReportService;

        public SmsDeliveryReportController(
            ISmsDeliveryTrackingService deliveryTrackingService,
            ISmsReportService smsReportService,
            IConfiguration configuration,
            IUserRepository userRepository)
            : base(configuration, userRepository)
        {
            _deliveryTrackingService = deliveryTrackingService;
            _smsReportService = smsReportService;
        }

        /// <summary>
        /// گزینه‌های فیلتر (نوع ارسال، بازه زمانی، وضعیت‌ها)
        /// </summary>
        [HttpGet("filter-options")]
        [ProducesResponseType(typeof(ApiResponse<SmsReportFilterOptionsDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<SmsReportFilterOptionsDto>>> GetFilterOptions()
        {
            _ = await GetCurrentUserIdAsync();
            var result = await _smsReportService.GetFilterOptionsAsync();
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// لیست ارسال‌ها (گروه‌بندی‌شده بر اساس کد ارسال / Sid)
        /// </summary>
        [HttpGet("sends")]
        [ProducesResponseType(typeof(ApiResponse<SmsSendBatchListDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<SmsSendBatchListDto>>> GetSends([FromQuery] SmsSendListFilterDto filter)
        {
            if (!ModelState.IsValid)
            {
                var errors = ExtractModelStateErrors();
                return StatusCode(400, ApiResponse<SmsSendBatchListDto>.BadRequest(
                    "داده‌های ورودی نامعتبر است",
                    errors,
                    ErrorCodes.ValidationFailed));
            }

            var userId = await GetCurrentUserIdAsync();
            var result = await _smsReportService.GetSendBatchesAsync(userId, filter);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// جزئیات یک ارسال (هدر + آمار + متن پیام)
        /// </summary>
        [HttpGet("sends/{sid:long}")]
        [ProducesResponseType(typeof(ApiResponse<SmsSendBatchDetailDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<SmsSendBatchDetailDto>), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ApiResponse<SmsSendBatchDetailDto>>> GetSendDetail(long sid)
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _smsReportService.GetSendBatchDetailAsync(userId, sid);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// لیست مخاطبین یک ارسال
        /// </summary>
        [HttpGet("sends/{sid:long}/recipients")]
        [ProducesResponseType(typeof(ApiResponse<SmsSendRecipientListDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<SmsSendRecipientListDto>>> GetSendRecipients(
            long sid,
            [FromQuery] SmsSendRecipientFilterDto filter)
        {
            if (!ModelState.IsValid)
            {
                var errors = ExtractModelStateErrors();
                return StatusCode(400, ApiResponse<SmsSendRecipientListDto>.BadRequest(
                    "داده‌های ورودی نامعتبر است",
                    errors,
                    ErrorCodes.ValidationFailed));
            }

            var userId = await GetCurrentUserIdAsync();
            var result = await _smsReportService.GetRecipientsAsync(userId, sid, filter);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// خروجی اکسل مخاطبین یک ارسال
        /// </summary>
        [HttpGet("sends/{sid:long}/recipients/export-excel")]
        [Produces("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "application/json")]
        [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<ExportExcelResultDto>), StatusCodes.Status404NotFound)]
        public async Task<ActionResult> ExportSendRecipients(long sid, [FromQuery] SmsSendRecipientFilterDto filter)
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _smsReportService.ExportRecipientsToExcelAsync(userId, sid, filter);

            if (!result.Success)
                return StatusCode(result.StatusCode, result);

            return File(
                result.Data!.FileContent,
                result.Data.ContentType,
                result.Data.FileName);
        }

        /// <summary>
        /// بروزرسانی وضعیت دلیوری یک ارسال از ایران‌نوین
        /// </summary>
        [HttpPost("sends/{sid:long}/refresh")]
        [ProducesResponseType(typeof(ApiResponse<SmsDeliverySummaryDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<SmsDeliverySummaryDto>>> RefreshSend(long sid)
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _smsReportService.RefreshSendBatchAsync(userId, sid);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// جزئیات یک پیامک (مودال مخاطب)
        /// </summary>
        [HttpGet("messages/{id:int}")]
        [ProducesResponseType(typeof(ApiResponse<SmsMessageDetailDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<SmsMessageDetailDto>), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ApiResponse<SmsMessageDetailDto>>> GetMessageDetail(int id)
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _smsReportService.GetMessageDetailAsync(userId, id);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// لیست گزارش پیامک‌ها با فیلتر (سطح رکورد — سازگاری قبلی)
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(ApiResponse<SmsDeliveryReportListDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<SmsDeliveryReportListDto>>> GetReports(
            [FromQuery] SmsDeliveryReportFilterDto filter)
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _deliveryTrackingService.GetReportAsync(userId, filter);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// خلاصه آمار وضعیت‌ها (رسیده به گوشی، ارسال به اپراتور، ...)
        /// </summary>
        [HttpGet("summary")]
        [ProducesResponseType(typeof(ApiResponse<SmsDeliverySummaryDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<SmsDeliverySummaryDto>>> GetSummary(
            [FromQuery] SmsDeliveryReportFilterDto filter)
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _deliveryTrackingService.GetSummaryAsync(userId, filter);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// جزئیات یک رکورد
        /// </summary>
        [HttpGet("{id:int}")]
        [ProducesResponseType(typeof(ApiResponse<SmsDeliveryRecordDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<SmsDeliveryRecordDto>), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ApiResponse<SmsDeliveryRecordDto>>> GetById(int id)
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _deliveryTrackingService.GetByIdAsync(userId, id);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// بروزرسانی دستی وضعیت دلیوری یک رکورد
        /// </summary>
        [HttpPost("{id:int}/refresh")]
        [ProducesResponseType(typeof(ApiResponse<SmsDeliveryRecordDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<SmsDeliveryRecordDto>>> Refresh(int id)
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _deliveryTrackingService.RefreshRecordAsync(userId, id);
            return StatusCode(result.StatusCode, result);
        }
    }
}
