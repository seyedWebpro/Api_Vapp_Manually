using Api_Vapp.DTOs.Admin;
using Api_Vapp.DTOs.Common;
using Api_Vapp.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api_Vapp.Controller
{
    /// <summary>
    /// API تیکت پشتیبانی سمت کاربر
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    [Produces("application/json")]
    public class SupportTicketController : VappControllerBase
    {
        private readonly IUserSupportTicketService _service;

        public SupportTicketController(
            IUserSupportTicketService service,
            IConfiguration configuration,
            IUserRepository userRepository)
            : base(configuration, userRepository)
        {
            _service = service;
        }

        /// <summary>
        /// آمار کلی تیکت‌های کاربر (کل / در انتظار پاسخ / پاسخ‌داده‌شده / بسته‌شده)
        /// </summary>
        [HttpGet("stats")]
        public async Task<ActionResult<ApiResponse<SupportTicketStatsDto>>> GetStats()
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _service.GetMyStatsAsync(userId);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// لیست ماژول‌های قابل انتخاب برای ثبت تیکت
        /// </summary>
        [HttpGet("modules")]
        public async Task<ActionResult<ApiResponse<List<TicketModuleOptionDto>>>> GetModules()
        {
            var result = await _service.GetModulesAsync();
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// لیست تیکت‌های کاربر با فیلتر وضعیت و اولویت
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<ApiResponse<PagedResponse<SupportTicketResponseDto>>>> GetMyTickets(
            [FromQuery] string? status = null,
            [FromQuery] string? priority = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _service.GetMyTicketsAsync(userId, status, priority, page, pageSize);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// جزئیات یک تیکت به‌همراه پیام‌ها
        /// </summary>
        [HttpGet("{id:int}")]
        public async Task<ActionResult<ApiResponse<SupportTicketResponseDto>>> GetMyTicketById(int id)
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _service.GetMyTicketByIdAsync(userId, id);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// ثبت تیکت جدید (JSON)
        /// </summary>
        [HttpPost]
        [Consumes("application/json")]
        public async Task<ActionResult<ApiResponse<SupportTicketResponseDto>>> Create([FromBody] CreateSupportTicketDto dto)
        {
            var invalid = InvalidModelStateResponse<SupportTicketResponseDto>();
            if (invalid != null) return invalid;

            var userId = await GetCurrentUserIdAsync();
            var result = await _service.CreateAsync(userId, dto);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// ثبت تیکت جدید با فایل (multipart)
        /// </summary>
        [HttpPost("with-attachment")]
        [Consumes("multipart/form-data")]
        public async Task<ActionResult<ApiResponse<SupportTicketResponseDto>>> CreateWithAttachment(
            [FromForm] CreateSupportTicketFormDto formDto)
        {
            TryValidateModel(formDto);
            var invalidForm = InvalidModelStateResponse<SupportTicketResponseDto>();
            if (invalidForm != null) return invalidForm;

            var dto = new CreateSupportTicketDto
            {
                Subject = formDto.Subject,
                Module = formDto.Module,
                Content = formDto.Content,
                Priority = formDto.Priority
            };

            var userId = await GetCurrentUserIdAsync();
            var attachment = formDto.AttachmentFile is { Length: > 0 } ? formDto.AttachmentFile : null;
            var result = await _service.CreateAsync(userId, dto, attachment);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// ارسال پیام جدید در تیکت (JSON)
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
                    "متن پیام الزامی است",
                    errorCode: ErrorCodes.ValidationFailed));
            }

            var userId = await GetCurrentUserIdAsync();
            var result = await _service.ReplyAsync(userId, id, dto);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// ارسال پیام جدید با فایل (multipart)
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
                    "متن یا فایل پیام الزامی است",
                    errorCode: ErrorCodes.ValidationFailed));
            }

            var dto = new ReplySupportTicketDto { Content = content };
            var userId = await GetCurrentUserIdAsync();
            var result = await _service.ReplyAsync(userId, id, dto, attachment);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// حذف تیکت کاربر + پاک‌سازی فایل‌های آپلودشده از سرور
        /// </summary>
        [HttpPost("{id:int}/delete")]
        public async Task<ActionResult<ApiResponse<bool>>> Delete(int id)
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _service.DeleteAsync(userId, id);
            return StatusCode(result.StatusCode, result);
        }
    }
}
