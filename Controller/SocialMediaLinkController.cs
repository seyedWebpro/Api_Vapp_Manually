using Api_Vapp.Attributes;
using Api_Vapp.Constants;
using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.Message;
using Api_Vapp.DTOs.SocialMediaLink;
using Api_Vapp.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api_Vapp.Controller
{
    /// <summary>
    /// کنترلر مدیریت لینک‌های شبکه‌های اجتماعی
    /// </summary>
    /// <remarks>
    /// CRUD لینک‌های سوشیال مدیا + تنظیم پیش‌فرض + ارسال سریع SMS به مخاطب.
    /// مسیرها مطابق قرارداد فرانت موبایل:
    /// GET/POST /api/SocialMediaLink ، GET /{id} ، POST /{id}/update|delete|set-default ، POST /quick-send
    /// </remarks>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    [RequireSubscriptionFeature(SubscriptionFeatureCodes.FreeQuickSend)]
    [Produces("application/json")]
    public class SocialMediaLinkController : VappControllerBase
    {
        private readonly ISocialMediaLinkService _socialMediaLinkService;

        public SocialMediaLinkController(
            ISocialMediaLinkService socialMediaLinkService,
            IConfiguration configuration,
            IUserRepository userRepository)
            : base(configuration, userRepository)
        {
            _socialMediaLinkService = socialMediaLinkService;
        }

        /// <summary>
        /// دریافت لیست لینک‌های شبکه‌های اجتماعی با pagination
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(ApiResponse<SocialMediaLinkListResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<SocialMediaLinkListResponseDto>), StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<ApiResponse<SocialMediaLinkListResponseDto>>> GetSocialMediaLinks(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10)
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _socialMediaLinkService.GetSocialMediaLinksAsync(userId, pageNumber, pageSize);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// دریافت جزئیات یک لینک
        /// </summary>
        [HttpGet("{id:int}")]
        [ProducesResponseType(typeof(ApiResponse<SocialMediaLinkResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<SocialMediaLinkResponseDto>), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ApiResponse<SocialMediaLinkResponseDto>>> GetSocialMediaLinkById(int id)
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _socialMediaLinkService.GetSocialMediaLinkByIdAsync(id, userId);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// ایجاد لینک جدید
        /// </summary>
        [HttpPost]
        [ProducesResponseType(typeof(ApiResponse<SocialMediaLinkResponseDto>), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ApiResponse<SocialMediaLinkResponseDto>), StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<ApiResponse<SocialMediaLinkResponseDto>>> CreateSocialMediaLink(
            [FromBody] CreateSocialMediaLinkDto? createDto)
        {
            if (createDto == null)
            {
                return StatusCode(400, ApiResponse<SocialMediaLinkResponseDto>.BadRequest(
                    "داده‌های ورودی نامعتبر است",
                    errorCode: ErrorCodes.ValidationFailed));
            }

            var invalid = InvalidModelStateResponse<SocialMediaLinkResponseDto>();
            if (invalid != null)
                return invalid;

            var userId = await GetCurrentUserIdAsync();
            var result = await _socialMediaLinkService.CreateSocialMediaLinkAsync(userId, createDto);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// به‌روزرسانی لینک
        /// </summary>
        [HttpPost("{id:int}/update")]
        [ProducesResponseType(typeof(ApiResponse<SocialMediaLinkResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<SocialMediaLinkResponseDto>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<SocialMediaLinkResponseDto>), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ApiResponse<SocialMediaLinkResponseDto>>> UpdateSocialMediaLink(
            int id,
            [FromBody] UpdateSocialMediaLinkDto? updateDto)
        {
            if (updateDto == null)
            {
                return StatusCode(400, ApiResponse<SocialMediaLinkResponseDto>.BadRequest(
                    "داده‌های ورودی نامعتبر است",
                    errorCode: ErrorCodes.ValidationFailed));
            }

            var invalid = InvalidModelStateResponse<SocialMediaLinkResponseDto>();
            if (invalid != null)
                return invalid;

            var userId = await GetCurrentUserIdAsync();
            var result = await _socialMediaLinkService.UpdateSocialMediaLinkAsync(id, userId, updateDto);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// حذف نرم لینک
        /// </summary>
        [HttpPost("{id:int}/delete")]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ApiResponse<bool>>> DeleteSocialMediaLink(int id)
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _socialMediaLinkService.DeleteSocialMediaLinkAsync(id, userId);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// تنظیم لینک به‌عنوان پیش‌فرض
        /// </summary>
        [HttpPost("{id:int}/set-default")]
        [ProducesResponseType(typeof(ApiResponse<SocialMediaLinkResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<SocialMediaLinkResponseDto>), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ApiResponse<SocialMediaLinkResponseDto>>> SetDefaultSocialMediaLink(int id)
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _socialMediaLinkService.SetUserDefaultSocialMediaLinkAsync(userId, id);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// ارسال سریع لینک مشخص به یک مخاطب (SMS)
        /// </summary>
        [HttpPost("quick-send")]
        [ProducesResponseType(typeof(ApiResponse<DirectSendResultDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<DirectSendResultDto>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<DirectSendResultDto>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<DirectSendResultDto>), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ApiResponse<DirectSendResultDto>>> QuickSendSocialMediaLink(
            [FromBody] QuickSendSocialMediaLinkDto? quickSendDto)
        {
            if (quickSendDto == null)
            {
                return StatusCode(400, ApiResponse<DirectSendResultDto>.BadRequest(
                    "داده‌های ورودی نامعتبر است",
                    errorCode: ErrorCodes.ValidationFailed));
            }

            var invalid = InvalidModelStateResponse<DirectSendResultDto>();
            if (invalid != null)
                return invalid;

            var userId = await GetCurrentUserIdAsync();
            var result = await _socialMediaLinkService.QuickSendSocialMediaLinkAsync(userId, quickSendDto);
            return StatusCode(result.StatusCode, result);
        }
    }
}
