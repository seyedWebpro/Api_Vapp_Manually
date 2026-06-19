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
    public class UserSubscriptionController : VappControllerBase
    {
        private readonly IAdminUserSubscriptionService _service;

        public UserSubscriptionController(
            IAdminUserSubscriptionService service,
            IConfiguration configuration,
            IUserRepository userRepository)
            : base(configuration, userRepository)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<ActionResult<ApiResponse<List<UserSubscriptionResponseDto>>>> GetAll(
            [FromQuery] int? userId = null,
            [FromQuery] string? status = null)
        {
            var result = await _service.GetAllAsync(userId, status);
            return StatusCode(result.StatusCode, result);
        }

        [HttpPost("assign")]
        public async Task<ActionResult<ApiResponse<UserSubscriptionResponseDto>>> Assign([FromBody] AssignUserSubscriptionDto dto)
        {
            var invalid = InvalidModelStateResponse<UserSubscriptionResponseDto>();
            if (invalid != null) return invalid;

            var result = await _service.AssignAsync(dto);
            return StatusCode(result.StatusCode, result);
        }

        [HttpPost("{id:int}/cancel")]
        public async Task<ActionResult<ApiResponse<bool>>> Cancel(int id)
        {
            var result = await _service.CancelAsync(id);
            return StatusCode(result.StatusCode, result);
        }
    }
}
