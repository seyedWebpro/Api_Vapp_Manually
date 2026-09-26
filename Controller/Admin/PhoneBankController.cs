using Api_Vapp.DTOs.Admin;
using Api_Vapp.DTOs.Common;
using Api_Vapp.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api_Vapp.Controller.Admin
{
    /// <summary>
    /// بانک آماده شماره شماره‌جو — موجودی، ایمپورت و جمع‌آوری برای ادمین
    /// </summary>
    [ApiController]
    [Route("api/Admin/[controller]")]
    [Authorize(Policy = "AdminOnly")]
    [Produces("application/json")]
    public class PhoneBankController : VappControllerBase
    {
        private readonly IAdminPhoneBankService _service;

        public PhoneBankController(
            IAdminPhoneBankService service,
            IConfiguration configuration,
            IUserRepository userRepository)
            : base(configuration, userRepository)
        {
            _service = service;
        }

        /// <summary>وضعیت بانک و آمار بر اساس شهر / دسته / منبع</summary>
        [HttpGet]
        [ProducesResponseType(typeof(ApiResponse<AdminPhoneBankOverviewDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<AdminPhoneBankOverviewDto>>> GetOverview()
        {
            var result = await _service.GetOverviewAsync();
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>شروع جمع‌آوری شماره برای پر کردن بانک</summary>
        [HttpPost("fill")]
        [ProducesResponseType(typeof(ApiResponse<AdminPhoneBankFillResultDto>), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ApiResponse<AdminPhoneBankFillResultDto>), StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<ApiResponse<AdminPhoneBankFillResultDto>>> StartFill(
            [FromBody] AdminPhoneBankFillDto request)
        {
            var invalid = InvalidModelStateResponse<AdminPhoneBankFillResultDto>();
            if (invalid != null) return invalid;

            var adminUserId = await GetCurrentUserIdAsync();
            var result = await _service.StartFillAsync(adminUserId, request);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>افزودن دستی شماره به بانک (بدون اسکرپ)</summary>
        [HttpPost("import")]
        [ProducesResponseType(typeof(ApiResponse<AdminPhoneBankImportResultDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<AdminPhoneBankImportResultDto>), StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<ApiResponse<AdminPhoneBankImportResultDto>>> Import(
            [FromBody] AdminPhoneBankImportDto request)
        {
            var invalid = InvalidModelStateResponse<AdminPhoneBankImportResultDto>();
            if (invalid != null) return invalid;

            var adminUserId = await GetCurrentUserIdAsync();
            var result = await _service.ImportPhonesAsync(adminUserId, request);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>حذف نرم شماره‌ها از بانک</summary>
        [HttpPost("delete-phones")]
        [ProducesResponseType(typeof(ApiResponse<AdminPhoneBankDeleteResultDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<AdminPhoneBankDeleteResultDto>>> DeletePhones(
            [FromBody] AdminPhoneBankDeletePhonesDto request)
        {
            var invalid = InvalidModelStateResponse<AdminPhoneBankDeleteResultDto>();
            if (invalid != null) return invalid;

            var adminUserId = await GetCurrentUserIdAsync();
            var result = await _service.DeletePhonesAsync(adminUserId, request);
            return StatusCode(result.StatusCode, result);
        }
    }
}
