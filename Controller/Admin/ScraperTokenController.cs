using Api_Vapp.DTOs.Admin;
using Api_Vapp.DTOs.Common;
using Api_Vapp.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api_Vapp.Controller.Admin
{
    /// <summary>
    /// مدیریت توکن‌های اسکرپر (دیوار / شیپور) برای ادمین
    /// </summary>
    [ApiController]
    [Route("api/Admin/[controller]")]
    [Authorize(Policy = "AdminOnly")]
    [Produces("application/json")]
    public class ScraperTokenController : VappControllerBase
    {
        private readonly IAdminScraperTokenService _service;

        public ScraperTokenController(
            IAdminScraperTokenService service,
            IConfiguration configuration,
            IUserRepository userRepository)
            : base(configuration, userRepository)
        {
            _service = service;
        }

        /// <summary>وضعیت توکن‌ها و هشدارها</summary>
        [HttpGet]
        public async Task<ActionResult<ApiResponse<AdminScraperTokensOverviewDto>>> GetOverview()
        {
            var result = await _service.GetOverviewAsync();
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>ذخیره توکن دیوار</summary>
        [HttpPost("divar")]
        public async Task<ActionResult<ApiResponse<AdminScraperTokenSaveResultDto>>> SaveDivar(
            [FromBody] SaveDivarTokenDto dto)
        {
            var invalid = InvalidModelStateResponse<AdminScraperTokenSaveResultDto>();
            if (invalid != null) return invalid;

            var result = await _service.SaveDivarAsync(dto);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>ذخیره توکن شیپور</summary>
        [HttpPost("sheypoor")]
        public async Task<ActionResult<ApiResponse<AdminScraperTokenSaveResultDto>>> SaveSheypoor(
            [FromBody] SaveSheypoorTokenDto dto)
        {
            var invalid = InvalidModelStateResponse<AdminScraperTokenSaveResultDto>();
            if (invalid != null) return invalid;

            var result = await _service.SaveSheypoorAsync(dto);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>اجرای نگهداری / تمدید خودکار توکن‌ها</summary>
        [HttpPost("maintenance")]
        public async Task<ActionResult<ApiResponse<AdminScraperTokenMaintenanceDto>>> RunMaintenance(
            [FromQuery] bool forceSheypoorRefresh = false,
            [FromQuery] bool forceDivarRefresh = false)
        {
            var result = await _service.RunMaintenanceAsync(forceSheypoorRefresh, forceDivarRefresh);
            return StatusCode(result.StatusCode, result);
        }
    }
}
