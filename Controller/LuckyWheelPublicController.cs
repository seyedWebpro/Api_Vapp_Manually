using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.LuckyWheel;
using Api_Vapp.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api_Vapp.Controller
{
    /// <summary>
    /// API عمومی گردونه شانس — بدون احراز هویت
    /// </summary>
    [ApiController]
    [Route("api/LuckyWheelPublic")]
    [AllowAnonymous]
    [Produces("application/json")]
    public class LuckyWheelPublicController : VappControllerBase
    {
        private readonly ILuckyWheelPublicService _wheelPublicService;

        public LuckyWheelPublicController(
            ILuckyWheelPublicService wheelPublicService,
            IConfiguration configuration,
            IUserRepository userRepository)
            : base(configuration, userRepository)
        {
            _wheelPublicService = wheelPublicService;
        }

        /// <summary>
        /// دریافت اطلاعات گردونه منتشرشده
        /// </summary>
        [HttpGet("{slug}")]
        [ProducesResponseType(typeof(ApiResponse<LuckyWheelPublicDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<LuckyWheelPublicDto>), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ApiResponse<LuckyWheelPublicDto>>> GetWheel(string slug)
        {
            var result = await _wheelPublicService.GetPublicWheelAsync(slug);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// چرخش گردونه توسط بازدیدکننده
        /// </summary>
        [HttpPost("{slug}/spin")]
        [ProducesResponseType(typeof(ApiResponse<SpinLuckyWheelPublicResponseDto>), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ApiResponse<SpinLuckyWheelPublicResponseDto>), StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<ApiResponse<SpinLuckyWheelPublicResponseDto>>> Spin(
            string slug,
            [FromBody] SpinLuckyWheelPublicDto dto)
        {
            var invalid = InvalidModelStateResponse<SpinLuckyWheelPublicResponseDto>();
            if (invalid != null)
            {
                return invalid;
            }

            var result = await _wheelPublicService.SpinAsync(slug, dto);
            return StatusCode(result.StatusCode, result);
        }
    }
}
