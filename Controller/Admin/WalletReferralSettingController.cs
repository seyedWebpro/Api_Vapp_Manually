using Api_Vapp.DTOs.Admin;
using Api_Vapp.DTOs.Common;
using Api_Vapp.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api_Vapp.Controller.Admin
{
    /// <summary>
    /// مدیریت تنظیمات سیستم معرفی (رفرال) شارژ کیف پول
    /// </summary>
    [ApiController]
    [Route("api/Admin/[controller]")]
    [Authorize(Policy = "AdminOnly")]
    [Produces("application/json")]
    public class WalletReferralSettingController : VappControllerBase
    {
        private readonly IWalletReferralService _service;

        public WalletReferralSettingController(
            IWalletReferralService service,
            IConfiguration configuration,
            IUserRepository userRepository)
            : base(configuration, userRepository)
        {
            _service = service;
        }

        /// <summary>دریافت تنظیمات فعلی سیستم معرفی</summary>
        [HttpGet]
        public async Task<ActionResult<ApiResponse<WalletReferralSettingResponseDto>>> Get()
        {
            var result = await _service.GetAdminSettingsAsync();
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>به‌روزرسانی تنظیمات (فعال/غیرفعال، درصد تخفیف، درصد پاداش)</summary>
        [HttpPost("update")]
        public async Task<ActionResult<ApiResponse<WalletReferralSettingResponseDto>>> Update(
            [FromBody] UpdateWalletReferralSettingDto dto)
        {
            var invalid = InvalidModelStateResponse<WalletReferralSettingResponseDto>();
            if (invalid != null) return invalid;

            var result = await _service.UpdateAdminSettingsAsync(dto);
            return StatusCode(result.StatusCode, result);
        }
    }
}
