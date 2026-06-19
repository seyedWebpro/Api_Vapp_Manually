using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.QuickAction;
using Api_Vapp.DTOs.Message;
using Api_Vapp.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace Api_Vapp.Controller
{
    /// <summary>
    /// کنترلر مدیریت لینک‌های سریع (Quick Actions)
    /// </summary>
    /// <remarks>
    /// این کنترلر شامل تمام endpoint های مربوط به مدیریت لینک‌های سریع می‌باشد.
    /// لینک‌های سریع برای دسترسی سریع به صفحات یا عملیات خاص استفاده می‌شوند.
    /// 
    /// **قابلیت‌های اصلی:**
    /// - ایجاد و مدیریت لینک‌های سریع
    /// - آپلود آیکون برای لینک‌ها
    /// - سازماندهی و دسته‌بندی لینک‌ها
    /// 
    /// تمام endpoint های این کنترلر نیاز به احراز هویت دارند.
    /// </remarks>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    [Produces("application/json")]
    public class QuickActionController : VappControllerBase
    {
        private readonly IQuickActionService _quickActionService;

        public QuickActionController(IQuickActionService quickActionService, IConfiguration configuration, IUserRepository userRepository)
            : base(configuration, userRepository)
        {
            _quickActionService = quickActionService;
        }

        /// <summary>
        /// دریافت لیست لینک‌های سریع با pagination
        /// </summary>
        /// <param name="pageNumber">شماره صفحه (پیش‌فرض: 1)</param>
        /// <param name="pageSize">تعداد در هر صفحه (پیش‌فرض: 10، حداکثر: 100)</param>
        /// <returns>پاسخ شامل لیست لینک‌های سریع و اطلاعات pagination</returns>
        /// <remarks>
        /// این endpoint لیست تمام لینک‌های سریع کاربر فعلی را با امکان pagination برمی‌گرداند.
        /// </remarks>
        /// <response code="200">لیست لینک‌های سریع با موفقیت برگردانده شد</response>
        /// <response code="400">پارامترهای ورودی نامعتبر است</response>
        /// <response code="500">خطای سرور</response>
        [HttpGet]
        [ProducesResponseType(typeof(ApiResponse<QuickActionListResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<QuickActionListResponseDto>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<QuickActionListResponseDto>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<QuickActionListResponseDto>>> GetQuickActions(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10)
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _quickActionService.GetQuickActionsAsync(userId, pageNumber, pageSize);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// دریافت اطلاعات لینک سریع بر اساس شناسه
        /// </summary>
        /// <param name="id">شناسه لینک سریع</param>
        /// <returns>پاسخ شامل اطلاعات کامل لینک سریع</returns>
        /// <remarks>
        /// این endpoint اطلاعات کامل یک لینک سریع را بر اساس شناسه برمی‌گرداند.
        /// 
        /// **اطلاعات شامل:**
        /// - عنوان و URL
        /// - آیکون
        /// - دسته‌بندی
        /// - تاریخ ایجاد و به‌روزرسانی
        /// </remarks>
        /// <response code="200">اطلاعات لینک سریع با موفقیت برگردانده شد</response>
        /// <response code="404">لینک سریع یافت نشد</response>
        /// <response code="500">خطای سرور</response>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(ApiResponse<QuickActionResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<QuickActionResponseDto>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<QuickActionResponseDto>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<QuickActionResponseDto>>> GetQuickActionById(int id)
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _quickActionService.GetQuickActionByIdAsync(id, userId);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// ایجاد لینک سریع جدید
        /// </summary>
        /// <param name="createDto">اطلاعات لینک سریع جدید شامل عنوان، URL و آیکون (اختیاری)</param>
        /// <returns>پاسخ شامل اطلاعات لینک سریع ایجاد شده</returns>
        /// <remarks>
        /// این endpoint برای ایجاد یک لینک سریع جدید استفاده می‌شود.
        /// 
        /// **اطلاعات مورد نیاز:**
        /// - عنوان (Title)
        /// - URL
        /// - آیکون (Icon) - اختیاری، می‌توانید فایل تصویر آپلود کنید
        /// 
        /// **نکات مهم:**
        /// - می‌توانید آیکون را به صورت فایل تصویر آپلود کنید
        /// - فرمت‌های مجاز: JPG, JPEG, PNG, GIF
        /// - حداکثر حجم فایل: 2 مگابایت
        /// </remarks>
        /// <response code="200">لینک سریع با موفقیت ایجاد شد</response>
        /// <response code="400">داده‌های ورودی نامعتبر است</response>
        /// <response code="500">خطای سرور</response>
        [HttpPost]
        [Consumes("multipart/form-data")]
        [ProducesResponseType(typeof(ApiResponse<QuickActionResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<QuickActionResponseDto>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<QuickActionResponseDto>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<QuickActionResponseDto>>> CreateQuickAction([FromForm] CreateQuickActionDto createDto)
        {
            if (!ModelState.IsValid)
            {
                var errors = ExtractModelStateErrors();
                return StatusCode(400, ApiResponse<QuickActionResponseDto>.BadRequest("داده‌های ورودی نامعتبر است", errors));
            }

            var userId = await GetCurrentUserIdAsync();
            var result = await _quickActionService.CreateQuickActionAsync(userId, createDto);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// به‌روزرسانی لینک سریع
        /// </summary>
        /// <param name="id">شناسه لینک سریع</param>
        /// <param name="updateDto">اطلاعات به‌روزرسانی</param>
        /// <returns>پاسخ شامل اطلاعات لینک سریع به‌روزرسانی شده</returns>
        /// <remarks>
        /// این endpoint برای به‌روزرسانی اطلاعات یک لینک سریع استفاده می‌شود.
        /// 
        /// **نکات مهم:**
        /// - فقط فیلدهای ارسال شده به‌روزرسانی می‌شوند
        /// - لینک سریع باید متعلق به کاربر فعلی باشد
        /// - می‌توانید آیکون جدید آپلود کنید
        /// </remarks>
        /// <response code="200">اطلاعات لینک سریع با موفقیت به‌روزرسانی شد</response>
        /// <response code="400">داده‌های ورودی نامعتبر است</response>
        /// <response code="404">لینک سریع یافت نشد</response>
        /// <response code="500">خطای سرور</response>
        [HttpPost("{id}/update")]
        [Consumes("multipart/form-data")]
        [ProducesResponseType(typeof(ApiResponse<QuickActionResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<QuickActionResponseDto>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<QuickActionResponseDto>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<QuickActionResponseDto>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<QuickActionResponseDto>>> UpdateQuickAction(int id, [FromForm] UpdateQuickActionDto updateDto)
        {
            if (!ModelState.IsValid)
            {
                var errors = ExtractModelStateErrors();
                return StatusCode(400, ApiResponse<QuickActionResponseDto>.BadRequest("داده‌های ورودی نامعتبر است", errors));
            }

            var userId = await GetCurrentUserIdAsync();
            var result = await _quickActionService.UpdateQuickActionAsync(id, userId, updateDto);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// حذف لینک سریع
        /// </summary>
        /// <param name="id">شناسه لینک سریع</param>
        /// <returns>پاسخ شامل وضعیت حذف</returns>
        /// <remarks>
        /// این endpoint برای حذف یک لینک سریع استفاده می‌شود.
        /// 
        /// **نکات مهم:**
        /// - لینک سریع باید متعلق به کاربر فعلی باشد
        /// - این عملیات قابل بازگشت نیست
        /// </remarks>
        /// <response code="200">لینک سریع با موفقیت حذف شد</response>
        /// <response code="404">لینک سریع یافت نشد</response>
        /// <response code="500">خطای سرور</response>
        [HttpPost("{id}/delete")]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<bool>>> DeleteQuickAction(int id)
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _quickActionService.DeleteQuickActionAsync(id, userId);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// تنظیم لینک پیش‌فرض کاربر
        /// </summary>
        /// <param name="id">شناسه لینک سریع</param>
        /// <returns>پاسخ شامل اطلاعات لینک پیش‌فرض تنظیم شده</returns>
        /// <remarks>
        /// این endpoint برای تنظیم یک لینک به عنوان لینک پیش‌فرض کاربر استفاده می‌شود.
        /// 
        /// **فرآیند:**
        /// 1. بررسی وجود لینک و تعلق آن به کاربر
        /// 2. غیرفعال کردن تمام لینک‌های پیش‌فرض قبلی کاربر
        /// 3. تنظیم لینک جدید به عنوان پیش‌فرض
        /// 
        /// **نکات مهم:**
        /// - لینک باید متعلق به کاربر فعلی باشد
        /// - لینک باید فعال باشد (IsActive = true)
        /// - با تنظیم لینک جدید، لینک پیش‌فرض قبلی به صورت خودکار غیرفعال می‌شود
        /// - فقط یک لینک می‌تواند به عنوان پیش‌فرض باشد
        /// </remarks>
        /// <response code="200">لینک پیش‌فرض با موفقیت تنظیم شد</response>
        /// <response code="400">لینک در حال استفاده توسط درخواست دیگری است</response>
        /// <response code="404">لینک یافت نشد یا متعلق به شما نیست</response>
        /// <response code="500">خطای سرور</response>
        [HttpPost("{id}/set-default")]
        [ProducesResponseType(typeof(ApiResponse<QuickActionResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<QuickActionResponseDto>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<QuickActionResponseDto>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<QuickActionResponseDto>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<QuickActionResponseDto>>> SetDefaultAction(int id)
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _quickActionService.SetUserDefaultActionAsync(userId, id);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// ارسال سریع لینک با اکشن پیش‌فرض
        /// </summary>
        /// <param name="quickSendDto">اطلاعات ارسال سریع شامل شناسه مخاطب</param>
        /// <returns>پاسخ شامل نتیجه ارسال</returns>
        /// <remarks>
        /// این endpoint برای ارسال سریع لینک با استفاده از اکشن پیش‌فرض کاربر استفاده می‌شود.
        /// 
        /// **فرآیند ارسال:**
        /// 1. بررسی وجود مخاطب و تعلق آن به کاربر
        /// 2. پیدا کردن اکشن پیش‌فرض کاربر
        /// 3. ایجاد پیام با محتوای لینک اکشن پیش‌فرض
        /// 4. ارسال مستقیم SMS به مخاطب
        /// 
        /// **نکات مهم:**
        /// - مخاطب باید متعلق به کاربر باشد
        /// - کاربر باید یک لینک پیش‌فرض تنظیم کرده باشد
        /// - محتوای SMS همان لینک انتخاب شده است
        /// - ارسال به صورت فوری انجام می‌شود (بدون نمایش خلاصه)
        /// 
        /// **استفاده:**
        /// این endpoint برای بخش "ارسال سریع لینک" استفاده می‌شود.
        /// کاربر می‌تواند با یک کلیک، لینک پیش‌فرض را برای مخاطب انتخاب شده ارسال کند.
        /// </remarks>
        /// <response code="200">لینک با موفقیت ارسال شد</response>
        /// <response code="400">داده‌های ورودی نامعتبر است یا لینک پیش‌فرض تنظیم نشده</response>
        /// <response code="403">مخاطب متعلق به شما نیست</response>
        /// <response code="404">مخاطب یافت نشد</response>
        /// <response code="500">خطای سرور</response>
        [HttpPost("quick-send")]
        [ProducesResponseType(typeof(ApiResponse<DirectSendResultDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<DirectSendResultDto>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<DirectSendResultDto>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<DirectSendResultDto>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<DirectSendResultDto>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<DirectSendResultDto>>> QuickSendAction([FromBody] QuickSendActionDto quickSendDto)
        {
            if (!ModelState.IsValid)
            {
                var errors = ExtractModelStateErrors();
                return StatusCode(400, ApiResponse<DirectSendResultDto>.BadRequest("داده‌های ورودی نامعتبر است", errors));
            }

            var userId = await GetCurrentUserIdAsync();
            var result = await _quickActionService.QuickSendActionAsync(userId, quickSendDto);
            return StatusCode(result.StatusCode, result);
        }
    }
}












