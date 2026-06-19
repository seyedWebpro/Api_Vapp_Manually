using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.Contact;
using Api_Vapp.DTOs.User;
using Api_Vapp.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api_Vapp.Controller
{
    /// <summary>
    /// کنترلر مدیریت دفترچه‌های تلفن
    /// </summary>
    /// <remarks>
    /// این کنترلر شامل تمام endpoint های مربوط به مدیریت دفترچه‌های تلفن (Contact Notebooks) می‌باشد.
    /// دفترچه‌های تلفن برای دسته‌بندی و سازماندهی مخاطبین استفاده می‌شوند.
    /// تمام endpoint های این کنترلر نیاز به احراز هویت دارند.
    /// </remarks>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    [Produces("application/json")]
    public class ContactNotebookController : VappControllerBase
    {
        private readonly IContactNotebookService _notebookService;

        public ContactNotebookController(
            IContactNotebookService notebookService,
            IConfiguration configuration,
            IUserRepository userRepository)
            : base(configuration, userRepository)
        {
            _notebookService = notebookService;
        }

        /// <summary>
        /// ایجاد دفترچه تلفن جدید
        /// </summary>
        /// <param name="createDto">اطلاعات دفترچه جدید شامل نام، توضیحات و تصویر (اختیاری)</param>
        /// <returns>پاسخ شامل اطلاعات دفترچه ایجاد شده</returns>
        /// <remarks>
        /// این endpoint برای ایجاد یک دفترچه تلفن جدید استفاده می‌شود.
        /// 
        /// **نکات مهم:**
        /// - نام دفترچه الزامی است
        /// - می‌توانید تصویر برای دفترچه آپلود کنید (اختیاری)
        /// - دفترچه به صورت پیش‌فرض فعال است
        /// - هر کاربر می‌تواند چندین دفترچه داشته باشد
        /// </remarks>
        /// <response code="200">دفترچه با موفقیت ایجاد شد</response>
        /// <response code="400">داده‌های ورودی نامعتبر است</response>
        /// <response code="500">خطای سرور</response>
        [HttpPost]
        [Consumes("multipart/form-data")]
        [ProducesResponseType(typeof(ApiResponse<ContactNotebookResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<ContactNotebookResponseDto>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<ContactNotebookResponseDto>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<ContactNotebookResponseDto>>> CreateNotebook([FromForm] CreateContactNotebookDto createDto)
        {
            if (!ModelState.IsValid)
            {
                var errors = ExtractModelStateErrors();
                return StatusCode(400, ApiResponse<ContactNotebookResponseDto>.BadRequest("داده‌های ورودی نامعتبر است", errors));
            }

            var userId = await GetCurrentUserIdAsync();
            var result = await _notebookService.CreateNotebookAsync(userId, createDto);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// دریافت اطلاعات دفترچه بر اساس شناسه
        /// </summary>
        /// <param name="id">شناسه دفترچه</param>
        /// <returns>پاسخ شامل اطلاعات کامل دفترچه</returns>
        /// <remarks>
        /// این endpoint برای دریافت اطلاعات کامل یک دفترچه بر اساس شناسه استفاده می‌شود.
        /// 
        /// **اطلاعات برگردانده شده شامل:**
        /// - اطلاعات دفترچه (نام، توضیحات، تصویر)
        /// - تعداد مخاطبین
        /// - وضعیت فعال/غیرفعال
        /// </remarks>
        /// <response code="200">اطلاعات دفترچه با موفقیت برگردانده شد</response>
        /// <response code="404">دفترچه یافت نشد</response>
        /// <response code="500">خطای سرور</response>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(ApiResponse<ContactNotebookResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<ContactNotebookResponseDto>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<ContactNotebookResponseDto>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<ContactNotebookResponseDto>>> GetNotebookById(int id)
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _notebookService.GetNotebookByIdAsync(id, userId);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// دریافت لیست دفترچه‌های تلفن با امکان جستجو و pagination
        /// </summary>
        /// <param name="pageNumber">شماره صفحه (پیش‌فرض: 1)</param>
        /// <param name="pageSize">تعداد آیتم در هر صفحه (پیش‌فرض: 10، حداکثر: 100)</param>
        /// <param name="isActive">فیلتر بر اساس وضعیت فعال/غیرفعال (اختیاری)</param>
        /// <param name="searchTerm">عبارت جستجو برای فیلتر کردن بر اساس نام (اختیاری)</param>
        /// <returns>پاسخ شامل لیست دفترچه‌ها و اطلاعات pagination</returns>
        /// <remarks>
        /// این endpoint برای دریافت لیست دفترچه‌های تلفن کاربر فعلی با امکان pagination و فیلتر استفاده می‌شود.
        /// 
        /// **نکات مهم:**
        /// - فقط دفترچه‌های کاربر فعلی برگردانده می‌شوند
        /// - جستجو بر اساس نام دفترچه انجام می‌شود
        /// - در صورت عدم ارسال فیلتر isActive، تمام دفترچه‌ها (فعال و غیرفعال) برگردانده می‌شوند
        /// </remarks>
        /// <response code="200">لیست دفترچه‌ها با موفقیت برگردانده شد</response>
        /// <response code="400">پارامترهای ورودی نامعتبر است</response>
        /// <response code="500">خطای سرور</response>
        [HttpGet]
        [ProducesResponseType(typeof(ApiResponse<ContactNotebookListResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<ContactNotebookListResponseDto>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<ContactNotebookListResponseDto>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<ContactNotebookListResponseDto>>> GetNotebooks(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] bool? isActive = null,
            [FromQuery] string? searchTerm = null)
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _notebookService.GetNotebooksAsync(userId, pageNumber, pageSize, isActive, searchTerm);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// به‌روزرسانی اطلاعات دفترچه
        /// </summary>
        /// <param name="id">شناسه دفترچه</param>
        /// <param name="updateDto">اطلاعات به‌روزرسانی</param>
        /// <returns>پاسخ شامل اطلاعات دفترچه به‌روزرسانی شده</returns>
        /// <remarks>
        /// این endpoint برای به‌روزرسانی اطلاعات یک دفترچه استفاده می‌شود.
        /// 
        /// **نکات مهم:**
        /// - فقط فیلدهای ارسال شده به‌روزرسانی می‌شوند
        /// - دفترچه باید متعلق به کاربر فعلی باشد
        /// - برای تغییر تصویر، از endpoint جداگانه استفاده کنید
        /// </remarks>
        /// <response code="200">اطلاعات دفترچه با موفقیت به‌روزرسانی شد</response>
        /// <response code="400">داده‌های ورودی نامعتبر است</response>
        /// <response code="404">دفترچه یافت نشد</response>
        /// <response code="500">خطای سرور</response>
        [HttpPost("{id}/update")]
        [ProducesResponseType(typeof(ApiResponse<ContactNotebookResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<ContactNotebookResponseDto>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<ContactNotebookResponseDto>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<ContactNotebookResponseDto>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<ContactNotebookResponseDto>>> UpdateNotebook(int id, [FromBody] UpdateContactNotebookDto updateDto)
        {
            if (!ModelState.IsValid)
            {
                var errors = ExtractModelStateErrors();
                return StatusCode(400, ApiResponse<ContactNotebookResponseDto>.BadRequest("داده‌های ورودی نامعتبر است", errors));
            }

            var userId = await GetCurrentUserIdAsync();
            var result = await _notebookService.UpdateNotebookAsync(id, userId, updateDto);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// حذف نرم دفترچه (Soft Delete)
        /// </summary>
        /// <param name="id">شناسه دفترچه</param>
        /// <returns>پاسخ شامل وضعیت حذف</returns>
        /// <remarks>
        /// این endpoint دفترچه را به صورت نرم حذف می‌کند (IsDeleted = true).
        /// 
        /// **نکات مهم:**
        /// - دفترچه از دیتابیس حذف نمی‌شود، فقط علامت IsDeleted برای آن تنظیم می‌شود
        /// - دفترچه حذف شده در لیست‌ها نمایش داده نمی‌شود
        /// - مخاطبین دفترچه حذف نمی‌شوند
        /// - می‌توانید دفترچه حذف شده را دوباره بازیابی کنید
        /// </remarks>
        /// <response code="200">دفترچه با موفقیت حذف شد</response>
        /// <response code="404">دفترچه یافت نشد</response>
        /// <response code="500">خطای سرور</response>
        [HttpPost("{id}/delete")]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<bool>>> DeleteNotebook(int id)
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _notebookService.DeleteNotebookAsync(id, userId);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// فعال/غیرفعال کردن دفترچه
        /// </summary>
        /// <param name="id">شناسه دفترچه</param>
        /// <param name="toggleActiveDto">وضعیت جدید (فعال/غیرفعال)</param>
        /// <returns>پاسخ شامل وضعیت تغییر</returns>
        /// <remarks>
        /// این endpoint برای تغییر وضعیت فعال/غیرفعال دفترچه استفاده می‌شود.
        /// 
        /// **نکات مهم:**
        /// - دفترچه غیرفعال در برخی لیست‌ها نمایش داده نمی‌شود
        /// - مخاطبین دفترچه غیرفعال همچنان قابل دسترسی هستند
        /// - این با حذف متفاوت است (دفترچه غیرفعال می‌تواند دوباره فعال شود)
        /// </remarks>
        /// <response code="200">وضعیت دفترچه با موفقیت تغییر کرد</response>
        /// <response code="400">داده‌های ورودی نامعتبر است</response>
        /// <response code="404">دفترچه یافت نشد</response>
        /// <response code="500">خطای سرور</response>
        [HttpPost("{id}/toggle-active")]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<bool>>> ToggleNotebookActiveStatus(
            int id,
            [FromBody] ToggleActiveDto toggleActiveDto)
        {
            if (!ModelState.IsValid)
            {
                var errors = ExtractModelStateErrors();
                return StatusCode(400, ApiResponse<bool>.BadRequest("داده‌های ورودی نامعتبر است", errors));
            }

            var userId = await GetCurrentUserIdAsync();
            var result = await _notebookService.ToggleNotebookActiveStatusAsync(id, userId, toggleActiveDto.IsActive);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// دریافت آمار و اطلاعات آماری دفترچه
        /// </summary>
        /// <param name="id">شناسه دفترچه</param>
        /// <returns>پاسخ شامل آمار دفترچه</returns>
        /// <remarks>
        /// این endpoint آمار و اطلاعات آماری یک دفترچه را برمی‌گرداند.
        /// 
        /// **اطلاعات آماری شامل:**
        /// - تعداد کل مخاطبین
        /// - تعداد مخاطبین فعال
        /// - تعداد مخاطبین با تگ
        /// - آمار بر اساس برند
        /// - تاریخ آخرین به‌روزرسانی
        /// 
        /// **نکات مهم:**
        /// - دفترچه باید متعلق به کاربر فعلی باشد
        /// - آمار به صورت Real-time محاسبه می‌شود
        /// </remarks>
        /// <response code="200">آمار دفترچه با موفقیت برگردانده شد</response>
        /// <response code="404">دفترچه یافت نشد</response>
        /// <response code="500">خطای سرور</response>
        [HttpGet("{id}/statistics")]
        [ProducesResponseType(typeof(ApiResponse<ContactNotebookStatisticsDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<ContactNotebookStatisticsDto>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<ContactNotebookStatisticsDto>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<ContactNotebookStatisticsDto>>> GetNotebookStatistics(int id)
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _notebookService.GetNotebookStatisticsAsync(id, userId);
            return StatusCode(result.StatusCode, result);
        }
    }
}

