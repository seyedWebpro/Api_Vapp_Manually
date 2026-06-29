using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.Sms;
using Api_Vapp.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api_Vapp.Controller
{
    /// <summary>
    /// گزارش یکپارچه وضعیت دلیوری پیامک — برای همه ماژول‌ها
    /// </summary>
    [ApiController]
    [Route("api/sms/delivery-reports")]
    [Authorize]
    [Produces("application/json")]
    public class SmsDeliveryReportController : VappControllerBase
    {
        private readonly ISmsDeliveryTrackingService _deliveryTrackingService;

        public SmsDeliveryReportController(
            ISmsDeliveryTrackingService deliveryTrackingService,
            IConfiguration configuration,
            IUserRepository userRepository)
            : base(configuration, userRepository)
        {
            _deliveryTrackingService = deliveryTrackingService;
        }

        /// <summary>
        /// لیست گزارش پیامک‌ها با فیلتر
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
