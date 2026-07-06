using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.LuckyWheel;
using Api_Vapp.DTOs.User;
using Api_Vapp.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api_Vapp.Controller
{
    /// <summary>
    /// کنترلر مدیریت گردونه شانس
    /// </summary>
    /// <remarks>
    /// این کنترلر برای ساخت، ویرایش، انتشار و مدیریت گردونه‌های شانس کاربر استفاده می‌شود.
    /// فلو مشابه UserForm: پیش‌نویس → به‌روزرسانی جوایز → انتشار و دریافت لینک عمومی.
    /// </remarks>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    [Produces("application/json")]
    public class LuckyWheelController : VappControllerBase
    {
        private readonly ILuckyWheelService _luckyWheelService;

        public LuckyWheelController(
            ILuckyWheelService luckyWheelService,
            IConfiguration configuration,
            IUserRepository userRepository)
            : base(configuration, userRepository)
        {
            _luckyWheelService = luckyWheelService;
        }

        /// <summary>
        /// ایجاد پیش‌نویس گردونه (مرحله ۱ — اطلاعات و دفترچه)
        /// </summary>
        [HttpPost]
        [ProducesResponseType(typeof(ApiResponse<LuckyWheelResponseDto>), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ApiResponse<LuckyWheelResponseDto>), StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<ApiResponse<LuckyWheelResponseDto>>> CreateDraft([FromBody] CreateLuckyWheelDto createDto)
        {
            var invalid = InvalidModelStateResponse<LuckyWheelResponseDto>();
            if (invalid != null)
            {
                return invalid;
            }

            var userId = await GetCurrentUserIdAsync();
            var result = await _luckyWheelService.CreateDraftAsync(userId, createDto);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// دریافت لیست گردونه‌های کاربر
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(ApiResponse<LuckyWheelListResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<LuckyWheelListResponseDto>), StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<ApiResponse<LuckyWheelListResponseDto>>> GetWheels(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10)
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _luckyWheelService.GetWheelsAsync(userId, pageNumber, pageSize);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// دریافت جزئیات گردونه
        /// </summary>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(ApiResponse<LuckyWheelResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<LuckyWheelResponseDto>), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ApiResponse<LuckyWheelResponseDto>>> GetById(int id)
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _luckyWheelService.GetByIdAsync(id, userId);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// به‌روزرسانی گردونه (اطلاعات اصلی، دفترچه، جوایز)
        /// </summary>
        [HttpPost("{id}/update")]
        [ProducesResponseType(typeof(ApiResponse<LuckyWheelResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<LuckyWheelResponseDto>), StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<ApiResponse<LuckyWheelResponseDto>>> Update(int id, [FromBody] UpdateLuckyWheelDto updateDto)
        {
            var invalid = InvalidModelStateResponse<LuckyWheelResponseDto>();
            if (invalid != null)
            {
                return invalid;
            }

            var userId = await GetCurrentUserIdAsync();
            var result = await _luckyWheelService.UpdateAsync(id, userId, updateDto);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// انتشار گردونه و دریافت لینک عمومی
        /// </summary>
        [HttpPost("{id}/publish")]
        [ProducesResponseType(typeof(ApiResponse<LuckyWheelResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<LuckyWheelResponseDto>), StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<ApiResponse<LuckyWheelResponseDto>>> Publish(int id, [FromBody] PublishLuckyWheelDto? publishDto = null)
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _luckyWheelService.PublishAsync(id, userId, publishDto);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// حذف نرم گردونه
        /// </summary>
        [HttpPost("{id}/delete")]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<bool>>> Delete(int id)
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _luckyWheelService.DeleteAsync(id, userId);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// فعال/غیرفعال کردن گردونه منتشرشده (صفحه تنظیمات)
        /// </summary>
        [HttpPost("{id}/toggle-active")]
        [ProducesResponseType(typeof(ApiResponse<LuckyWheelResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<LuckyWheelResponseDto>), StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<ApiResponse<LuckyWheelResponseDto>>> SetActiveStatus(
            int id,
            [FromBody] ToggleActiveDto toggleActiveDto)
        {
            var invalid = InvalidModelStateResponse<LuckyWheelResponseDto>();
            if (invalid != null)
            {
                return invalid;
            }

            var userId = await GetCurrentUserIdAsync();
            var result = await _luckyWheelService.SetActiveStatusAsync(id, userId, toggleActiveDto.IsActive);
            return StatusCode(result.StatusCode, result);
        }
    }
}
