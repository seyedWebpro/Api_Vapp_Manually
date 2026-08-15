using Api_Vapp.DTOs.AppVersion;
using Api_Vapp.DTOs.Common;
using Api_Vapp.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api_Vapp.Controller.Admin
{
    /// <summary>
    /// مدیریت سیاست نسخه اپ موبایل (android / ios)
    /// </summary>
    [ApiController]
    [Route("api/Admin/AppVersion")]
    [Authorize(Policy = "AdminOnly")]
    [Produces("application/json")]
    public class AppVersionPolicyController : VappControllerBase
    {
        private readonly IAppVersionService _service;

        public AppVersionPolicyController(
            IAppVersionService service,
            IConfiguration configuration,
            IUserRepository userRepository)
            : base(configuration, userRepository)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<ActionResult<ApiResponse<List<AppVersionPolicyResponseDto>>>> GetAll()
        {
            var result = await _service.GetAllPoliciesAsync();
            return StatusCode(result.StatusCode, result);
        }

        [HttpGet("{platform}")]
        public async Task<ActionResult<ApiResponse<AppVersionPolicyResponseDto>>> GetByPlatform(string platform)
        {
            var result = await _service.GetPolicyByPlatformAsync(platform);
            return StatusCode(result.StatusCode, result);
        }

        [HttpPost("{platform}/update")]
        public async Task<ActionResult<ApiResponse<AppVersionPolicyResponseDto>>> Update(
            string platform,
            [FromBody] UpdateAppVersionPolicyDto dto)
        {
            var invalid = InvalidModelStateResponse<AppVersionPolicyResponseDto>();
            if (invalid != null) return invalid;

            var result = await _service.UpdatePolicyAsync(platform, dto);
            return StatusCode(result.StatusCode, result);
        }
    }
}
