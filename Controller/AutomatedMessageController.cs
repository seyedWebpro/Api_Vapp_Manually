using Api_Vapp.DTOs.Automation;
using Api_Vapp.DTOs.Common;
using Api_Vapp.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System.Security.Claims;

namespace Api_Vapp.Controller
{
    /// <summary>
    /// کنترلر مدیریت پیام‌های خودکار
    /// </summary>
    /// <remarks>
    /// این کنترلر شامل تمام endpoint های مربوط به مدیریت پیام‌های خودکار می‌باشد.
    /// پیام‌های خودکار برای ارسال خودکار پیام‌ها در زمان‌های خاص (مثلاً تولد، سالگرد و غیره) استفاده می‌شوند.
    /// 
    /// **قابلیت‌های اصلی:**
    /// - ایجاد و مدیریت پیام‌های خودکار
    /// - انتخاب گیرندگان برای پیام‌های خودکار
    /// - تنظیم زمان و شرایط ارسال
    /// - مدیریت محتوای پیام
    /// - فعال/غیرفعال کردن پیام‌های خودکار
    /// 
    /// **انواع پیام‌های خودکار:**
    /// - پیام تولد
    /// - پیام سالگرد
    /// - پیام‌های رویداد خاص
    /// 
    /// تمام endpoint های این کنترلر نیاز به احراز هویت دارند.
    /// </remarks>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    [Produces("application/json")]
    public class AutomatedMessageController : VappControllerBase
    {
        private readonly IAutomatedMessageService _automatedMessageService;

        public AutomatedMessageController(IAutomatedMessageService automatedMessageService, IConfiguration configuration, IUserRepository userRepository)
            : base(configuration, userRepository)
        {
            _automatedMessageService = automatedMessageService;
        }

