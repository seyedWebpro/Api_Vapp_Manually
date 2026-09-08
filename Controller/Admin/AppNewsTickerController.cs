using Api_Vapp.DTOs.Admin;
using Api_Vapp.DTOs.Common;
using Api_Vapp.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api_Vapp.Controller.Admin
{
    /// <summary>مدیریت متن‌های زیرنویس خبری اپ.</summary>
    [ApiController]
    [Route("api/Admin/NewsTicker")]
    [Authorize(Policy = "AdminOnly")]
    [Produces("application/json")]
    public class AppNewsTickerController : VappControllerBase
    {
        private readonly IAppNewsTickerService _service;

        public AppNewsTickerController(
            IAppNewsTickerService service,
            IConfiguration configuration,
            IUserRepository userRepository)
            : base(configuration, userRepository)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<ActionResult<ApiResponse<List<AppNewsTickerMessageResponseDto>>>> GetAll(
            [FromQuery] bool includeInactive = true)
        {
            var result = await _service.GetAllAsync(includeInactive);
            return StatusCode(result.StatusCode, result);
        }

        [HttpPost("create")]
        public async Task<ActionResult<ApiResponse<AppNewsTickerMessageResponseDto>>> Create(
            [FromBody] CreateAppNewsTickerMessageDto dto)
        {
            var result = await _service.CreateAsync(dto);
            return StatusCode(result.StatusCode, result);
        }

        [HttpPost("{id:int}/update")]
        public async Task<ActionResult<ApiResponse<AppNewsTickerMessageResponseDto>>> Update(
            int id,
            [FromBody] UpdateAppNewsTickerMessageDto dto)
        {
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
