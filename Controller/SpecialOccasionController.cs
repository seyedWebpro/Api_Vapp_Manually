using Api_Vapp.Attributes;
using Api_Vapp.Constants;
using Api_Vapp.DTOs.Automation;
using Api_Vapp.DTOs.Common;
using Api_Vapp.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace Api_Vapp.Controller
{
    /// <summary>
    /// جدول تبریک و تسلیت مناسبتی + مدیریت مناسبت‌های سفارشی
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    [RequireSubscriptionFeature(SubscriptionFeatureCodes.MessageAutomation)]
    public class SpecialOccasionController : VappControllerBase
    {
        private readonly ISpecialOccasionService _specialOccasionService;

        public SpecialOccasionController(
            ISpecialOccasionService specialOccasionService,
            IConfiguration configuration,
            IUserRepository userRepository)
            : base(configuration, userRepository)
        {
            _specialOccasionService = specialOccasionService;
        }

        /// <summary>
        /// دریافت جدول کامل مناسبت‌ها (سیستمی + سفارشی) با وضعیت فعال/غیرفعال و قالب
        /// </summary>
        [HttpGet("table")]
        public async Task<ActionResult<ApiResponse<OccasionTableResponseDto>>> GetOccasionTable([FromQuery] string? category = null)
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _specialOccasionService.GetOccasionTableAsync(userId, category);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// دریافت پروفایل سراسری (نام شرکت، فعال‌سازی تبریک/تسلیت، ساعت ارسال تهران)
        /// </summary>
        [HttpGet("profile")]
        public async Task<ActionResult<ApiResponse<UserOccasionProfileDto>>> GetProfile()
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _specialOccasionService.GetProfileAsync(userId);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// به‌روزرسانی پروفایل مناسبتی
        /// </summary>
        [HttpPost("profile/update")]
        public async Task<ActionResult<ApiResponse<UserOccasionProfileDto>>> UpdateProfile([FromBody] UpdateUserOccasionProfileDto dto)
        {
            var invalid = InvalidModelStateResponse<UserOccasionProfileDto>();
            if (invalid != null) return invalid;

            var userId = await GetCurrentUserIdAsync();
            var result = await _specialOccasionService.UpdateProfileAsync(userId, dto);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// فعال/غیرفعال کردن کل دسته تبریک یا تسلیت
        /// </summary>
        [HttpPost("category/toggle")]
        public async Task<ActionResult<ApiResponse<UserOccasionProfileDto>>> ToggleCategory([FromBody] ToggleOccasionCategoryDto dto)
        {
            var invalid = InvalidModelStateResponse<UserOccasionProfileDto>();
            if (invalid != null) return invalid;

            var userId = await GetCurrentUserIdAsync();
            var result = await _specialOccasionService.ToggleCategoryAsync(userId, dto);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// فعال/غیرفعال کردن یک مناسبت برای کاربر
        /// </summary>
        [HttpPost("{id:int}/toggle")]
        public async Task<ActionResult<ApiResponse<OccasionTableItemDto>>> TogglePreference(int id, [FromBody] ToggleOccasionPreferenceDto dto)
        {
            var invalid = InvalidModelStateResponse<OccasionTableItemDto>();
            if (invalid != null) return invalid;

            var userId = await GetCurrentUserIdAsync();
            var result = await _specialOccasionService.TogglePreferenceAsync(userId, id, dto);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// ویرایش کامل قالب مناسبت (ارسال به تأیید متن در صورت تغییر نسبت به پیش‌فرض ادمین)
        /// </summary>
        [HttpPost("{id:int}/template/update")]
        public async Task<ActionResult<ApiResponse<OccasionTableItemDto>>> UpdateTemplate(int id, [FromBody] UpdateOccasionTemplateDto dto)
        {
            var invalid = InvalidModelStateResponse<OccasionTableItemDto>();
            if (invalid != null) return invalid;

            var userId = await GetCurrentUserIdAsync();
            var result = await _specialOccasionService.UpdateTemplateAsync(userId, id, dto);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// بازگشت به قالب پیش‌فرض ادمین
        /// </summary>
        [HttpPost("{id:int}/template/reset")]
        public async Task<ActionResult<ApiResponse<OccasionTableItemDto>>> ResetTemplate(int id)
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _specialOccasionService.ResetTemplateAsync(userId, id);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// دریافت لیست مناسبت‌ها (سازگاری با API قبلی)
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<ApiResponse<List<SpecialOccasionResponseDto>>>> GetSpecialOccasions()
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _specialOccasionService.GetSpecialOccasionsAsync(userId);
            return StatusCode(result.StatusCode, result);
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<ApiResponse<SpecialOccasionResponseDto>>> GetSpecialOccasionById(int id)
        {
            var result = await _specialOccasionService.GetSpecialOccasionByIdAsync(id);
            return StatusCode(result.StatusCode, result);
        }

        [HttpPost]
        public async Task<ActionResult<ApiResponse<SpecialOccasionResponseDto>>> CreateSpecialOccasion([FromBody] CreateSpecialOccasionDto createDto)
        {
            var invalid = InvalidModelStateResponse<SpecialOccasionResponseDto>();
            if (invalid != null) return invalid;

            var userId = await GetCurrentUserIdAsync();
            var result = await _specialOccasionService.CreateSpecialOccasionAsync(userId, createDto);
            return StatusCode(result.StatusCode, result);
        }

        [HttpPost("{id:int}/update")]
        public async Task<ActionResult<ApiResponse<SpecialOccasionResponseDto>>> UpdateSpecialOccasion(int id, [FromBody] UpdateSpecialOccasionDto updateDto)
        {
            var invalid = InvalidModelStateResponse<SpecialOccasionResponseDto>();
            if (invalid != null) return invalid;

            var userId = await GetCurrentUserIdAsync();
            var result = await _specialOccasionService.UpdateSpecialOccasionAsync(id, userId, updateDto);
            return StatusCode(result.StatusCode, result);
        }

        [HttpPost("{id:int}/delete")]
        public async Task<ActionResult<ApiResponse<bool>>> DeleteSpecialOccasion(int id)
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _specialOccasionService.DeleteSpecialOccasionAsync(id, userId);
            return StatusCode(result.StatusCode, result);
        }
    }
}