        /// <summary>
        /// دریافت لیست انواع پیام‌های خودکار با Pagination
        /// </summary>
        /// <param name="pageNumber">شماره صفحه (پیش‌فرض: 1)</param>
        /// <param name="pageSize">تعداد آیتم در هر صفحه (پیش‌فرض: 10، حداکثر: 100)</param>
        /// <returns>پاسخ شامل لیست انواع پیام‌های خودکار و اطلاعات pagination</returns>
        /// <remarks>
        /// این endpoint برای دریافت لیست انواع پیام‌های خودکار موجود در سیستم استفاده می‌شود.
        /// 
        /// **انواع پیام‌های خودکار شامل:**
        /// - پیام تولد
        /// - پیام سالگرد
        /// - پیام‌های رویداد خاص
        /// 
        /// **نکات مهم:**
        /// - این لیست برای تمام کاربران یکسان است
        /// - از این لیست برای انتخاب نوع پیام خودکار در مرحله ایجاد استفاده می‌شود
        /// </remarks>
        /// <response code="200">لیست انواع پیام‌های خودکار با موفقیت برگردانده شد</response>
        /// <response code="400">پارامترهای ورودی نامعتبر است</response>
        /// <response code="500">خطای سرور</response>
        [HttpGet("types")]
        [ProducesResponseType(typeof(ApiResponse<AutomationTypeListResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<AutomationTypeListResponseDto>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<AutomationTypeListResponseDto>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<AutomationTypeListResponseDto>>> GetAutomationTypes(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10)
        {
            var result = await _automatedMessageService.GetAutomationTypesAsync(pageNumber, pageSize);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// ایجاد پیش‌نویس پیام خودکار (مرحله 1 - انتخاب نوع پیام)
        /// </summary>
        /// <param name="createDto">اطلاعات پیش‌نویس شامل نوع پیام خودکار</param>
        /// <returns>پاسخ شامل اطلاعات پیش‌نویس ایجاد شده</returns>
        /// <remarks>
        /// این endpoint برای ایجاد یک پیش‌نویس پیام خودکار استفاده می‌شود.
        /// این اولین مرحله از فرآیند ایجاد پیام خودکار است.
        /// 
        /// **فرآیند ایجاد پیام خودکار:**
        /// 1. ایجاد پیش‌نویس (این endpoint)
        /// 2. انتخاب گیرندگان
        /// 3. تنظیمات پایه
        /// 4. ساخت پیام
        /// 5. خلاصه و تأیید
        /// 
        /// **نکات مهم:**
        /// - پس از ایجاد پیش‌نویس، می‌توانید مراحل بعدی را تکمیل کنید
        /// - پیش‌نویس تا زمان تکمیل کامل قابل ویرایش است
        /// </remarks>
        /// <response code="200">پیش‌نویس با موفقیت ایجاد شد</response>
        /// <response code="400">داده‌های ورودی نامعتبر است</response>
        /// <response code="500">خطای سرور</response>
        [HttpPost("create-draft")]
        [ProducesResponseType(typeof(ApiResponse<AutomatedMessageResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<AutomatedMessageResponseDto>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<AutomatedMessageResponseDto>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<AutomatedMessageResponseDto>>> CreateAutomatedMessageDraft([FromBody] CreateAutomatedMessageDraftDto createDto)
        {
            if (!ModelState.IsValid)
            {
                var errors = ExtractModelStateErrors();
                return StatusCode(400, ApiResponse<AutomatedMessageResponseDto>.BadRequest("داده‌های ورودی نامعتبر است", errors));
            }

            var userId = await GetCurrentUserIdAsync();
            var result = await _automatedMessageService.CreateAutomatedMessageDraftAsync(userId, createDto);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// انتخاب گیرندگان برای پیام خودکار (مرحله 2 - انتخاب مخاطبین)
        /// </summary>
        /// <param name="automatedMessageId">شناسه پیام خودکار</param>
        /// <param name="selectDto">اطلاعات انتخاب گیرندگان شامل تگ‌ها، دفترچه‌ها یا لیست مخاطبین</param>
        /// <returns>پاسخ شامل لیست گیرندگان انتخاب شده و تعداد آن‌ها</returns>
        /// <remarks>
        /// این endpoint برای انتخاب گیرندگان یک پیام خودکار استفاده می‌شود.
        /// این دومین مرحله از فرآیند ایجاد پیام خودکار است.
        /// 
        /// **روش‌های انتخاب گیرندگان:**
        /// - بر اساس تگ: انتخاب تمام مخاطبینی که تگ خاصی دارند
        /// - بر اساس دفترچه: انتخاب تمام مخاطبین یک یا چند دفترچه
        /// - بر اساس لیست: انتخاب مخاطبین خاص از لیست شناسه‌ها
        /// - ترکیبی: می‌توانید از چند روش همزمان استفاده کنید
        /// 
        /// **نکات مهم:**
        /// - می‌توانید فیلترهای اضافی اعمال کنید (مثلاً فقط مخاطبین فعال)
        /// - لیست گیرندگان برای استفاده در پیام خودکار ذخیره می‌شود
        /// </remarks>
        /// <response code="200">لیست گیرندگان با موفقیت انتخاب شد</response>
        /// <response code="400">داده‌های ورودی نامعتبر است</response>
        /// <response code="404">پیام خودکار یافت نشد</response>
        /// <response code="500">خطای سرور</response>
        [HttpPost("{automatedMessageId}/recipients/select")]
        [ProducesResponseType(typeof(ApiResponse<RecipientListForAutomatedMessageResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<RecipientListForAutomatedMessageResponseDto>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<RecipientListForAutomatedMessageResponseDto>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<RecipientListForAutomatedMessageResponseDto>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<RecipientListForAutomatedMessageResponseDto>>> SelectRecipientsForAutomatedMessage(
            int automatedMessageId,
            [FromBody] SelectRecipientsForAutomatedMessageDto selectDto)
        {
            if (!ModelState.IsValid)
            {
                var errors = ExtractModelStateErrors();
                return StatusCode(400, ApiResponse<RecipientListForAutomatedMessageResponseDto>.BadRequest("داده‌های ورودی نامعتبر است", errors));
            }

            var userId = await GetCurrentUserIdAsync();
            var result = await _automatedMessageService.SelectRecipientsForAutomatedMessageAsync(userId, automatedMessageId, selectDto);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// ایجاد پیام خودکار جدید (یکجا - تمام مراحل)
        /// </summary>
        /// <param name="createDto">اطلاعات کامل پیام خودکار شامل نوع، گیرندگان، تنظیمات و محتوا</param>
        /// <returns>پاسخ شامل اطلاعات پیام خودکار ایجاد شده</returns>
        /// <remarks>
        /// این endpoint برای ایجاد یک پیام خودکار کامل در یک درخواست استفاده می‌شود.
        /// 
        /// **تفاوت با فرآیند مرحله‌ای:**
        /// - این endpoint تمام مراحل را در یک درخواست انجام می‌دهد
        /// - برای ایجاد سریع و ساده استفاده می‌شود
        /// - برای ایجاد پیچیده‌تر، از فرآیند مرحله‌ای استفاده کنید
        /// 
        /// **نکات مهم:**
        /// - پیام خودکار ایجاد شده به صورت پیش‌فرض فعال است
        /// - می‌توانید بعداً از endpoint های دیگر برای ویرایش استفاده کنید
        /// </remarks>
        /// <response code="200">پیام خودکار با موفقیت ایجاد شد</response>
        /// <response code="400">داده‌های ورودی نامعتبر است</response>
        /// <response code="500">خطای سرور</response>
        [HttpPost]
        [ProducesResponseType(typeof(ApiResponse<AutomatedMessageResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<AutomatedMessageResponseDto>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<AutomatedMessageResponseDto>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<AutomatedMessageResponseDto>>> CreateAutomatedMessage([FromBody] CreateAutomatedMessageDto createDto)
        {
            if (!ModelState.IsValid)
            {
                var errors = ExtractModelStateErrors();
                return StatusCode(400, ApiResponse<AutomatedMessageResponseDto>.BadRequest("داده‌های ورودی نامعتبر است", errors));
            }

            var userId = await GetCurrentUserIdAsync();
            var result = await _automatedMessageService.CreateAutomatedMessageAsync(userId, createDto);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// دریافت اطلاعات پیام خودکار بر اساس شناسه
        /// </summary>
        /// <param name="id">شناسه پیام خودکار</param>
        /// <returns>پاسخ شامل اطلاعات کامل پیام خودکار</returns>
        /// <remarks>
        /// این endpoint برای دریافت اطلاعات کامل یک پیام خودکار بر اساس شناسه استفاده می‌شود.
        /// 
        /// **اطلاعات برگردانده شده شامل:**
        /// - نوع پیام خودکار
        /// - لیست گیرندگان
        /// - تنظیمات ارسال
        /// - محتوای پیام
        /// - وضعیت فعال/غیرفعال
        /// - تاریخ ایجاد و به‌روزرسانی
        /// </remarks>
        /// <response code="200">اطلاعات پیام خودکار با موفقیت برگردانده شد</response>
        /// <response code="404">پیام خودکار یافت نشد</response>
        /// <response code="500">خطای سرور</response>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(ApiResponse<AutomatedMessageResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<AutomatedMessageResponseDto>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<AutomatedMessageResponseDto>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<AutomatedMessageResponseDto>>> GetAutomatedMessageById(int id)
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _automatedMessageService.GetAutomatedMessageByIdAsync(id, userId);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// دریافت لیست پیام‌های خودکار با pagination و فیلتر
        /// </summary>
        /// <param name="pageNumber">شماره صفحه (پیش‌فرض: 1)</param>
        /// <param name="pageSize">تعداد آیتم در هر صفحه (پیش‌فرض: 10، حداکثر: 100)</param>
        /// <param name="filter">فیلتر بر اساس نوع یا وضعیت (اختیاری)</param>
        /// <returns>پاسخ شامل لیست پیام‌های خودکار و اطلاعات pagination</returns>
        /// <remarks>
        /// این endpoint برای دریافت لیست پیام‌های خودکار کاربر فعلی با امکان pagination و فیلتر استفاده می‌شود.
        /// 
        /// **نکات مهم:**
        /// - فقط پیام‌های خودکار کاربر فعلی برگردانده می‌شوند
        /// - می‌توانید بر اساس نوع یا وضعیت فیلتر کنید
        /// </remarks>
        /// <response code="200">لیست پیام‌های خودکار با موفقیت برگردانده شد</response>
        /// <response code="400">پارامترهای ورودی نامعتبر است</response>
        /// <response code="500">خطای سرور</response>
        [HttpGet]
        [ProducesResponseType(typeof(ApiResponse<AutomatedMessageListResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<AutomatedMessageListResponseDto>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<AutomatedMessageListResponseDto>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<AutomatedMessageListResponseDto>>> GetAutomatedMessages(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? filter = null)
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _automatedMessageService.GetAutomatedMessagesAsync(userId, pageNumber, pageSize, filter);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// تست فوری ارسال پیام‌های خودکار تولد (فقط برای توسعه)
        /// </summary>
        /// <returns>پاسخ شامل نتیجه تست ارسال</returns>
        /// <remarks>
        /// این endpoint برای تست فوری ارسال پیام‌های خودکار تولد استفاده می‌شود.
        /// 
        /// **نکات مهم:**
        /// - این endpoint فقط برای محیط توسعه است
        /// - تمام پیام‌های خودکار تولد فعال را بلافاصله ارسال می‌کند
        /// - برای تست عملکرد سیستم استفاده می‌شود
        /// - در محیط production باید غیرفعال باشد
        /// </remarks>
        /// <response code="200">تست ارسال با موفقیت انجام شد</response>
        /// <response code="500">خطای سرور</response>
        [HttpPost("test-send-birthday-now")]
        [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<string>>> TestSendBirthdayMessagesNow()
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _automatedMessageService.TestSendBirthdayMessagesNowAsync(userId);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// به‌روزرسانی پیام خودکار
        /// </summary>
        /// <param name="id">شناسه پیام خودکار</param>
        /// <param name="updateDto">اطلاعات به‌روزرسانی</param>
        /// <returns>پاسخ شامل اطلاعات پیام خودکار به‌روزرسانی شده</returns>
        /// <remarks>
        /// این endpoint برای به‌روزرسانی اطلاعات یک پیام خودکار استفاده می‌شود.
        /// 
        /// **نکات مهم:**
        /// - فقط فیلدهای ارسال شده به‌روزرسانی می‌شوند
        /// - پیام خودکار باید متعلق به کاربر فعلی باشد
        /// - می‌توانید گیرندگان، تنظیمات یا محتوا را به‌روزرسانی کنید
        /// </remarks>
        /// <response code="200">اطلاعات پیام خودکار با موفقیت به‌روزرسانی شد</response>
        /// <response code="400">داده‌های ورودی نامعتبر است</response>
        /// <response code="404">پیام خودکار یافت نشد</response>
        /// <response code="500">خطای سرور</response>
        [HttpPost("{id}/update")]
        [ProducesResponseType(typeof(ApiResponse<AutomatedMessageResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<AutomatedMessageResponseDto>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<AutomatedMessageResponseDto>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<AutomatedMessageResponseDto>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<AutomatedMessageResponseDto>>> UpdateAutomatedMessage(int id, [FromBody] UpdateAutomatedMessageDto updateDto)
        {
            if (!ModelState.IsValid)
            {
                var errors = ExtractModelStateErrors();
                return StatusCode(400, ApiResponse<AutomatedMessageResponseDto>.BadRequest("داده‌های ورودی نامعتبر است", errors));
            }

            var userId = await GetCurrentUserIdAsync();
            var result = await _automatedMessageService.UpdateAutomatedMessageAsync(id, userId, updateDto);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// حذف پیام خودکار
        /// </summary>
        /// <param name="id">شناسه پیام خودکار</param>
        /// <returns>پاسخ شامل وضعیت حذف</returns>
        /// <remarks>
        /// این endpoint برای حذف یک پیام خودکار استفاده می‌شود.
        /// 
        /// **نکات مهم:**
        /// - پیام خودکار باید متعلق به کاربر فعلی باشد
        /// - این عملیات قابل بازگشت نیست
        /// - پس از حذف، پیام خودکار دیگر ارسال نمی‌شود
        /// </remarks>
        /// <response code="200">پیام خودکار با موفقیت حذف شد</response>
        /// <response code="404">پیام خودکار یافت نشد</response>
        /// <response code="500">خطای سرور</response>
        [HttpPost("{id}/delete")]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<bool>>> DeleteAutomatedMessage(int id)
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _automatedMessageService.DeleteAutomatedMessageAsync(id, userId);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// تغییر وضعیت پیام خودکار (فعال/غیرفعال)
        /// </summary>
        /// <param name="id">شناسه پیام خودکار</param>
        /// <param name="isActive">وضعیت جدید (true = فعال، false = غیرفعال)</param>
        /// <returns>پاسخ شامل وضعیت تغییر</returns>
        /// <remarks>
        /// این endpoint برای تغییر وضعیت فعال/غیرفعال یک پیام خودکار استفاده می‌شود.
        /// 
        /// **نکات مهم:**
        /// - پیام خودکار غیرفعال ارسال نمی‌شود
        /// - می‌توانید پیام خودکار را موقتاً غیرفعال کنید بدون حذف
        /// - پیام خودکار غیرفعال می‌تواند دوباره فعال شود
        /// </remarks>
        /// <response code="200">وضعیت پیام خودکار با موفقیت تغییر کرد</response>
        /// <response code="404">پیام خودکار یافت نشد</response>
        /// <response code="500">خطای سرور</response>
        [HttpPost("{id}/toggle-status")]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<bool>>> ToggleAutomatedMessageStatus(int id, [FromBody] bool isActive)
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _automatedMessageService.ToggleAutomatedMessageStatusAsync(id, userId, isActive);
            return StatusCode(result.StatusCode, result);
        }

        #region تنظیمات پایه (مرحله 3)







        /// <summary>
        /// ذخیره تنظیمات یکپارچه برای پیام خودکار (مرحله 3 - تنظیمات پایه)
        /// </summary>
        /// <param name="automatedMessageId">شناسه پیام خودکار</param>
        /// <param name="unifiedDto">تنظیمات یکپارچه شامل زمان ارسال، شرایط و سایر تنظیمات</param>
        /// <returns>پاسخ شامل وضعیت ذخیره تنظیمات</returns>
        /// <remarks>
        /// این endpoint برای ذخیره تنظیمات یکپارچه یک پیام خودکار استفاده می‌شود.
        /// این سومین مرحله از فرآیند ایجاد پیام خودکار است.
        /// 
        /// **تنظیمات شامل:**
        /// - زمان ارسال (مثلاً ساعت 9 صبح)
        /// - شرایط ارسال (مثلاً فقط برای مخاطبین فعال)
        /// - تنظیمات پیشرفته
        /// 
        /// **نکات مهم:**
        /// - این endpoint جایگزین endpoint های جداگانه تنظیمات است
        /// - می‌توانید تمام تنظیمات را در یک درخواست ذخیره کنید
        /// </remarks>
        /// <response code="200">تنظیمات با موفقیت ذخیره شد</response>
        /// <response code="400">داده‌های ورودی نامعتبر است</response>
        /// <response code="404">پیام خودکار یافت نشد</response>
        /// <response code="500">خطای سرور</response>
        [HttpPost("{automatedMessageId}/settings")]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<object>>> SaveUnifiedSettings(
            int automatedMessageId,
            [FromBody] UnifiedSettingsDto unifiedDto)
        {
            if (!ModelState.IsValid)
            {
                var errors = ExtractModelStateErrors();
                return StatusCode(400, ApiResponse<object>.BadRequest("داده‌های ورودی نامعتبر است", errors));
            }

            var userId = await GetCurrentUserIdAsync();
            var result = await _automatedMessageService.SaveUnifiedSettingsAsync(automatedMessageId, userId, unifiedDto);
            return StatusCode(result.StatusCode, result);
        }

        #endregion

        #region ساخت پیام (مرحله 4)

        /// <summary>
        /// ذخیره محتوای پیام خودکار (مرحله 4 - ساخت پیام)
        /// </summary>
        /// <param name="automatedMessageId">شناسه پیام خودکار</param>
        /// <param name="contentDto">محتوای پیام شامل متن و Placeholder ها</param>
        /// <returns>پاسخ شامل اطلاعات محتوای ذخیره شده</returns>
        /// <remarks>
        /// این endpoint برای ذخیره محتوای یک پیام خودکار استفاده می‌شود.
        /// این چهارمین مرحله از فرآیند ایجاد پیام خودکار است.
        /// 
        /// **محتوای پیام شامل:**
        /// - متن پیام
        /// - Placeholder ها برای شخصی‌سازی (مثلاً {FullName}, {BirthdayDate})
        /// 
        /// **Placeholder های رایج:**
        /// - {FullName}: نام کامل مخاطب
        /// - {MobileNumber}: شماره موبایل
        /// - {BirthdayDate}: تاریخ تولد
        /// - {Brand}: برند
        /// 
        /// **نکات مهم:**
        /// - می‌توانید از Placeholder ها برای شخصی‌سازی استفاده کنید
        /// - محتوا در زمان ارسال با داده‌های واقعی جایگزین می‌شود
        /// </remarks>
        /// <response code="200">محتوای پیام با موفقیت ذخیره شد</response>
        /// <response code="400">داده‌های ورودی نامعتبر است</response>
        /// <response code="404">پیام خودکار یافت نشد</response>
        /// <response code="500">خطای سرور</response>
        [HttpPost("{automatedMessageId}/message/content")]
        [ProducesResponseType(typeof(ApiResponse<MessageContentResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<MessageContentResponseDto>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<MessageContentResponseDto>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<MessageContentResponseDto>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<MessageContentResponseDto>>> SaveMessageContent(
            int automatedMessageId,
            [FromBody] SaveMessageContentDto contentDto)
        {
            if (!ModelState.IsValid)
            {
                var errors = ExtractModelStateErrors();
                return StatusCode(400, ApiResponse<MessageContentResponseDto>.BadRequest("داده‌های ورودی نامعتبر است", errors));
            }

            var userId = await GetCurrentUserIdAsync();
            var result = await _automatedMessageService.SaveMessageContentAsync(automatedMessageId, userId, contentDto);
            return StatusCode(result.StatusCode, result);
        }

        #endregion

        #region خلاصه و تنظیمات (مرحله 5)

        /// <summary>
        /// دریافت خلاصه پیام خودکار (مرحله 5 - خلاصه و تأیید)
        /// </summary>
        /// <param name="automatedMessageId">شناسه پیام خودکار</param>
        /// <returns>پاسخ شامل خلاصه کامل پیام خودکار</returns>
        /// <remarks>
        /// این endpoint برای دریافت خلاصه یک پیام خودکار استفاده می‌شود.
        /// این پنجمین و آخرین مرحله از فرآیند ایجاد پیام خودکار است.
        /// 
        /// **خلاصه شامل:**
        /// - نوع پیام خودکار
        /// - تعداد گیرندگان
        /// - محتوای پیام
        /// - تنظیمات ارسال
        /// - هزینه تخمینی
        /// - وضعیت
        /// 
        /// **نکات مهم:**
        /// - از این endpoint برای بررسی نهایی قبل از فعال‌سازی استفاده کنید
        /// - خلاصه شامل تمام اطلاعات پیام خودکار است
        /// </remarks>
        /// <response code="200">خلاصه پیام خودکار با موفقیت برگردانده شد</response>
        /// <response code="404">پیام خودکار یافت نشد</response>
        /// <response code="500">خطای سرور</response>
        [HttpGet("{automatedMessageId}/summary")]
        [ProducesResponseType(typeof(ApiResponse<AutomatedMessageSummaryResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<AutomatedMessageSummaryResponseDto>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<AutomatedMessageSummaryResponseDto>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<AutomatedMessageSummaryResponseDto>>> GetAutomatedMessageSummary(int automatedMessageId)
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _automatedMessageService.GetAutomatedMessageSummaryAsync(automatedMessageId, userId);
            return StatusCode(result.StatusCode, result);
        }


        /// <summary>
        /// محاسبه خلاصه پیام خودکار (برای سازگاری با نسخه قبلی)
        /// </summary>
        /// <param name="automatedMessageId">شناسه پیام خودکار</param>
        /// <param name="summaryDto">اطلاعات برای محاسبه خلاصه</param>
        /// <returns>پاسخ شامل خلاصه محاسبه شده</returns>
        /// <remarks>
        /// این endpoint برای محاسبه خلاصه یک پیام خودکار استفاده می‌شود.
        /// 
        /// **نکات مهم:**
        /// - این endpoint برای سازگاری با نسخه قبلی API است
        /// - توصیه می‌شود از GET /summary استفاده کنید
        /// - این endpoint خلاصه را محاسبه و برمی‌گرداند
        /// </remarks>
        /// <response code="200">خلاصه پیام خودکار با موفقیت محاسبه شد</response>
        /// <response code="400">داده‌های ورودی نامعتبر است</response>
        /// <response code="404">پیام خودکار یافت نشد</response>
        /// <response code="500">خطای سرور</response>
        [HttpPost("{automatedMessageId}/summary/calculate")]
        [ProducesResponseType(typeof(ApiResponse<AutomatedMessageSummaryResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<AutomatedMessageSummaryResponseDto>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<AutomatedMessageSummaryResponseDto>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<AutomatedMessageSummaryResponseDto>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<AutomatedMessageSummaryResponseDto>>> CalculateAutomatedMessageSummary(
            int automatedMessageId,
            [FromBody] CalculateAutomatedMessageSummaryDto summaryDto)
        {
            if (!ModelState.IsValid)
            {
                var errors = ExtractModelStateErrors();
                return StatusCode(400, ApiResponse<AutomatedMessageSummaryResponseDto>.BadRequest("داده‌های ورودی نامعتبر است", errors));
            }

            var userId = await GetCurrentUserIdAsync();
            var result = await _automatedMessageService.CalculateAutomatedMessageSummaryAsync(automatedMessageId, userId, summaryDto);
            return StatusCode(result.StatusCode, result);
        }


        #endregion
    }
}


