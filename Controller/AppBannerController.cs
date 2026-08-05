using Api_Vapp.DTOs.Admin;
using Api_Vapp.DTOs.Common;
using Api_Vapp.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api_Vapp.Controller
{
    /// <summary>
    /// بنرهای فعال اپ برای کلاینت موبایل
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    [Produces("application/json")]
    public class AppBannerController : VappControllerBase
    {
        private readonly IAdminAppBannerService _service;

        public AppBannerController(
            IAdminAppBannerService service,
            IConfiguration configuration,
            IUserRepository userRepository)
            : base(configuration, userRepository)
        {
            _service = service;
        }

        /// <summary>
        /// لیست بنرهای فعال که تصویر دارند — اپ با Key فیلتر می‌کند.
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<ApiResponse<List<AppBannerResponseDto>>>> GetActiveBanners()
        {
            var result = await _service.GetActiveBannersAsync();
            return StatusCode(result.StatusCode, result);
        }
    }
}
