using Api_Vapp.DTOs.Admin;
using Api_Vapp.DTOs.Common;
using Api_Vapp.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api_Vapp.Controller
{
    /// <summary>بررسی کلمات فیلتر برای کاربران احراز هویت‌شده (قبل از ارسال به صف تأیید).</summary>
    [ApiController]
    [Route("api/ForbiddenWords")]
    [Authorize]
    [Produces("application/json")]
    public class ForbiddenWordController : VappControllerBase
    {
        private readonly IForbiddenWordService _service;

        public ForbiddenWordController(
            IForbiddenWordService service,
            IConfiguration configuration,
            IUserRepository userRepository)
            : base(configuration, userRepository)
        {
            _service = service;
        }

        /// <summary>فهرست کلمات فیلتر فعال — برای اعتبارسنجی فوری در اپ.</summary>
        [HttpGet("active")]
        public async Task<ActionResult<ApiResponse<List<string>>>> GetActive()
        {
            var result = await _service.GetActiveWordsAsync();
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>اعتبارسنجی یک متن؛ در صورت یافتن کلمه فیلتر، فرایند نباید ادامه یابد.</summary>
        [HttpPost("validate")]
        public async Task<ActionResult<ApiResponse<ForbiddenWordValidateResultDto>>> Validate(
            [FromBody] ForbiddenWordValidateRequestDto dto)
        {
            var invalid = InvalidModelStateResponse<ForbiddenWordValidateResultDto>();
            if (invalid != null)
                return invalid;

            var result = await _service.ValidateTextAsync(dto);
            return StatusCode(result.StatusCode, result);
        }
    }
}
