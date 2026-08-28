using Api_Vapp.DTOs.Admin;
using Api_Vapp.DTOs.Common;
using Api_Vapp.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Api_Vapp.Controller
{
    /// <summary>
    /// پیش‌نمایش محتوای ارسال سریع با توکن کوتاه‌عمر — فقط برای ادمین (توکن از پنل ادمین صادر می‌شود).
    /// </summary>
    [ApiController]
    [Route("api/Public/QuickSendPreview")]
    [Produces("application/json")]
    public class QuickSendPreviewPublicController : ControllerBase
    {
        private readonly IQuickSendAdminPreviewService _previewService;

        public QuickSendPreviewPublicController(IQuickSendAdminPreviewService previewService)
        {
            _previewService = previewService;
        }

        /// <summary>دریافت محتوای پیش‌نمایش با توکن</summary>
        [HttpGet("{token}")]
        [ProducesResponseType(typeof(ApiResponse<QuickSendPreviewContentDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<QuickSendPreviewContentDto>), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ApiResponse<QuickSendPreviewContentDto>>> GetPreview(string token)
        {
            var result = await _previewService.GetPreviewByTokenAsync(token);
            return StatusCode(result.StatusCode, result);
        }
    }
}
