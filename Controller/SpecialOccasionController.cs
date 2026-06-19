using Api_Vapp.DTOs.Automation;
using Api_Vapp.DTOs.Common;
using Api_Vapp.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System.Security.Claims;

namespace Api_Vapp.Controller
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class SpecialOccasionController : ControllerBase
    {
        private readonly ISpecialOccasionService _specialOccasionService;
        private readonly IConfiguration _configuration;
        private readonly IUserRepository _userRepository;

        public SpecialOccasionController(ISpecialOccasionService specialOccasionService, IConfiguration configuration, IUserRepository userRepository)
        {
            _specialOccasionService = specialOccasionService;
            _configuration = configuration;
            _userRepository = userRepository;
        }

        /// <summary>
        /// استخراج خطاهای ModelState برای نمایش به کاربر
        /// </summary>
        private List<string> ExtractModelStateErrors() =>
            Api_Vapp.Utilities.ErrorTranslator.ExtractModelStateErrors(ModelState);

        /// <summary>
        /// دریافت شناسه کاربر از JWT Token یا برگرداندن کاربر پیش‌فرض در حالت DisableAuth
        /// </summary>
        private async Task<int?> GetCurrentUserIdAsync()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(userIdClaim) && int.TryParse(userIdClaim, out int userId))
            {
                return userId;
            }

            var disableAuth = _configuration.GetValue<bool>("Development:DisableAuth", false);
            if (disableAuth)
            {
                var defaultUser = await _userRepository.GetOrCreateDefaultUserAsync();
                return defaultUser.Id;
            }

            return null;
        }

        /// <summary>
        /// دریافت لیست مناسبت‌ها
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<ApiResponse<List<SpecialOccasionResponseDto>>>> GetSpecialOccasions()
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _specialOccasionService.GetSpecialOccasionsAsync(userId);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// دریافت مناسبت با شناسه
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<ApiResponse<SpecialOccasionResponseDto>>> GetSpecialOccasionById(int id)
        {
            var result = await _specialOccasionService.GetSpecialOccasionByIdAsync(id);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// ایجاد مناسبت جدید
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<ApiResponse<SpecialOccasionResponseDto>>> CreateSpecialOccasion([FromBody] CreateSpecialOccasionDto createDto)
        {
            if (!ModelState.IsValid)
            {
                var errors = ExtractModelStateErrors();
                return StatusCode(400, ApiResponse<SpecialOccasionResponseDto>.BadRequest("داده‌های ورودی نامعتبر است", errors));
            }

            var userId = await GetCurrentUserIdAsync();
            if (!userId.HasValue)
            {
                return StatusCode(401, ApiResponse<SpecialOccasionResponseDto>.Unauthorized("شناسه کاربر معتبر نیست"));
            }

            var result = await _specialOccasionService.CreateSpecialOccasionAsync(userId.Value, createDto);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// به‌روزرسانی مناسبت
        /// </summary>
        [HttpPost("{id}/update")]
        public async Task<ActionResult<ApiResponse<SpecialOccasionResponseDto>>> UpdateSpecialOccasion(int id, [FromBody] UpdateSpecialOccasionDto updateDto)
        {
            if (!ModelState.IsValid)
            {
                var errors = ExtractModelStateErrors();
                return StatusCode(400, ApiResponse<SpecialOccasionResponseDto>.BadRequest("داده‌های ورودی نامعتبر است", errors));
            }

            var userId = await GetCurrentUserIdAsync();
            var result = await _specialOccasionService.UpdateSpecialOccasionAsync(id, userId, updateDto);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// حذف مناسبت
        /// </summary>
        [HttpPost("{id}/delete")]
        public async Task<ActionResult<ApiResponse<bool>>> DeleteSpecialOccasion(int id)
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _specialOccasionService.DeleteSpecialOccasionAsync(id, userId);
            return StatusCode(result.StatusCode, result);
        }
    }
}

