using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.Message;
using Api_Vapp.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System.Security.Claims;

namespace Api_Vapp.Controller
{
    /// <summary>
    /// کنترلر مدیریت قالب‌های پیام و گروه‌های قالب
    /// </summary>
    /// <remarks>
    /// این کنترلر شامل تمام endpoint های مربوط به مدیریت قالب‌های پیام و گروه‌های قالب می‌باشد.
    /// 
    /// **قابلیت‌های اصلی:**
    /// - ایجاد و مدیریت قالب‌های پیام
    /// - ایجاد و مدیریت گروه‌های قالب
    /// - دسته‌بندی قالب‌ها
    /// - استفاده از قالب‌ها در پیام‌ها و کمپین‌ها
    /// 
    /// **نکات مهم:**
    /// - قالب‌ها برای استفاده مجدد در پیام‌ها طراحی شده‌اند
    /// - می‌توانید قالب‌ها را در گروه‌ها دسته‌بندی کنید
    /// - قالب‌ها می‌توانند شامل Placeholder ها باشند
    /// 
    /// تمام endpoint های این کنترلر نیاز به احراز هویت دارند.
    /// </remarks>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    [Produces("application/json")]
    public class TemplateController : VappControllerBase
    {
        private readonly IMessageService _messageService;

        public TemplateController(IMessageService messageService, IConfiguration configuration, IUserRepository userRepository)
            : base(configuration, userRepository)
        {
            _messageService = messageService;
        }

        #region Template Operations

