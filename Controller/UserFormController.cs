using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.UserForm;
using Api_Vapp.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api_Vapp.Controller
{
    /// <summary>
    /// کنترلر مدیریت فرم‌های کاربر (فرم‌ساز)
    /// </summary>
    /// <remarks>
    /// این کنترلر برای ساخت، ویرایش، انتشار و مدیریت فرم‌های سفارشی کاربر استفاده می‌شود.
    /// قالب‌ها سمت کلاینت (Flutter) مدیریت می‌شوند و بکند فقط پیکربندی نهایی را ذخیره می‌کند.
    /// </remarks>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    [Produces("application/json")]
    public class UserFormController : VappControllerBase
    {
        private readonly IUserFormService _userFormService;

        public UserFormController(
            IUserFormService userFormService,
            IConfiguration configuration,
            IUserRepository userRepository)
            : base(configuration, userRepository)
        {
            _userFormService = userFormService;
        }

        /// <summary>
        /// ایجاد پیش‌نویس فرم
        /// </summary>
        [HttpPost]
        [ProducesResponseType(typeof(ApiResponse<UserFormResponseDto>), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ApiResponse<UserFormResponseDto>), StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<ApiResponse<UserFormResponseDto>>> CreateDraft([FromBody] CreateUserFormDto createDto)
        {
            var invalid = InvalidModelStateResponse<UserFormResponseDto>();
            if (invalid != null)
            {
                return invalid;
            }

            var userId = await GetCurrentUserIdAsync();
            var result = await _userFormService.CreateDraftAsync(userId, createDto);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// دریافت لیست فرم‌های کاربر
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(ApiResponse<UserFormListResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<UserFormListResponseDto>), StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<ApiResponse<UserFormListResponseDto>>> GetForms(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10)
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _userFormService.GetFormsAsync(userId, pageNumber, pageSize);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// دریافت جزئیات فرم
        /// </summary>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(ApiResponse<UserFormResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<UserFormResponseDto>), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ApiResponse<UserFormResponseDto>>> GetById(int id)
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _userFormService.GetByIdAsync(id, userId);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// به‌روزرسانی فرم (اطلاعات اصلی، فیلدها، دفترچه)
        /// </summary>
        [HttpPost("{id}/update")]
        [ProducesResponseType(typeof(ApiResponse<UserFormResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<UserFormResponseDto>), StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<ApiResponse<UserFormResponseDto>>> Update(int id, [FromBody] UpdateUserFormDto? updateDto)
        {
            if (updateDto == null)
            {
                return StatusCode(400, ApiResponse<UserFormResponseDto>.BadRequest(
                    "هیچ موردی برای به‌روزرسانی ارسال نشده است",
                    errorCode: ErrorCodes.ValidationFailed));
            }

            var invalid = InvalidModelStateResponse<UserFormResponseDto>();
            if (invalid != null)
            {
                return invalid;
            }

            var userId = await GetCurrentUserIdAsync();
            var result = await _userFormService.UpdateAsync(id, userId, updateDto);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// انتشار فرم و دریافت لینک عمومی
        /// </summary>
        [HttpPost("{id}/publish")]
        [ProducesResponseType(typeof(ApiResponse<UserFormResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<UserFormResponseDto>), StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<ApiResponse<UserFormResponseDto>>> Publish(int id, [FromBody] PublishUserFormDto? publishDto = null)
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _userFormService.PublishAsync(id, userId, publishDto);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// حذف نرم فرم
        /// </summary>
        [HttpPost("{id}/delete")]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<bool>>> Delete(int id)
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _userFormService.DeleteAsync(id, userId);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// فعال/غیرفعال کردن فرم منتشرشده
        /// </summary>
        [HttpPost("{id}/toggle-status")]
        [ProducesResponseType(typeof(ApiResponse<UserFormResponseDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<UserFormResponseDto>>> ToggleStatus(int id)
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _userFormService.ToggleStatusAsync(id, userId);
            return StatusCode(result.StatusCode, result);
        }
    }
}
