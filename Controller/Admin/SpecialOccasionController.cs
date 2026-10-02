using Api_Vapp.DTOs.Admin;
using Api_Vapp.DTOs.Common;
using Api_Vapp.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api_Vapp.Controller.Admin
{
    /// <summary>
    /// مدیریت مناسبت‌های سیستمی/پیش‌فرض کاربران در پنل ادمین
    /// </summary>
    [ApiController]
    [Route("api/Admin/SpecialOccasion")]
    [Authorize(Policy = "AdminOnly")]
    [Produces("application/json")]
    public class SpecialOccasionController : VappControllerBase
    {
        private readonly IAdminSpecialOccasionService _service;

        public SpecialOccasionController(
            IAdminSpecialOccasionService service,
            IConfiguration configuration,
            IUserRepository userRepository)
            : base(configuration, userRepository)
        {
            _service = service;
        }

        /// <summary>لیست مناسبت‌های سیستمی</summary>
        [HttpGet]
        public async Task<ActionResult<ApiResponse<List<SpecialOccasionAdminResponseDto>>>> GetAll(
            [FromQuery] bool includeInactive = true)
        {
            var result = await _service.GetAllAsync(includeInactive);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>جزئیات یک مناسبت سیستمی</summary>
        [HttpGet("{id:int}")]
        public async Task<ActionResult<ApiResponse<SpecialOccasionAdminResponseDto>>> GetById(int id)
        {
            var result = await _service.GetByIdAsync(id);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>ایجاد مناسبت سیستمی جدید</summary>
        [HttpPost("create")]
        public async Task<ActionResult<ApiResponse<SpecialOccasionAdminResponseDto>>> Create(
            [FromBody] CreateSpecialOccasionAdminDto dto)
        {
            var invalid = InvalidModelStateResponse<SpecialOccasionAdminResponseDto>();
            if (invalid != null) return invalid;

            var result = await _service.CreateAsync(dto);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>ویرایش مناسبت سیستمی</summary>
        [HttpPost("{id:int}/update")]
        public async Task<ActionResult<ApiResponse<SpecialOccasionAdminResponseDto>>> Update(
            int id,
            [FromBody] UpdateSpecialOccasionAdminDto dto)
        {
            var invalid = InvalidModelStateResponse<SpecialOccasionAdminResponseDto>();
            if (invalid != null) return invalid;

            var result = await _service.UpdateAsync(id, dto);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>حذف نرم مناسبت سیستمی</summary>
        [HttpPost("{id:int}/delete")]
        public async Task<ActionResult<ApiResponse<bool>>> Delete(int id)
        {
            var result = await _service.DeleteAsync(id);
            return StatusCode(result.StatusCode, result);
        }
    }
}
