using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.Device;
using Api_Vapp.Interfaces;
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

        public DeviceController(
            IUserDeviceService userDeviceService,
            IConfiguration configuration,
            IUserRepository userRepository)
            : base(configuration, userRepository)
        {
            _userDeviceService = userDeviceService;
        }

        /// <summary>
        /// ثبت یا به‌روزرسانی توکن FCM
        /// </summary>
        /// <remarks>
        /// بعد از لاگین، اپ موبایل توکن Firebase را با این endpoint ارسال می‌کند.
        /// ارسال دوبارهٔ همان توکن خطا نمی‌دهد و رکورد را به‌روزرسانی می‌کند (upsert).
        ///
        /// نمونه body:
        /// ```
        /// { "token": "fcm_device_token_here" }
        /// ```
        /// </remarks>
        /// <response code="200">ثبت یا به‌روزرسانی موفق</response>
        /// <response code="400">توکن خالی یا نامعتبر</response>
        /// <response code="401">بدون JWT معتبر</response>
        [HttpPost("fcm-token")]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<ApiResponse<object>>> RegisterFcmToken([FromBody] RegisterFcmTokenDto dto)
        {
            if (dto == null)
                return StatusCode(400, ApiResponse<object>.BadRequest("توکن ارسال نشده است"));

            var invalid = InvalidModelStateResponse<object>();
            if (invalid != null)
                return invalid;

            var userId = await GetCurrentUserIdAsync();
            var result = await _userDeviceService.RegisterFcmTokenAsync(userId, dto.Token);
            return StatusCode(result.StatusCode, result);
        }
    }
}
