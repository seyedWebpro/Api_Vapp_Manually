using Api_Vapp.DTOs.Admin;
using Api_Vapp.DTOs.Common;
using Api_Vapp.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api_Vapp.Controller.Admin
{
    /// <summary>
    /// مدیریت بنرهای اپ موبایل در پنل ادمین
    /// </summary>
    [ApiController]
    [Route("api/Admin/[controller]")]
    [Authorize(Policy = "AdminOnly")]
    [Produces("application/json")]
    public class AppBannerController : VappControllerBase
    {
        private const long MaxBannerBodyBytes = (5L * 1024 * 1024) + (512L * 1024); // 5 MB + overhead

        private readonly IAdminAppBannerService _service;

        public AppBannerController(
            IAdminAppBannerService service,
            IConfiguration configuration,
            IUserRepository userRepository)
            : base(configuration, userRepository)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<ActionResult<ApiResponse<List<AppBannerResponseDto>>>> GetAll(
            [FromQuery] bool includeInactive = true)
        {
            var result = await _service.GetAllAsync(includeInactive);
            return StatusCode(result.StatusCode, result);
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<ApiResponse<AppBannerResponseDto>>> GetById(int id)
        {
            var result = await _service.GetByIdAsync(id);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// به‌روزرسانی بنر (multipart — تصویر فایل)
        /// </summary>
        [HttpPost("{id:int}/update")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(MaxBannerBodyBytes)]
        [RequestFormLimits(MultipartBodyLengthLimit = MaxBannerBodyBytes)]
        public async Task<ActionResult<ApiResponse<AppBannerResponseDto>>> Update(
            int id,
            [FromForm] UpdateAppBannerDto dto)
        {
            var invalid = InvalidModelStateResponse<AppBannerResponseDto>();
            if (invalid != null) return invalid;

            var result = await _service.UpdateAsync(id, dto);
            return StatusCode(result.StatusCode, result);
        }
    }
}
