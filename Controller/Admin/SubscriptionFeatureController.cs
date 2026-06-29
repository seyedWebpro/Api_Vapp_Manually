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
    public class SubscriptionFeatureController : VappControllerBase
    {
        private readonly IAdminSubscriptionFeatureService _service;

        public SubscriptionFeatureController(
            IAdminSubscriptionFeatureService service,
            IConfiguration configuration,
            IUserRepository userRepository)
            : base(configuration, userRepository)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<ActionResult<ApiResponse<List<SubscriptionFeatureResponseDto>>>> GetAll([FromQuery] bool includeInactive = true)
        {
            var result = await _service.GetAllAsync(includeInactive);
            return StatusCode(result.StatusCode, result);
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<ApiResponse<SubscriptionFeatureResponseDto>>> GetById(int id)
        {
            var result = await _service.GetByIdAsync(id);
            return StatusCode(result.StatusCode, result);
        }

        [HttpPost("{id:int}/update")]
        public async Task<ActionResult<ApiResponse<SubscriptionFeatureResponseDto>>> Update(int id, [FromBody] UpdateSubscriptionFeatureDto dto)
        {
            var invalid = InvalidModelStateResponse<SubscriptionFeatureResponseDto>();
            if (invalid != null) return invalid;

            var result = await _service.UpdateAsync(id, dto);
            return StatusCode(result.StatusCode, result);
        }
    }
}
