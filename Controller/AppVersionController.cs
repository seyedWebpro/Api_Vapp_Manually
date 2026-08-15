using Api_Vapp.DTOs.AppVersion;
using Api_Vapp.DTOs.Common;
using Api_Vapp.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api_Vapp.Controller
{
    /// <summary>
    /// چک آپدیت اپ موبایل — بدون احراز هویت (Splash)
    /// </summary>
    [ApiController]
    [Route("api/AppVersion")]
    [AllowAnonymous]
    [Produces("application/json")]
    public class AppVersionController : VappControllerBase
    {
        private readonly IAppVersionService _service;

        public AppVersionController(
            IAppVersionService service,
            IConfiguration configuration,
            IUserRepository userRepository)
            : base(configuration, userRepository)
        {
            _service = service;
        }

        /// <summary>
        /// بررسی نیاز به آپدیت بر اساس پلتفرم و ورژن فعلی اپ
        /// </summary>
        [HttpGet("check")]
        [ProducesResponseType(typeof(ApiResponse<AppVersionCheckResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<AppVersionCheckResponseDto>), StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<ApiResponse<AppVersionCheckResponseDto>>> Check(
            [FromQuery] string platform,
            [FromQuery] string currentVersion)
        {
            var result = await _service.CheckAsync(platform, currentVersion);
            return StatusCode(result.StatusCode, result);
        }
    }
}
