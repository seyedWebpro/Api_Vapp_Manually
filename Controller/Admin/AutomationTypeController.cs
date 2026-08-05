using Api_Vapp.DTOs.Admin;
using Api_Vapp.DTOs.Common;
using Api_Vapp.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api_Vapp.Controller.Admin
{
    /// <summary>
    /// مدیریت انواع پیام خودکار در پنل ادمین
    /// </summary>
    [ApiController]
    [Route("api/Admin/[controller]")]
    [Authorize(Policy = "AdminOnly")]
    [Produces("application/json")]
    public class AutomationTypeController : VappControllerBase
    {
        private readonly IAdminAutomationTypeService _service;

        public AutomationTypeController(
            IAdminAutomationTypeService service,
            IConfiguration configuration,
            IUserRepository userRepository)
            : base(configuration, userRepository)
        {
            _service = service;
        }

        /// <summary>
        /// دریافت همه انواع پیام خودکار
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<ApiResponse<List<AutomationTypeAdminResponseDto>>>> GetAll(
            [FromQuery] bool includeInactive = true)
        {
            var result = await _service.GetAllAsync(includeInactive);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// دریافت یک نوع پیام خودکار
        /// </summary>
        [HttpGet("{id:int}")]
        public async Task<ActionResult<ApiResponse<AutomationTypeAdminResponseDto>>> GetById(int id)
        {
            var result = await _service.GetByIdAsync(id);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// به‌روزرسانی نوع پیام خودکار (multipart — آیکون فایل)
        /// </summary>
        [HttpPost("{id:int}/update")]
        [Consumes("multipart/form-data")]
        public async Task<ActionResult<ApiResponse<AutomationTypeAdminResponseDto>>> Update(
            int id,
            [FromForm] UpdateAutomationTypeDto dto)
        {
            var invalid = InvalidModelStateResponse<AutomationTypeAdminResponseDto>();
            if (invalid != null) return invalid;

            var result = await _service.UpdateAsync(id, dto);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// حذف نرم نوع پیام خودکار
        /// </summary>
        [HttpPost("{id:int}/delete")]
        public async Task<ActionResult<ApiResponse<bool>>> Delete(int id)
        {
            var result = await _service.DeleteAsync(id);
            return StatusCode(result.StatusCode, result);
        }
    }
}
