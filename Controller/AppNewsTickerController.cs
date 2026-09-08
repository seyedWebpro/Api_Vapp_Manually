using Api_Vapp.DTOs.Admin;
using Api_Vapp.DTOs.Common;
using Api_Vapp.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api_Vapp.Controller
{
    /// <summary>متن‌های فعال زیرنویس خبری برای اپ موبایل.</summary>
    [ApiController]
    [Route("api/NewsTicker")]
    [Authorize]
    [Produces("application/json")]
    public class AppNewsTickerController : VappControllerBase
    {
        private readonly IAppNewsTickerService _service;

        public AppNewsTickerController(
            IAppNewsTickerService service,
            IConfiguration configuration,
            IUserRepository userRepository)
            : base(configuration, userRepository)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<ActionResult<ApiResponse<List<AppNewsTickerMessageResponseDto>>>> GetActive()
        {
            var result = await _service.GetActiveAsync();
            return StatusCode(result.StatusCode, result);
        }
    }
}
