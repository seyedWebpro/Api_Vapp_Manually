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
    public class TemplateApprovalController : VappControllerBase
    {
        private readonly IAdminTemplateApprovalService _service;

        public TemplateApprovalController(
            IAdminTemplateApprovalService service,
            IConfiguration configuration,
            IUserRepository userRepository)
            : base(configuration, userRepository)
        {
            _service = service;
        }

        [HttpGet("pending")]
        public async Task<ActionResult<ApiResponse<PagedResponse<TemplateApprovalResponseDto>>>> GetPending(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            var result = await _service.GetPendingAsync(page, pageSize);
            return StatusCode(result.StatusCode, result);
        }

        [HttpGet]
        public async Task<ActionResult<ApiResponse<PagedResponse<TemplateApprovalResponseDto>>>> GetAll(
            [FromQuery] string? status = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            var result = await _service.GetAllAsync(status, page, pageSize);
            return StatusCode(result.StatusCode, result);
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<ApiResponse<TemplateApprovalResponseDto>>> GetById(int id)
        {
            var result = await _service.GetByIdAsync(id);
            return StatusCode(result.StatusCode, result);
        }

        [HttpPost("{id:int}/approve")]
        public async Task<ActionResult<ApiResponse<bool>>> Approve(int id)
        {
            var adminUserId = await GetCurrentUserIdAsync();
            var result = await _service.ApproveAsync(id, adminUserId);
            return StatusCode(result.StatusCode, result);
        }

        [HttpPost("{id:int}/reject")]
        public async Task<ActionResult<ApiResponse<bool>>> Reject(int id, [FromBody] RejectApprovalDto dto)
        {
            var invalid = InvalidModelStateResponse<bool>();
            if (invalid != null) return invalid;

            var adminUserId = await GetCurrentUserIdAsync();
            var result = await _service.RejectAsync(id, adminUserId, dto);
            return StatusCode(result.StatusCode, result);
        }
    }
}
