using Api_Vapp.Constants;
using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.Device;
using Api_Vapp.Interfaces;
using Api_Vapp.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api_Vapp.Controller
{
    /// <summary>
    /// ثبت توکن FCM دستگاه برای Push Notification
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    [Produces("application/json")]
    public class DeviceController : VappControllerBase
    {
        private readonly IUserDeviceService _userDeviceService;
        private readonly IPushNotificationService _pushNotificationService;

        public DeviceController(
            IUserDeviceService userDeviceService,
            IPushNotificationService pushNotificationService,
            IConfiguration configuration,
            IUserRepository userRepository)
            : base(configuration, userRepository)
        {
            _userDeviceService = userDeviceService;
            _pushNotificationService = pushNotificationService;
        }

        /// <summary>
        /// ثبت یا به‌روزرسانی توکن FCM
        /// </summary>
        [HttpPost("fcm-token")]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<ApiResponse<object>>> RegisterFcmToken([FromBody] RegisterFcmTokenDto dto)
        {
            if (dto == null)
            {
                return StatusCode(400, ApiResponse<object>.BadRequest(
                    "توکن ارسال نشده است",
                    errorCode: ErrorCodes.ValidationFailed));
            }

            var invalid = InvalidModelStateResponse<object>();
            if (invalid != null)
                return invalid;

            var userId = await GetCurrentUserIdAsync();
            var result = await _userDeviceService.RegisterFcmTokenAsync(userId, dto.Token);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// ارسال نوتیفیکیشن تستی به دستگاه‌های کاربر جاری
        /// </summary>
        /// <remarks>
        /// تنظیمات پروفایل (PushEnabled و ImportantNotifications) رعایت می‌شود.
        /// </remarks>
        [HttpPost("test-push")]
        [ProducesResponseType(typeof(ApiResponse<PushDeliveryResultDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<PushDeliveryResultDto>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<PushDeliveryResultDto>), StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<ApiResponse<PushDeliveryResultDto>>> TestPush([FromBody] TestPushDto? dto)
        {
            if (!_pushNotificationService.TryInitialize())
            {
                return StatusCode(400, ApiResponse<PushDeliveryResultDto>.BadRequest(
                    ControlledErrorHelper.PushNotConfigured,
                    errorCode: ErrorCodes.PushNotConfigured));
            }

            var userId = await GetCurrentUserIdAsync();
            var title = string.IsNullOrWhiteSpace(dto?.Title) ? "تست وپ" : dto!.Title!.Trim();
            var body = string.IsNullOrWhiteSpace(dto?.Body) ? "این یک نوتیفیکیشن تستی است" : dto!.Body!.Trim();

            var delivery = await _pushNotificationService.SendToUserAsync(
                userId,
                title,
                body,
                NotificationCategory.ImportantNotifications);

            if (delivery.SkippedByPreference)
            {
                return StatusCode(400, ApiResponse<PushDeliveryResultDto>.BadRequest(
                    ControlledErrorHelper.PushDisabled,
                    errorCode: ErrorCodes.PushDisabled));
            }

            if (delivery.DeviceCount == 0)
            {
                return StatusCode(400, ApiResponse<PushDeliveryResultDto>.BadRequest(
                    ControlledErrorHelper.PushNoDevice,
                    errorCode: ErrorCodes.PushNoDevice));
            }

            if (delivery.SentCount <= 0)
            {
                return StatusCode(400, ApiResponse<PushDeliveryResultDto>.BadRequest(
                    ControlledErrorHelper.PushFailed,
                    errorCode: ErrorCodes.PushFailed));
            }

            return Ok(ApiResponse<PushDeliveryResultDto>.CreateSuccess(
                delivery,
                $"نوتیفیکیشن تستی به {delivery.SentCount} دستگاه ارسال شد"));
        }
    }
}
