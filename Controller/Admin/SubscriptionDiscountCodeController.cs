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
    public class SubscriptionDiscountCodeController : VappControllerBase
    {
        private readonly ISubscriptionDiscountService _service;

        public SubscriptionDiscountCodeController(
            ISubscriptionDiscountService service,
            IConfiguration configuration,
            IUserRepository userRepository)
            : base(configuration, userRepository)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<ActionResult<ApiResponse<List<SubscriptionDiscountCodeResponseDto>>>> GetAll(
            [FromQuery] bool includeInactive = true)
        {
            var result = await _service.GetAllAsync(includeInactive);
            return StatusCode(result.StatusCode, result);
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<ApiResponse<SubscriptionDiscountCodeResponseDto>>> GetById(int id)
        {
            var result = await _service.GetByIdAsync(id);
            return StatusCode(result.StatusCode, result);
        }

        [HttpPost]
        public async Task<ActionResult<ApiResponse<SubscriptionDiscountCodeResponseDto>>> Create(
            [FromBody] CreateSubscriptionDiscountCodeDto dto)
        {
            var invalid = InvalidModelStateResponse<SubscriptionDiscountCodeResponseDto>();
            if (invalid != null) return invalid;

            var result = await _service.CreateAsync(dto);
            return StatusCode(result.StatusCode, result);
        }

        [HttpPost("{id:int}/update")]
        public async Task<ActionResult<ApiResponse<SubscriptionDiscountCodeResponseDto>>> Update(
            int id,
            [FromBody] UpdateSubscriptionDiscountCodeDto dto)
        {
            var invalid = InvalidModelStateResponse<SubscriptionDiscountCodeResponseDto>();
            if (invalid != null) return invalid;

            var result = await _service.UpdateAsync(id, dto);
            return StatusCode(result.StatusCode, result);
        }

        [HttpPost("{id:int}/delete")]
        public async Task<ActionResult<ApiResponse<bool>>> Delete(int id)
        {
            var result = await _service.DeleteAsync(id);
            return StatusCode(result.StatusCode, result);
        }
    }
}
