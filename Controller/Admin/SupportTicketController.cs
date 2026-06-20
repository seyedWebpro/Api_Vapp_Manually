using Api_Vapp.DTOs.Admin;
using Api_Vapp.DTOs.Common;
using Api_Vapp.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api_Vapp.Controller.Admin
{
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

        [HttpGet]
        public async Task<ActionResult<ApiResponse<PagedResponse<SupportTicketResponseDto>>>> GetAll(
            [FromQuery] string? status = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            var result = await _service.GetAllAsync(status, page, pageSize);
            return StatusCode(result.StatusCode, result);
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<ApiResponse<SupportTicketResponseDto>>> GetById(int id)
        {
            var result = await _service.GetByIdAsync(id);
            return StatusCode(result.StatusCode, result);
        }

        [HttpPost("{id:int}/reply")]
        [Consumes("application/json", "multipart/form-data")]
        public async Task<ActionResult<ApiResponse<SupportTicketResponseDto>>> Reply(
            int id,
            [FromForm] ReplySupportTicketFormDto? formDto,
            [FromBody] ReplySupportTicketDto? jsonDto)
        {
            var dto = new ReplySupportTicketDto
            {
                Content = formDto?.Content ?? jsonDto?.Content ?? string.Empty
            };

            if (string.IsNullOrWhiteSpace(dto.Content) && (formDto?.ImageFile == null || formDto.ImageFile.Length == 0))
            {
                return StatusCode(400, ApiResponse<SupportTicketResponseDto>.BadRequest("متن یا تصویر پاسخ الزامی است"));
            }

            var adminUserId = await GetCurrentUserIdAsync();
            var result = await _service.ReplyAsync(id, adminUserId, dto, formDto?.ImageFile);
            return StatusCode(result.StatusCode, result);
        }

        [HttpPost("{id:int}/status")]
        public async Task<ActionResult<ApiResponse<SupportTicketResponseDto>>> UpdateStatus(int id, [FromBody] UpdateSupportTicketStatusDto dto)
        {
            var invalid = InvalidModelStateResponse<SupportTicketResponseDto>();
            if (invalid != null) return invalid;

            var result = await _service.UpdateStatusAsync(id, dto);
            return StatusCode(result.StatusCode, result);
        }
    }
}