        /// <summary>
        /// ایجاد قالب پیام جدید
        /// </summary>
        /// <param name="createDto">اطلاعات قالب جدید شامل محتوا، عنوان و دسته‌بندی</param>
        /// <returns>پاسخ شامل اطلاعات قالب ایجاد شده</returns>
        /// <remarks>
        /// این endpoint برای ایجاد یک قالب پیام جدید استفاده می‌شود.
        /// 
        /// **نکات مهم:**
        /// - می‌توانید از Placeholder ها برای شخصی‌سازی استفاده کنید
        /// - می‌توانید قالب را در یک گروه دسته‌بندی کنید
        /// - قالب ایجاد شده می‌تواند در پیام‌ها و کمپین‌ها استفاده شود
        /// - می‌توانید فایل آیکون را به صورت اختیاری آپلود کنید (IconFile)
        /// - در صورت آپلود فایل، مسیر فایل در فیلد Icon ذخیره می‌شود
        /// </remarks>
        /// <response code="200">قالب با موفقیت ایجاد شد</response>
        /// <response code="400">داده‌های ورودی نامعتبر است</response>
        /// <response code="500">خطای سرور</response>
        [HttpPost]
        [Consumes("multipart/form-data")]
        [ProducesResponseType(typeof(ApiResponse<TemplateResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<TemplateResponseDto>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<TemplateResponseDto>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<TemplateResponseDto>>> CreateTemplate([FromForm] CreateTemplateDto createDto)
        {
            if (!ModelState.IsValid)
            {
                var errors = ExtractModelStateErrors();
                return StatusCode(400, ApiResponse<TemplateResponseDto>.BadRequest("داده‌های ورودی نامعتبر است", errors));
            }

            var userId = await GetCurrentUserIdAsync();
            var result = await _messageService.CreateTemplateAsync(userId, createDto);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// دریافت لیست قالب‌های پیام
        /// </summary>
        /// <returns>پاسخ شامل لیست تمام قالب‌های کاربر</returns>
        /// <remarks>
        /// این endpoint لیست تمام قالب‌های پیام کاربر فعلی را برمی‌گرداند.
        /// 
        /// **نکات مهم:**
        /// - فقط قالب‌های کاربر فعلی برگردانده می‌شوند
        /// - قالب‌ها به ترتیب تاریخ ایجاد (جدیدترین اول) برگردانده می‌شوند
        /// </remarks>
        /// <response code="200">لیست قالب‌ها با موفقیت برگردانده شد</response>
        /// <response code="500">خطای سرور</response>
        [HttpGet]
        [ProducesResponseType(typeof(ApiResponse<List<TemplateResponseDto>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<List<TemplateResponseDto>>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<List<TemplateResponseDto>>>> GetTemplates()
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _messageService.GetTemplatesAsync(userId);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// دریافت لیست قالب‌ها به صورت دسته‌بندی شده
        /// </summary>
        /// <returns>پاسخ شامل لیست قالب‌ها دسته‌بندی شده بر اساس گروه</returns>
        /// <remarks>
        /// این endpoint لیست قالب‌های پیام کاربر را به صورت دسته‌بندی شده بر اساس گروه‌ها برمی‌گرداند.
        /// 
        /// **ساختار پاسخ:**
        /// - هر گروه شامل نام گروه و لیست قالب‌های آن
        /// - قالب‌های بدون گروه در بخش "بدون گروه" نمایش داده می‌شوند
        /// 
        /// **استفاده:**
        /// برای نمایش قالب‌ها در رابط کاربری به صورت دسته‌بندی شده استفاده می‌شود.
        /// </remarks>
        /// <response code="200">لیست قالب‌های دسته‌بندی شده با موفقیت برگردانده شد</response>
        /// <response code="500">خطای سرور</response>
        [HttpGet("grouped")]
        [ProducesResponseType(typeof(ApiResponse<List<CategoryGroupDto>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<List<CategoryGroupDto>>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<List<CategoryGroupDto>>>> GetTemplatesGrouped()
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _messageService.GetTemplatesGroupedByCategoryAsync(userId);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// به‌روزرسانی قالب پیام
        /// </summary>
        /// <param name="id">شناسه قالب</param>
        /// <param name="updateDto">اطلاعات به‌روزرسانی</param>
        /// <returns>پاسخ شامل اطلاعات قالب به‌روزرسانی شده</returns>
        /// <remarks>
        /// این endpoint برای به‌روزرسانی اطلاعات یک قالب پیام استفاده می‌شود.
        /// 
        /// **نکات مهم:**
        /// - فقط فیلدهای ارسال شده به‌روزرسانی می‌شوند
        /// - قالب باید متعلق به کاربر فعلی باشد
        /// - در صورت استفاده از قالب در کمپین فعال، تغییرات اعمال می‌شود
        /// - می‌توانید یک فایل عکس به عنوان آیکون آپلود کنید (اختیاری)
        /// </remarks>
        /// <response code="200">اطلاعات قالب با موفقیت به‌روزرسانی شد</response>
        /// <response code="400">داده‌های ورودی نامعتبر است</response>
        /// <response code="404">قالب یافت نشد</response>
        /// <response code="500">خطای سرور</response>
        [HttpPost("{id}/update")]
        [Consumes("multipart/form-data")]
        [ProducesResponseType(typeof(ApiResponse<TemplateResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<TemplateResponseDto>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<TemplateResponseDto>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<TemplateResponseDto>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<TemplateResponseDto>>> UpdateTemplate(int id, [FromForm] UpdateTemplateDto updateDto)
        {
            if (!ModelState.IsValid)
            {
                var errors = ExtractModelStateErrors();
                return StatusCode(400, ApiResponse<TemplateResponseDto>.BadRequest("داده‌های ورودی نامعتبر است", errors));
            }

            var userId = await GetCurrentUserIdAsync();
            var result = await _messageService.UpdateTemplateAsync(id, userId, updateDto);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// حذف قالب پیام
        /// </summary>
        /// <param name="id">شناسه قالب</param>
        /// <returns>پاسخ شامل وضعیت حذف</returns>
        /// <remarks>
        /// این endpoint برای حذف یک قالب پیام استفاده می‌شود.
        /// 
        /// **نکات مهم:**
        /// - قالب باید متعلق به کاربر فعلی باشد
        /// - در صورت استفاده از قالب در کمپین فعال، حذف انجام نمی‌شود
        /// - این عملیات قابل بازگشت نیست
        /// </remarks>
        /// <response code="200">قالب با موفقیت حذف شد</response>
        /// <response code="404">قالب یافت نشد</response>
        /// <response code="409">قالب در کمپین فعال استفاده می‌شود</response>
        /// <response code="500">خطای سرور</response>
        [HttpPost("{id}/delete")]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<bool>>> DeleteTemplate(int id)
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _messageService.DeleteTemplateAsync(id, userId);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// تنظیم قالب پیش‌فرض کاربر
        /// </summary>
        /// <param name="setDefaultDto">اطلاعات شامل شناسه قالب</param>
        /// <returns>پاسخ شامل اطلاعات قالب پیش‌فرض تنظیم شده</returns>
        /// <remarks>
        /// این endpoint برای تنظیم یک قالب به عنوان قالب پیش‌فرض کاربر استفاده می‌شود.
        /// 
        /// **نکات مهم:**
        /// - قالب باید متعلق به کاربر فعلی باشد
        /// - قالب باید فعال و حذف نشده باشد
        /// - با تنظیم قالب جدید به عنوان پیش‌فرض، قالب پیش‌فرض قبلی به صورت خودکار غیرفعال می‌شود
        /// - هر کاربر فقط می‌تواند یک قالب پیش‌فرض داشته باشد
        /// 
        /// **استفاده:**
        /// کاربر می‌تواند از بین تمام قالب‌های خود، یکی را به عنوان قالب پیش‌فرض انتخاب کند.
        /// این قالب پیش‌فرض می‌تواند در رابط کاربری به صورت پیش‌فرض انتخاب شود.
        /// </remarks>
        /// <response code="200">قالب پیش‌فرض با موفقیت تنظیم شد</response>
        /// <response code="400">داده‌های ورودی نامعتبر است</response>
        /// <response code="404">قالب یافت نشد یا متعلق به شما نیست</response>
        /// <response code="500">خطای سرور</response>
        [HttpPost("set-default")]
        [ProducesResponseType(typeof(ApiResponse<TemplateResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<TemplateResponseDto>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<TemplateResponseDto>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<TemplateResponseDto>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<TemplateResponseDto>>> SetDefaultTemplate([FromBody] SetDefaultTemplateDto setDefaultDto)
        {
            if (!ModelState.IsValid)
            {
                var errors = ExtractModelStateErrors();
                return StatusCode(400, ApiResponse<TemplateResponseDto>.BadRequest("داده‌های ورودی نامعتبر است", errors));
            }

            var userId = await GetCurrentUserIdAsync();
            var result = await _messageService.SetUserDefaultTemplateAsync(userId, setDefaultDto.TemplateId);
            return StatusCode(result.StatusCode, result);
        }

        #endregion

        #region Template Group Operations

        /// <summary>
        /// ایجاد گروه قالب جدید
        /// </summary>
        /// <param name="createDto">اطلاعات گروه جدید شامل نام و توضیحات</param>
        /// <returns>پاسخ شامل اطلاعات گروه ایجاد شده</returns>
        /// <remarks>
        /// این endpoint برای ایجاد یک گروه قالب جدید استفاده می‌شود.
        /// 
        /// **استفاده:**
        /// گروه‌های قالب برای سازماندهی و دسته‌بندی قالب‌ها استفاده می‌شوند.
        /// 
        /// **نکات مهم:**
        /// - نام گروه باید منحصر به فرد باشد
        /// - می‌توانید بعداً قالب‌ها را به این گروه اضافه کنید
        /// - می‌توانید یک فایل عکس به عنوان آیکون آپلود کنید (اختیاری)
        /// </remarks>
        /// <response code="200">گروه قالب با موفقیت ایجاد شد</response>
        /// <response code="400">داده‌های ورودی نامعتبر است</response>
        /// <response code="409">گروه با این نام قبلاً ایجاد شده است</response>
        /// <response code="500">خطای سرور</response>
        [HttpPost("group")]
        [Consumes("multipart/form-data")]
        [ProducesResponseType(typeof(ApiResponse<TemplateGroupResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<TemplateGroupResponseDto>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<TemplateGroupResponseDto>), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(ApiResponse<TemplateGroupResponseDto>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<TemplateGroupResponseDto>>> CreateTemplateGroup([FromForm] CreateTemplateGroupDto createDto)
        {
            if (!ModelState.IsValid)
            {
                var errors = ExtractModelStateErrors();
                return StatusCode(400, ApiResponse<TemplateGroupResponseDto>.BadRequest("داده‌های ورودی نامعتبر است", errors));
            }

            var userId = await GetCurrentUserIdAsync();
            var result = await _messageService.CreateTemplateGroupAsync(userId, createDto);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// دریافت لیست گروه‌های قالب
        /// </summary>
        /// <returns>پاسخ شامل لیست گروه‌های قالب (فقط نام و تعداد)</returns>
        /// <remarks>
        /// این endpoint لیست تمام گروه‌های قالب کاربر فعلی را برمی‌گرداند.
        /// 
        /// **نکات مهم:**
        /// - فقط نام گروه‌ها و تعداد قالب‌های هر گروه برگردانده می‌شود
        /// - قالب‌های بدون گروه در گروه "بدون گروه" قرار می‌گیرند
        /// - گروه‌ها به ترتیب DisplayOrder مرتب می‌شوند
        /// </remarks>
        /// <response code="200">لیست گروه‌های قالب با موفقیت برگردانده شد</response>
        /// <response code="500">خطای سرور</response>
        [HttpGet("group")]
        [ProducesResponseType(typeof(ApiResponse<List<TemplateGroupSummaryDto>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<List<TemplateGroupSummaryDto>>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<List<TemplateGroupSummaryDto>>>> GetTemplateGroups()
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _messageService.GetTemplateGroupsAsync(userId);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// دریافت اطلاعات گروه قالب بر اساس شناسه
        /// </summary>
        /// <param name="id">شناسه گروه قالب</param>
        /// <returns>پاسخ شامل اطلاعات کامل گروه</returns>
        /// <remarks>
        /// این endpoint اطلاعات کامل یک گروه قالب را بر اساس شناسه برمی‌گرداند.
        /// 
        /// **اطلاعات شامل:**
        /// - نام و توضیحات گروه
        /// - لیست قالب‌های موجود در گروه
        /// - تاریخ ایجاد و به‌روزرسانی
        /// </remarks>
        /// <response code="200">اطلاعات گروه با موفقیت برگردانده شد</response>
        /// <response code="404">گروه یافت نشد</response>
        /// <response code="500">خطای سرور</response>
        [HttpGet("group/{id}")]
        [ProducesResponseType(typeof(ApiResponse<TemplateGroupResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<TemplateGroupResponseDto>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<TemplateGroupResponseDto>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<TemplateGroupResponseDto>>> GetTemplateGroupById(int id)
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _messageService.GetTemplateGroupByIdAsync(id, userId);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// دریافت لیست قالی‌های (قالب‌های) یک گروه
        /// </summary>
        /// <param name="id">شناسه گروه قالب</param>
        /// <returns>پاسخ شامل لیست قالی‌های (قالب‌های) گروه</returns>
        /// <remarks>
        /// این endpoint لیست تمام قالی‌های (قالب‌های) متعلق به یک گروه خاص را برمی‌گرداند.
        /// 
        /// **نکات مهم:**
        /// - فقط قالی‌های فعال و حذف نشده برگردانده می‌شوند
        /// - گروه باید متعلق به کاربر فعلی باشد
        /// - اگر id = 0 باشد، قالب‌های بدون گروه (uncategorized) برگردانده می‌شوند
        /// - قالی‌ها به ترتیب تاریخ ایجاد (نزولی) مرتب می‌شوند
        /// </remarks>
        /// <response code="200">لیست قالی‌های گروه با موفقیت برگردانده شد</response>
        /// <response code="404">گروه یافت نشد</response>
        /// <response code="500">خطای سرور</response>
        [HttpGet("group/{id}/templates")]
        [ProducesResponseType(typeof(ApiResponse<List<TemplateResponseDto>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<List<TemplateResponseDto>>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<List<TemplateResponseDto>>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<List<TemplateResponseDto>>>> GetTemplatesByGroupId(int id)
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _messageService.GetTemplatesByGroupIdAsync(id, userId);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// به‌روزرسانی گروه قالب
        /// </summary>
        /// <param name="id">شناسه گروه قالب</param>
        /// <param name="updateDto">اطلاعات به‌روزرسانی</param>
        /// <returns>پاسخ شامل اطلاعات گروه به‌روزرسانی شده</returns>
        /// <remarks>
        /// این endpoint برای به‌روزرسانی اطلاعات یک گروه قالب استفاده می‌شود.
        /// 
        /// **نکات مهم:**
        /// - فقط فیلدهای ارسال شده به‌روزرسانی می‌شوند
        /// - گروه باید متعلق به کاربر فعلی باشد
        /// - نام گروه باید منحصر به فرد باشد
        /// - می‌توانید یک فایل عکس به عنوان آیکون آپلود کنید (اختیاری)
        /// </remarks>
        /// <response code="200">اطلاعات گروه با موفقیت به‌روزرسانی شد</response>
        /// <response code="400">داده‌های ورودی نامعتبر است</response>
        /// <response code="404">گروه یافت نشد</response>
        /// <response code="409">گروه با این نام قبلاً وجود دارد</response>
        /// <response code="500">خطای سرور</response>
        [HttpPost("group/{id}/update")]
        [Consumes("multipart/form-data")]
        [ProducesResponseType(typeof(ApiResponse<TemplateGroupResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<TemplateGroupResponseDto>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<TemplateGroupResponseDto>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<TemplateGroupResponseDto>), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(ApiResponse<TemplateGroupResponseDto>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<TemplateGroupResponseDto>>> UpdateTemplateGroup(int id, [FromForm] UpdateTemplateGroupDto updateDto)
        {
            if (!ModelState.IsValid)
            {
                var errors = ExtractModelStateErrors();
                return StatusCode(400, ApiResponse<TemplateGroupResponseDto>.BadRequest("داده‌های ورودی نامعتبر است", errors));
            }

            var userId = await GetCurrentUserIdAsync();
            var result = await _messageService.UpdateTemplateGroupAsync(id, userId, updateDto);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// حذف گروه قالب
        /// </summary>
        /// <param name="id">شناسه گروه قالب</param>
        /// <returns>پاسخ شامل وضعیت حذف</returns>
        /// <remarks>
        /// این endpoint برای حذف یک گروه قالب استفاده می‌شود.
        /// 
        /// **نکات مهم:**
        /// - گروه باید متعلق به کاربر فعلی باشد
        /// - اگر گروه دارای قالب باشد، امکان حذف وجود ندارد و خطای 409 برمی‌گرداند
        /// - ابتدا باید قالب‌ها را به گروه دیگری منتقل کنید یا حذف کنید
        /// - این عملیات قابل بازگشت نیست
        /// </remarks>
        /// <response code="200">گروه با موفقیت حذف شد</response>
        /// <response code="404">گروه یافت نشد</response>
        /// <response code="409">گروه دارای قالب است و قابل حذف نیست</response>
        /// <response code="500">خطای سرور</response>
        [HttpPost("group/{id}/delete")]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<bool>>> DeleteTemplateGroup(int id)
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _messageService.DeleteTemplateGroupAsync(id, userId);
            return StatusCode(result.StatusCode, result);
        }

        #endregion
    }
}

