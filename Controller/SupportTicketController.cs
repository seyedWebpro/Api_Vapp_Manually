using Api_Vapp.DTOs.Admin;
using Api_Vapp.DTOs.Common;
using Api_Vapp.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api_Vapp.Controller
{
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

        [HttpPost]
        public async Task<ActionResult<ApiResponse<SupportTicketResponseDto>>> Create([FromBody] CreateSupportTicketDto dto)
        {
            var invalid = InvalidModelStateResponse<SupportTicketResponseDto>();
            if (invalid != null) return invalid;

            var userId = await GetCurrentUserIdAsync();
            var result = await _service.CreateAsync(userId, dto);
            return StatusCode(result.StatusCode, result);
        }

        [HttpGet]
        public async Task<ActionResult<ApiResponse<List<SupportTicketResponseDto>>>> GetMyTickets()
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _service.GetMyTicketsAsync(userId);
            return StatusCode(result.StatusCode, result);
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<ApiResponse<SupportTicketResponseDto>>> GetMyTicketById(int id)
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _service.GetMyTicketByIdAsync(userId, id);
            return StatusCode(result.StatusCode, result);
        }

        [HttpPost("{id:int}/reply")]
        public async Task<ActionResult<ApiResponse<SupportTicketResponseDto>>> Reply(int id, [FromBody] ReplySupportTicketDto dto)
        {
            var invalid = InvalidModelStateResponse<SupportTicketResponseDto>();
            if (invalid != null) return invalid;

            var userId = await GetCurrentUserIdAsync();
            var result = await _service.ReplyAsync(userId, id, dto);
            return StatusCode(result.StatusCode, result);
        }
    }
}
