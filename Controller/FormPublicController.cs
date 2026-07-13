using Api_Vapp.DTOs.Common;
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
        [ProducesResponseType(typeof(ApiResponse<FormPublicDto>), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ApiResponse<FormPublicDto>>> GetForm(string slug)
        {
            var result = await _formPublicService.GetPublicFormAsync(slug);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// ثبت پاسخ فرم توسط بازدیدکننده
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
