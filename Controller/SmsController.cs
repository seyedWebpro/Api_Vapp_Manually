using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.Sms;
using Api_Vapp.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api_Vapp.Controller
{
    /// <summary>
    /// کنترلر مدیریت ارسال پیامک (ادمین) — منطق ارسال/tracking در SmsService
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    [Produces("application/json")]
    public class SmsController : VappControllerBase
    {
        private readonly ISmsService _smsService;

        public SmsController(
            ISmsService smsService,
            IConfiguration configuration,
            IUserRepository userRepository)
            : base(configuration, userRepository)
        {
            _smsService = smsService;
        }

        [HttpPost("send")]
        [Authorize(Policy = "AdminOnly")]
        [ProducesResponseType(typeof(ApiResponse<SendSmsResponseDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<SendSmsResponseDto>>> SendSms([FromBody] SendSmsRequestDto request)
        {
            var invalid = InvalidModelStateResponse<SendSmsResponseDto>();
            if (invalid != null) return invalid;

            var userId = await GetCurrentUserIdAsync();
            var result = await _smsService.SendManualSmsAsync(userId, request);
            return StatusCode(result.StatusCode, result);
        }

        [HttpPost("send-bulk")]
        [Authorize(Policy = "AdminOnly")]
        [ProducesResponseType(typeof(ApiResponse<SendBulkResponseDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<SendBulkResponseDto>>> SendBulkSms([FromBody] SendBulkRequestDto request)
        {
            var invalid = InvalidModelStateResponse<SendBulkResponseDto>();
            if (invalid != null) return invalid;

            var userId = await GetCurrentUserIdAsync();
            var result = await _smsService.SendManualBulkSmsAsync(userId, request);
            return StatusCode(result.StatusCode, result);
        }

        [HttpPost("send-array")]
        [Authorize(Policy = "AdminOnly")]
        [ProducesResponseType(typeof(ApiResponse<SendArrayResponseDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<SendArrayResponseDto>>> SendArraySms([FromBody] SendArrayRequestDto request)
        {
            var invalid = InvalidModelStateResponse<SendArrayResponseDto>();
            if (invalid != null) return invalid;

            var userId = await GetCurrentUserIdAsync();
            var result = await _smsService.SendManualArraySmsAsync(userId, request);
            return StatusCode(result.StatusCode, result);
        }

        [HttpPost("delivery")]
        [ProducesResponseType(typeof(ApiResponse<DeliveryResponseDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<DeliveryResponseDto>>> GetDeliveryStatus([FromBody] DeliveryRequestDto request)
        {
            var invalid = InvalidModelStateResponse<DeliveryResponseDto>();
            if (invalid != null) return invalid;

            _ = await GetCurrentUserIdAsync();
            var result = await _smsService.GetDeliveryStatusAsync(request.Sid);
            return StatusCode(result.StatusCode, result);
        }

        [HttpGet("delivery/{sid}")]
        [ProducesResponseType(typeof(ApiResponse<DeliveryResponseDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<DeliveryResponseDto>>> GetDeliveryStatusBySid(long sid)
        {
            _ = await GetCurrentUserIdAsync();
            var result = await _smsService.GetDeliveryStatusAsync(sid);
            return StatusCode(result.StatusCode, result);
        }

        [HttpPost("inbox")]
        [ProducesResponseType(typeof(ApiResponse<InboxResponseDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<InboxResponseDto>>> GetInbox([FromBody] InboxRequestDto request)
        {
            var invalid = InvalidModelStateResponse<InboxResponseDto>();
            if (invalid != null) return invalid;

            _ = await GetCurrentUserIdAsync();
            var result = await _smsService.GetInboxAsync(request);
            return StatusCode(result.StatusCode, result);
        }

        [HttpGet("info")]
        [ProducesResponseType(typeof(ApiResponse<InfoResponseDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<InfoResponseDto>>> GetWalletInfo()
        {
            _ = await GetCurrentUserIdAsync();
            var result = await _smsService.GetWalletInfoAsync();
            return StatusCode(result.StatusCode, result);
        }
    }
}
