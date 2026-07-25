using Api_Vapp.DTOs.Admin;
using Api_Vapp.DTOs.Common;
using Api_Vapp.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api_Vapp.Controller.Admin
{
    /// <summary>
    /// API تیکت پشتیبانی سمت ادمین
    /// </summary>
    [ApiController]
    [Route("api/Admin/[controller]")]
    [Authorize(Policy = "AdminOnly")]
    [Produces("application/json")]
    public class SupportTicketController : VappControllerBase
    {
        private readonly IAdminSupportTicketService _service;

        public SupportTicketController(
            IAdminSupportTicketService service,
            IConfiguration configuration,
            IUserRepository userRepository)
            : base(configuration, userRepository)
        {
            _service = service;
        }

        /// <summary>
        /// لیست صفحه‌بندی‌شده تیکت‌ها با فیلتر وضعیت و اولویت
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<ApiResponse<PagedResponse<SupportTicketResponseDto>>>> GetAll(
            [FromQuery] string? status = null,
            [FromQuery] string? priority = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            var result = await _service.GetAllAsync(status, priority, page, pageSize);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// جزئیات تیکت به‌همراه پیام‌ها
        /// </summary>
        [HttpGet("{id:int}")]
        public async Task<ActionResult<ApiResponse<SupportTicketResponseDto>>> GetById(int id)
        {
            var result = await _service.GetByIdAsync(id);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// پاسخ ادمین (JSON)
        /// </summary>
        [HttpPost("{id:int}/reply")]
        [Consumes("application/json")]
        public async Task<ActionResult<ApiResponse<SupportTicketResponseDto>>> Reply(
            int id,
            [FromBody] ReplySupportTicketDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Content))
            {
                return StatusCode(400, ApiResponse<SupportTicketResponseDto>.BadRequest(
                    "متن پاسخ الزامی است",
                    errorCode: ErrorCodes.ValidationFailed));
            }

            var adminUserId = await GetCurrentUserIdAsync();
            var result = await _service.ReplyAsync(id, adminUserId, dto);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// پاسخ ادمین با فایل (multipart) — سازگار با imageFile کلاینت فعلی
        /// </summary>
        [HttpPost("{id:int}/reply-with-attachment")]
        [Consumes("multipart/form-data")]
        public async Task<ActionResult<ApiResponse<SupportTicketResponseDto>>> ReplyWithAttachment(
            int id,
            [FromForm] ReplySupportTicketFormDto formDto)
        {
            var attachment = formDto.GetAttachment();
            var content = formDto.Content ?? string.Empty;

            if (string.IsNullOrWhiteSpace(content) && attachment == null)
            {
                return StatusCode(400, ApiResponse<SupportTicketResponseDto>.BadRequest(
                    "متن یا فایل پاسخ الزامی است",
                    errorCode: ErrorCodes.ValidationFailed));
            }

            var dto = new ReplySupportTicketDto { Content = content };
            var adminUserId = await GetCurrentUserIdAsync();
            var result = await _service.ReplyAsync(id, adminUserId, dto, attachment);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// تغییر وضعیت تیکت (باز / در حال بررسی / حل‌شده / بسته)
        /// </summary>
        [HttpPost("{id:int}/status")]
        public async Task<ActionResult<ApiResponse<SupportTicketResponseDto>>> UpdateStatus(
            int id,
            [FromBody] UpdateSupportTicketStatusDto dto)
        {
            var invalid = InvalidModelStateResponse<SupportTicketResponseDto>();
            if (invalid != null) return invalid;

            var result = await _service.UpdateStatusAsync(id, dto);
            return StatusCode(result.StatusCode, result);
        }
    }
}
