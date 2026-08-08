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
    /// شماره‌جو — API موبایل برای تاریخچه، جستجوی جدید، پیشرفت، نتایج و ذخیره در دفترچه.
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

        /// <summary>تاریخچه اسکرپ‌های اخیر — صفحه اول</summary>
        [HttpGet("tasks")]
        [ProducesResponseType(typeof(ApiResponse<NumberSeekerTaskListDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<NumberSeekerTaskListDto>>> GetRecentTasks(
            [FromQuery] int limit = 20)
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _numberSeekerService.GetRecentTasksAsync(userId, limit);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>متادیتای فرم جستجوی جدید (پلتفرم + شهر + دسته + محدودیت تعداد)</summary>
        [HttpGet("form-meta")]
        [ProducesResponseType(typeof(ApiResponse<NumberSeekerFormMetaDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<NumberSeekerFormMetaDto>>> GetFormMeta()
        {
            await GetCurrentUserIdAsync();
            var result = _numberSeekerService.GetFormMeta();
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>لیست پلتفرم‌ها</summary>
        [HttpGet("sources")]
        [ProducesResponseType(typeof(ApiResponse<NumberSeekerSourcesDto>), StatusCodes.Status200OK)]
        public ActionResult<ApiResponse<NumberSeekerSourcesDto>> GetSources()
        {
            var result = _numberSeekerService.GetSources();
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>لیست شهرها برای دراپ‌داون</summary>
        [HttpGet("cities")]
        [ProducesResponseType(typeof(ApiResponse<NumberSeekerCitiesDto>), StatusCodes.Status200OK)]
        public ActionResult<ApiResponse<NumberSeekerCitiesDto>> GetCities()
        {
            var result = _numberSeekerService.GetCities();
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>پیشنهاد دسته‌ها / نوع کسب‌وکار</summary>
        [HttpGet("categories")]
        [ProducesResponseType(typeof(ApiResponse<NumberSeekerCategoriesDto>), StatusCodes.Status200OK)]
        public ActionResult<ApiResponse<NumberSeekerCategoriesDto>> GetCategories()
        {
            var result = _numberSeekerService.GetCategories();
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>شروع اسکرپ — صفحه جستجوی جدید</summary>
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

        /// <summary>Poll وضعیت — صفحه در حال جستجو / نتایج (هر ۲–۳ ثانیه)</summary>
        [HttpGet("task/{taskId}")]
        [ProducesResponseType(typeof(ApiResponse<NumberSeekerTaskStatusDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<NumberSeekerTaskStatusDto>), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ApiResponse<NumberSeekerTaskStatusDto>>> GetTaskStatus(string taskId)
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _numberSeekerService.GetTaskStatusAsync(userId, taskId);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>لغو جستجو وسط کار — صفحه در حال جستجو</summary>
        [HttpPost("task/{taskId}/cancel")]
        [ProducesResponseType(typeof(ApiResponse<NumberSeekerCancelResultDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<NumberSeekerCancelResultDto>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<NumberSeekerCancelResultDto>), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ApiResponse<NumberSeekerCancelResultDto>>> CancelTask(string taskId)
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _numberSeekerService.CancelTaskAsync(userId, taskId);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>دانلود / کپی همه شماره‌ها (JSON) — آیکن دانلود تاریخچه و دکمه کپی همه</summary>
        [HttpGet("task/{taskId}/export")]
        [ProducesResponseType(typeof(ApiResponse<NumberSeekerExportDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<NumberSeekerExportDto>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<NumberSeekerExportDto>), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ApiResponse<NumberSeekerExportDto>>> ExportPhones(string taskId)
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _numberSeekerService.ExportPhonesAsync(userId, taskId);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>دانلود فایل اکسل لیست شماره‌ها</summary>
        [HttpGet("task/{taskId}/export-excel")]
        [Produces("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "application/json")]
        [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        public async Task<ActionResult> ExportPhonesExcel(string taskId)
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _numberSeekerService.ExportPhonesToExcelAsync(userId, taskId);
            if (!result.Success || result.Data == null)
                return StatusCode(result.StatusCode, result);

            return File(result.Data.FileContent, result.Data.ContentType, result.Data.FileName);
        }

        /// <summary>ذخیره در دفترچه تلفن</summary>
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

        /// <summary>سلامت سرویس اسکرپ</summary>
        [HttpGet("health")]
        [ProducesResponseType(typeof(ApiResponse<NumberSeekerHealthDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<NumberSeekerHealthDto>>> GetHealth()
        {
            var result = await _numberSeekerService.GetHealthAsync();
            return StatusCode(result.StatusCode, result);
        }
    }
}
