using Api_Vapp.DTOs.Admin;
using Api_Vapp.DTOs.Common;
using Api_Vapp.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api_Vapp.Controller.Admin
{
    /// <summary>
    /// مدیریت تعرفه پیامک و قواعد محاسبه پارت (فاصله، ایموجی، ظرفیت صفحات)
    /// </summary>
    [ApiController]
    [Route("api/Admin/[controller]")]
    [Authorize(Policy = "AdminOnly")]
    [Produces("application/json")]
    public class SmsPricingSettingController : VappControllerBase
    {
        private readonly ISmsPricingService _service;

        public SmsPricingSettingController(
            ISmsPricingService service,
            IConfiguration configuration,
            IUserRepository userRepository)
            : base(configuration, userRepository)
        {
            _service = service;
        }

        /// <summary>دریافت تنظیمات فعلی تعرفه پیامک</summary>
        [HttpGet]
        public async Task<ActionResult<ApiResponse<SmsPricingSettingResponseDto>>> Get()
        {
            var result = await _service.GetAdminSettingsAsync();
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>به‌روزرسانی تنظیمات تعرفه و قواعد پارت</summary>
        [HttpPost("update")]
        public async Task<ActionResult<ApiResponse<SmsPricingSettingResponseDto>>> Update(
            [FromBody] UpdateSmsPricingSettingDto dto)
        {
            var invalid = InvalidModelStateResponse<SmsPricingSettingResponseDto>();
            if (invalid != null) return invalid;

            var result = await _service.UpdateAdminSettingsAsync(dto);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>پیش‌نمایش هزینه بر اساس متن نمونه و تنظیمات ذخیره‌شده یا پیش‌نویس فرم</summary>
        [HttpPost("preview")]
        public async Task<ActionResult<ApiResponse<SmsPricingPreviewResponseDto>>> Preview(
            [FromBody] SmsPricingPreviewRequestDto dto)
        {
            var invalid = InvalidModelStateResponse<SmsPricingPreviewResponseDto>();
            if (invalid != null) return invalid;

            var result = await _service.PreviewAsync(dto);
            return StatusCode(result.StatusCode, result);
        }
    }
}
