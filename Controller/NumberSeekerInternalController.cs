using Api_Vapp.Attributes;
using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.NumberSeeker;
using Api_Vapp.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Api_Vapp.Controller
{
    /// <summary>
    /// endpointهای داخلی سرویس‌به‌سرویس — فقط با X-API-Key (ربات پایتون).
    /// </summary>
    [ApiController]
    [Route("api/NumberSeeker/internal")]
    [ServiceApiKey]
    [Produces("application/json")]
    public class NumberSeekerInternalController : ControllerBase
    {
        private readonly INumberSeekerService _numberSeekerService;

        public NumberSeekerInternalController(INumberSeekerService numberSeekerService)
        {
            _numberSeekerService = numberSeekerService;
        }

        /// <summary>Webhook اتمام تسک از ربات پایتون — کاهش نیاز به poll</summary>
        [HttpPost("webhook/task-completed")]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<bool>>> TaskCompleted(
            [FromBody] NumberSeekerWebhookDto webhook)
        {
            var result = await _numberSeekerService.HandleWebhookAsync(webhook);
            return StatusCode(result.StatusCode, result);
        }
    }
}
