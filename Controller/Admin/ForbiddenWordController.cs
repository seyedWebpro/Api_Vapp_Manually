using Api_Vapp.DTOs.Admin;
using Api_Vapp.DTOs.Common;
using Api_Vapp.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api_Vapp.Controller.Admin
{
    /// <summary>مدیریت کلمات فیلتر شده (ممنوعه).</summary>
    [ApiController]
    [Route("api/Admin/ForbiddenWords")]
    [Authorize(Policy = "AdminOnly")]
    [Produces("application/json")]
    public class ForbiddenWordController : VappControllerBase
    {
        private readonly IForbiddenWordService _service;

        public ForbiddenWordController(
            IForbiddenWordService service,
            IConfiguration configuration,
            IUserRepository userRepository)
            : base(configuration, userRepository)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<ActionResult<ApiResponse<List<ForbiddenWordResponseDto>>>> GetAll(
            [FromQuery] bool includeInactive = true,
            [FromQuery] string? search = null)
        {
            var result = await _service.GetAllAsync(includeInactive, search);
            return StatusCode(result.StatusCode, result);
        }

        [HttpPost("create")]
        public async Task<ActionResult<ApiResponse<ForbiddenWordBulkCreateResultDto>>> Create(
            [FromBody] CreateForbiddenWordDto dto)
        {
            var invalid = InvalidModelStateResponse<ForbiddenWordBulkCreateResultDto>();
            if (invalid != null)
                return invalid;

            var result = await _service.CreateAsync(dto);
            return StatusCode(result.StatusCode, result);
        }

        [HttpPost("{id:int}/update")]
        public async Task<ActionResult<ApiResponse<ForbiddenWordResponseDto>>> Update(
            int id,
            [FromBody] UpdateForbiddenWordDto dto)
        {
            var invalid = InvalidModelStateResponse<ForbiddenWordResponseDto>();
            if (invalid != null)
                return invalid;

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
