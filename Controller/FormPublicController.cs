using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.Public;
using Api_Vapp.DTOs.UserForm;
using Api_Vapp.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api_Vapp.Controller
{
    /// <summary>
    /// API عمومی فرم — بدون احراز هویت
    /// </summary>
    [ApiController]
    [Route("api/FormPublic")]
    [AllowAnonymous]
    [Produces("application/json")]
    public class FormPublicController : VappControllerBase
    {
        private readonly IUserFormPublicService _formPublicService;

        public FormPublicController(
            IUserFormPublicService formPublicService,
            IConfiguration configuration,
            IUserRepository userRepository)
            : base(configuration, userRepository)
        {
            _formPublicService = formPublicService;
        }

        /// <summary>
        /// دریافت schema فرم منتشرشده
        /// </summary>
        [HttpGet("{slug}")]
        [ProducesResponseType(typeof(ApiResponse<FormPublicDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<FormPublicDto>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<FormPublicDto>), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ApiResponse<FormPublicDto>>> GetForm(string slug)
        {
            var result = await _formPublicService.GetPublicFormAsync(slug);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// ثبت مشخصات تماس و ارسال کد تایید
        /// </summary>
        [HttpPost("{slug}/register")]
        [ProducesResponseType(typeof(ApiResponse<RegisterPublicParticipantResponseDto>), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ApiResponse<RegisterPublicParticipantResponseDto>), StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<ApiResponse<RegisterPublicParticipantResponseDto>>> Register(
            string slug,
            [FromBody] RegisterPublicParticipantDto dto)
        {
            var invalid = InvalidModelStateResponse<RegisterPublicParticipantResponseDto>();
            if (invalid != null)
            {
                return invalid;
            }

            var result = await _formPublicService.RegisterAsync(slug, dto);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// تأیید کد OTP شماره موبایل
        /// </summary>
        [HttpPost("{slug}/verify-otp")]
        [ProducesResponseType(typeof(ApiResponse<PublicParticipantOtpResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<PublicParticipantOtpResponseDto>), StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<ApiResponse<PublicParticipantOtpResponseDto>>> VerifyOtp(
            string slug,
            [FromBody] VerifyPublicParticipantOtpDto dto)
        {
            var invalid = InvalidModelStateResponse<PublicParticipantOtpResponseDto>();
            if (invalid != null)
            {
                return invalid;
            }

            var result = await _formPublicService.VerifyOtpAsync(slug, dto);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// ارسال مجدد کد تایید
        /// </summary>
        [HttpPost("{slug}/resend-otp")]
        [ProducesResponseType(typeof(ApiResponse<PublicParticipantOtpResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<PublicParticipantOtpResponseDto>), StatusCodes.Status429TooManyRequests)]
        public async Task<ActionResult<ApiResponse<PublicParticipantOtpResponseDto>>> ResendOtp(
            string slug,
            [FromBody] ResendPublicParticipantOtpDto dto)
        {
            var invalid = InvalidModelStateResponse<PublicParticipantOtpResponseDto>();
            if (invalid != null)
            {
                return invalid;
            }

            var result = await _formPublicService.ResendOtpAsync(slug, dto);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// ثبت پاسخ فرم توسط بازدیدکننده (نیازمند توکن و تأیید موبایل)
        /// </summary>
        [HttpPost("{slug}/submit")]
        [ProducesResponseType(typeof(ApiResponse<SubmitFormPublicResponseDto>), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ApiResponse<SubmitFormPublicResponseDto>), StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<ApiResponse<SubmitFormPublicResponseDto>>> Submit(
            string slug,
            [FromBody] SubmitFormPublicDto dto)
        {
            var invalid = InvalidModelStateResponse<SubmitFormPublicResponseDto>();
            if (invalid != null)
            {
                return invalid;
            }

            var result = await _formPublicService.SubmitFormAsync(slug, dto);
            return StatusCode(result.StatusCode, result);
        }
    }
}
