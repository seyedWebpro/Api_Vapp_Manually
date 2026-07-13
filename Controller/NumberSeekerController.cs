using Api_Vapp.Attributes;
using Api_Vapp.Constants;
using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.NumberSeeker;
using Api_Vapp.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api_Vapp.Controller
{
    /// <summary>
    /// شماره‌جو — پروکسی امن به سرویس Python Number Scraper.
    /// موبایل فقط این API را صدا می‌زند؛ ربات از شبکه داخلی در دسترس است.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    [RequireSubscriptionFeature(SubscriptionFeatureCodes.NumberSeeker)]
    [Produces("application/json")]
    public class NumberSeekerController : VappControllerBase
    {
        private readonly INumberSeekerService _numberSeekerService;

        public NumberSeekerController(
            INumberSeekerService numberSeekerService,
            IConfiguration configuration,
            IUserRepository userRepository)
            : base(configuration, userRepository)
        {
            _numberSeekerService = numberSeekerService;
        }

        /// <summary>شروع اسکرپ شماره از پلتفرم (دیوار، شیپور، ...)</summary>
        [HttpPost("scrape")]
        [ProducesResponseType(typeof(ApiResponse<NumberSeekerTaskCreatedDto>), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ApiResponse<NumberSeekerTaskCreatedDto>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<NumberSeekerTaskCreatedDto>), StatusCodes.Status503ServiceUnavailable)]
        public async Task<ActionResult<ApiResponse<NumberSeekerTaskCreatedDto>>> StartScrape(
            [FromBody] StartNumberSeekerScrapeDto request)
        {
            var invalid = InvalidModelStateResponse<NumberSeekerTaskCreatedDto>();
            if (invalid != null) return invalid;

            var userId = await GetCurrentUserIdAsync();
            var result = await _numberSeekerService.StartScrapeAsync(userId, request);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>Poll وضعیت تسک — هر ۲–۳ ثانیه تا completed/failed</summary>
        [HttpGet("task/{taskId}")]
        [ProducesResponseType(typeof(ApiResponse<NumberSeekerTaskStatusDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<NumberSeekerTaskStatusDto>), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ApiResponse<NumberSeekerTaskStatusDto>>> GetTaskStatus(string taskId)
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _numberSeekerService.GetTaskStatusAsync(userId, taskId);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>لغو تسک در حال اجرا یا در صف</summary>
        [HttpPost("task/{taskId}/cancel")]
        [ProducesResponseType(typeof(ApiResponse<NumberSeekerCancelResultDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<NumberSeekerCancelResultDto>), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ApiResponse<NumberSeekerCancelResultDto>>> CancelTask(string taskId)
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _numberSeekerService.CancelTaskAsync(userId, taskId);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>تاریخچه تسک‌های اخیر کاربر</summary>
        [HttpGet("tasks")]
        [ProducesResponseType(typeof(ApiResponse<NumberSeekerTaskListDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<NumberSeekerTaskListDto>>> GetRecentTasks(
            [FromQuery] int limit = 20)
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _numberSeekerService.GetRecentTasksAsync(userId, limit);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>سلامت سرویس اسکرپ (برای دیباگ اپ)</summary>
        [HttpGet("health")]
        [ProducesResponseType(typeof(ApiResponse<NumberSeekerHealthDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<NumberSeekerHealthDto>>> GetHealth()
        {
            var result = await _numberSeekerService.GetHealthAsync();
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>لیست پلتفرم‌های پشتیبانی‌شده</summary>
        [HttpGet("sources")]
        [ProducesResponseType(typeof(ApiResponse<NumberSeekerSourcesDto>), StatusCodes.Status200OK)]
        public ActionResult<ApiResponse<NumberSeekerSourcesDto>> GetSources()
        {
            var result = _numberSeekerService.GetSources();
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>Import شماره‌های تسک به دفترچه تلفن</summary>
        [HttpPost("task/{taskId}/import")]
        [RequireSubscriptionFeature(SubscriptionFeatureCodes.Phonebook)]
        [ProducesResponseType(typeof(ApiResponse<NumberSeekerImportResultDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<NumberSeekerImportResultDto>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<NumberSeekerImportResultDto>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<NumberSeekerImportResultDto>), StatusCodes.Status409Conflict)]
        public async Task<ActionResult<ApiResponse<NumberSeekerImportResultDto>>> ImportPhones(
            string taskId,
            [FromBody] ImportNumberSeekerPhonesDto request)
        {
            var invalid = InvalidModelStateResponse<NumberSeekerImportResultDto>();
            if (invalid != null) return invalid;

            var userId = await GetCurrentUserIdAsync();
            var result = await _numberSeekerService.ImportPhonesAsync(userId, taskId, request);
            return StatusCode(result.StatusCode, result);
        }
    }
}
