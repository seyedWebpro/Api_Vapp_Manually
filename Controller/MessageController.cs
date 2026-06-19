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
    /// کنترلر مدیریت پیام‌ها، کمپین‌ها و قالب‌ها
    /// </summary>
    /// <remarks>
    /// این کنترلر شامل تمام endpoint های مربوط به مدیریت پیام‌ها، کمپین‌های بازاریابی، قالب‌های پیام، 
    /// گروه‌های قالب، تگ‌ها، انتخاب گیرندگان و گزارش‌گیری می‌باشد.
    /// 
    /// **قابلیت‌های اصلی:**
    /// - ایجاد و مدیریت پیام‌ها
    /// - ایجاد و مدیریت کمپین‌های بازاریابی
    /// - مدیریت قالب‌های پیام و گروه‌های قالب
    /// - شخصی‌سازی پیام‌ها با Placeholder ها
    /// - انتخاب گیرندگان بر اساس تگ، دفترچه یا لیست
    /// - گزارش‌گیری جامع از عملکرد کمپین‌ها
    /// 
    /// تمام endpoint های این کنترلر نیاز به احراز هویت دارند.
    /// </remarks>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    [Produces("application/json")]
    public class MessageController : VappControllerBase
    {
        private readonly IMessageService _messageService;

        public MessageController(IMessageService messageService, IConfiguration configuration, IUserRepository userRepository)
            : base(configuration, userRepository)
        {
            _messageService = messageService;
        }

        #region Message Operations

        /// <summary>
        /// ایجاد پیام جدید
        /// </summary>
        /// <param name="createDto">اطلاعات پیام جدید شامل محتوا، عنوان و تنظیمات</param>
        /// <returns>پاسخ شامل اطلاعات پیام ایجاد شده</returns>
        /// <remarks>
        /// این endpoint برای ایجاد یک پیام جدید استفاده می‌شود.
        /// 
        /// **نکات مهم:**
        /// - می‌توانید از Placeholder ها برای شخصی‌سازی استفاده کنید (مثلاً {FullName}, {MobileNumber})
        /// - پیام ایجاد شده می‌تواند در کمپین‌ها استفاده شود
        /// - می‌توانید از قالب‌های موجود استفاده کنید یا پیام جدید ایجاد کنید
        /// </remarks>
        /// <response code="200">پیام با موفقیت ایجاد شد</response>
        /// <response code="400">داده‌های ورودی نامعتبر است</response>
        /// <response code="500">خطای سرور</response>
        [HttpPost]
        [ProducesResponseType(typeof(ApiResponse<MessageResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<MessageResponseDto>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<MessageResponseDto>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<MessageResponseDto>>> CreateMessage([FromBody] CreateMessageDto createDto)
        {
            if (!ModelState.IsValid)
            {
                var errors = ExtractModelStateErrors();
                return StatusCode(400, ApiResponse<MessageResponseDto>.BadRequest("داده‌های ورودی نامعتبر است", errors));
            }

            var userId = await GetCurrentUserIdAsync();
            var result = await _messageService.CreateMessageAsync(userId, createDto);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// دریافت اطلاعات پیام بر اساس شناسه
        /// </summary>
        /// <param name="id">شناسه پیام</param>
        /// <returns>پاسخ شامل اطلاعات کامل پیام</returns>
        /// <remarks>
        /// این endpoint برای دریافت اطلاعات کامل یک پیام بر اساس شناسه استفاده می‌شود.
        /// 
        /// **اطلاعات برگردانده شده شامل:**
        /// - محتوای پیام
        /// - عنوان
        /// - Placeholder های استفاده شده
        /// - تاریخ ایجاد و به‌روزرسانی
        /// - اطلاعات کمپین‌های مرتبط
        /// </remarks>
        /// <response code="200">اطلاعات پیام با موفقیت برگردانده شد</response>
        /// <response code="404">پیام یافت نشد</response>
        /// <response code="500">خطای سرور</response>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(ApiResponse<MessageResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<MessageResponseDto>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<MessageResponseDto>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<MessageResponseDto>>> GetMessageById(int id)
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _messageService.GetMessageByIdAsync(id, userId);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// دریافت لیست پیام‌ها با pagination و جستجو
        /// </summary>
        /// <param name="pageNumber">شماره صفحه (پیش‌فرض: 1)</param>
        /// <param name="pageSize">تعداد آیتم در هر صفحه (پیش‌فرض: 10، حداکثر: 100)</param>
        /// <param name="searchTerm">عبارت جستجو برای فیلتر کردن بر اساس عنوان یا محتوا (اختیاری)</param>
        /// <returns>پاسخ شامل لیست پیام‌ها و اطلاعات pagination</returns>
        /// <remarks>
        /// این endpoint برای دریافت لیست پیام‌های کاربر فعلی با امکان pagination و جستجو استفاده می‌شود.
        /// 
        /// **نکات مهم:**
        /// - فقط پیام‌های کاربر فعلی برگردانده می‌شوند
        /// - جستجو بر اساس عنوان و محتوای پیام انجام می‌شود
        /// </remarks>
        /// <response code="200">لیست پیام‌ها با موفقیت برگردانده شد</response>
        /// <response code="400">پارامترهای ورودی نامعتبر است</response>
        /// <response code="500">خطای سرور</response>
        [HttpGet]
        [ProducesResponseType(typeof(ApiResponse<MessageListResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<MessageListResponseDto>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<MessageListResponseDto>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<MessageListResponseDto>>> GetMessages(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? searchTerm = null)
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _messageService.GetMessagesAsync(userId, pageNumber, pageSize, searchTerm);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// به‌روزرسانی پیام (فیلدهای اختیاری - فقط فیلدهای ارسال شده به‌روزرسانی می‌شوند)
        /// </summary>
        /// <param name="id">شناسه پیام</param>
        /// <param name="updateDto">اطلاعات به‌روزرسانی</param>
        /// <returns>پاسخ شامل اطلاعات پیام به‌روزرسانی شده</returns>
        /// <remarks>
        /// این endpoint برای به‌روزرسانی اطلاعات یک پیام استفاده می‌شود.
        /// 
        /// **نکات مهم:**
        /// - فقط فیلدهای ارسال شده به‌روزرسانی می‌شوند
        /// - پیام باید متعلق به کاربر فعلی باشد
        /// - در صورت استفاده از پیام در کمپین فعال، تغییرات اعمال می‌شود
        /// </remarks>
        /// <response code="200">اطلاعات پیام با موفقیت به‌روزرسانی شد</response>
        /// <response code="400">داده‌های ورودی نامعتبر است</response>
        /// <response code="404">پیام یافت نشد</response>
        /// <response code="500">خطای سرور</response>
        [HttpPost("{id}/update")]
        [ProducesResponseType(typeof(ApiResponse<MessageResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<MessageResponseDto>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<MessageResponseDto>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<MessageResponseDto>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<MessageResponseDto>>> UpdateMessage(int id, [FromBody] UpdateMessageDto updateDto)
        {
            if (!ModelState.IsValid)
            {
                var errors = ExtractModelStateErrors();
                return StatusCode(400, ApiResponse<MessageResponseDto>.BadRequest("داده‌های ورودی نامعتبر است", errors));
            }

            var userId = await GetCurrentUserIdAsync();
            var result = await _messageService.UpdateMessageAsync(id, userId, updateDto);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// حذف پیام
        /// </summary>
        /// <param name="id">شناسه پیام</param>
        /// <returns>پاسخ شامل وضعیت حذف</returns>
        /// <remarks>
        /// این endpoint برای حذف یک پیام استفاده می‌شود.
        /// 
        /// **نکات مهم:**
        /// - پیام باید متعلق به کاربر فعلی باشد
        /// - در صورت استفاده از پیام در کمپین فعال، حذف انجام نمی‌شود
        /// - این عملیات قابل بازگشت نیست
        /// </remarks>
        /// <response code="200">پیام با موفقیت حذف شد</response>
        /// <response code="404">پیام یافت نشد</response>
        /// <response code="409">پیام در کمپین فعال استفاده می‌شود</response>
        /// <response code="500">خطای سرور</response>
        [HttpPost("{id}/delete")]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<bool>>> DeleteMessage(int id)
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _messageService.DeleteMessageAsync(id, userId);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// دریافت پیش‌نمایش پیام
        /// </summary>
        /// <param name="id">شناسه پیام</param>
        /// <returns>پاسخ شامل پیش‌نمایش پیام</returns>
        /// <remarks>
        /// این endpoint برای دریافت پیش‌نمایش یک پیام استفاده می‌شود.
        /// 
        /// **پیش‌نمایش شامل:**
        /// - محتوای پیام با جایگزینی Placeholder ها با نمونه داده
        /// - تعداد کاراکترها
        /// - تعداد SMS مورد نیاز (بر اساس طول پیام)
        /// 
        /// **نکات مهم:**
        /// - Placeholder ها با داده‌های نمونه جایگزین می‌شوند
        /// - این endpoint فقط برای نمایش است و تغییری در پیام ایجاد نمی‌کند
        /// </remarks>
        /// <response code="200">پیش‌نمایش پیام با موفقیت برگردانده شد</response>
        /// <response code="404">پیام یافت نشد</response>
        /// <response code="500">خطای سرور</response>
        [HttpGet("{id}/preview")]
        [ProducesResponseType(typeof(ApiResponse<MessagePreviewDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<MessagePreviewDto>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<MessagePreviewDto>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<MessagePreviewDto>>> GetMessagePreview(int id)
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _messageService.GetMessagePreviewAsync(id, userId);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// شخصی‌سازی پیام با جایگزینی Placeholder ها
        /// </summary>
        /// <param name="id">شناسه پیام</param>
        /// <param name="personalizeDto">اطلاعات شخصی‌سازی شامل مقادیر Placeholder ها و گزینه ذخیره</param>
        /// <returns>پاسخ شامل پیام شخصی‌سازی شده</returns>
        /// <remarks>
        /// این endpoint برای شخصی‌سازی یک پیام با جایگزینی Placeholder ها با مقادیر واقعی استفاده می‌شود.
        /// 
        /// **Placeholder های رایج:**
        /// - {FullName}: نام کامل مخاطب
        /// - {MobileNumber}: شماره موبایل
        /// - {Brand}: برند
        /// - {Date}: تاریخ امروز
        /// 
        /// **نکات مهم:**
        /// - می‌توانید انتخاب کنید که پیام شخصی‌سازی شده در دیتابیس ذخیره شود یا فقط پیش‌نمایش باشد
        /// - اگر SaveToMessage = true باشد، پیام اصلی به‌روزرسانی می‌شود
        /// </remarks>
        /// <response code="200">پیام با موفقیت شخصی‌سازی شد</response>
        /// <response code="400">داده‌های ورودی نامعتبر است</response>
        /// <response code="404">پیام یافت نشد</response>
        /// <response code="500">خطای سرور</response>
        [HttpPost("{id}/personalize")]
        [ProducesResponseType(typeof(ApiResponse<PersonalizedMessageResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<PersonalizedMessageResponseDto>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<PersonalizedMessageResponseDto>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<PersonalizedMessageResponseDto>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<PersonalizedMessageResponseDto>>> PersonalizeMessage(int id, [FromBody] PersonalizeMessageDto personalizeDto)
        {
            if (!ModelState.IsValid)
            {
                var errors = ExtractModelStateErrors();
                return StatusCode(400, ApiResponse<PersonalizedMessageResponseDto>.BadRequest("داده‌های ورودی نامعتبر است", errors));
            }

            var userId = await GetCurrentUserIdAsync();
            var result = await _messageService.PersonalizeMessageAsync(id, userId, personalizeDto.PlaceholderValues, personalizeDto.SaveToMessage);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// ارسال پیام سریع با قالب پیش‌فرض
        /// </summary>
        /// <param name="quickSendDto">اطلاعات شامل شناسه مخاطب</param>
        /// <returns>پاسخ شامل نتیجه ارسال پیام</returns>
        /// <remarks>
        /// این endpoint برای ارسال سریع پیام با استفاده از قالب پیش‌فرض کاربر استفاده می‌شود.
        /// 
        /// **فرآیند ارسال:**
        /// 1. بررسی وجود مخاطب و تعلق آن به کاربر
        /// 2. پیدا کردن قالب پیش‌فرض کاربر
        /// 3. ایجاد پیام با محتوای قالب پیش‌فرض
        /// 4. شخصی‌سازی پیام با اطلاعات مخاطب (جایگزینی Placeholder ها)
        /// 5. ارسال مستقیم پیام به مخاطب
        /// 
        /// **نکات مهم:**
        /// - مخاطب باید متعلق به کاربر باشد
        /// - کاربر باید یک قالب پیش‌فرض تنظیم کرده باشد
        /// - پیام به صورت خودکار شخصی‌سازی می‌شود (مثلاً {{نام}} با نام مخاطب جایگزین می‌شود)
        /// - ارسال به صورت فوری انجام می‌شود (بدون نمایش خلاصه)
        /// 
        /// **استفاده:**
        /// این endpoint برای بخش "ارسال سریع" در تگ پرکاربرد استفاده می‌شود.
        /// کاربر می‌تواند با یک کلیک، پیام قالب پیش‌فرض را برای مخاطب انتخاب شده ارسال کند.
        /// </remarks>
        /// <response code="200">پیام با موفقیت ارسال شد</response>
        /// <response code="400">داده‌های ورودی نامعتبر است یا قالب پیش‌فرض تنظیم نشده</response>
        /// <response code="403">مخاطب متعلق به شما نیست</response>
        /// <response code="404">مخاطب یافت نشد</response>
        /// <response code="500">خطای سرور</response>
        [HttpPost("quick-send")]
        [ProducesResponseType(typeof(ApiResponse<DirectSendResultDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<DirectSendResultDto>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<DirectSendResultDto>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<DirectSendResultDto>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<DirectSendResultDto>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<DirectSendResultDto>>> QuickSendMessage([FromBody] QuickSendMessageDto quickSendDto)
        {
            if (!ModelState.IsValid)
            {
                var errors = ExtractModelStateErrors();
                return StatusCode(400, ApiResponse<DirectSendResultDto>.BadRequest("داده‌های ورودی نامعتبر است", errors));
            }

            var userId = await GetCurrentUserIdAsync();
            var result = await _messageService.QuickSendMessageAsync(userId, quickSendDto);
            return StatusCode(result.StatusCode, result);
        }

        #endregion

        #region Campaign Operations

        /// <summary>
        /// دریافت خلاصه کمپین از Session (بدون ارسال)
        /// </summary>
        /// <param name="messageId">شناسه پیام</param>
        /// <returns>پاسخ شامل خلاصه کمپین شامل تعداد گیرندگان و هزینه تخمینی</returns>
        /// <remarks>
        /// این endpoint برای دریافت خلاصه کمپین از Session استفاده می‌شود.
        /// 
        /// **خلاصه شامل:**
        /// - تعداد گیرندگان
        /// - هزینه تخمینی ارسال
        /// - تعداد SMS مورد نیاز
        /// - وضعیت کمپین
        /// 
        /// **نکات مهم:**
        /// - این endpoint فقط خلاصه را برمی‌گرداند و کمپینی ایجاد نمی‌کند
        /// - برای ایجاد کمپین از endpoint /campaign استفاده کنید
        /// </remarks>
        /// <response code="200">خلاصه کمپین با موفقیت برگردانده شد</response>
        /// <response code="404">پیام یافت نشد</response>
        /// <response code="500">خطای سرور</response>
        [HttpGet("{messageId}/campaign/summary")]
        [ProducesResponseType(typeof(ApiResponse<CampaignSummaryDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<CampaignSummaryDto>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<CampaignSummaryDto>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<CampaignSummaryDto>>> GetCampaignSummary(int messageId)
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _messageService.GetCampaignSummaryAsync(userId, messageId);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// محاسبه خلاصه کمپین و ارسال (در صورت AutoSend = true)
        /// </summary>
        /// <param name="campaignDto">اطلاعات کمپین شامل پیام، گیرندگان و تنظیمات ارسال</param>
        /// <returns>پاسخ شامل خلاصه کمپین و در صورت AutoSend = true، نتیجه ارسال</returns>
        /// <remarks>
        /// این endpoint برای محاسبه خلاصه کمپین و در صورت فعال بودن AutoSend، ارسال فوری کمپین استفاده می‌شود.
        /// 
        /// **انواع ارسال (SendType):**
        /// - Immediate: ارسال فوری
        /// - Scheduled: ارسال زمان‌دار (نیاز به ScheduledAt)
        /// 
        /// **نکات مهم:**
        /// - برای ارسال زمان‌دار، ScheduledAt باید مشخص شود
        /// - می‌توانید Idempotency-Key را در header ارسال کنید برای جلوگیری از ارسال تکراری
        /// - اگر AutoSend = true باشد، کمپین بلافاصله ارسال می‌شود
        /// </remarks>
        /// <response code="200">خلاصه کمپین محاسبه شد و در صورت AutoSend، ارسال انجام شد</response>
        /// <response code="400">داده‌های ورودی نامعتبر است یا ScheduledAt برای ارسال زمان‌دار مشخص نشده</response>
        /// <response code="402">موجودی کیف پول کافی نیست</response>
        /// <response code="500">خطای سرور</response>
        [HttpPost("campaign/calculate-summary")]
        [ProducesResponseType(typeof(ApiResponse<CampaignSummaryDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<CampaignSummaryDto>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<CampaignSummaryDto>), StatusCodes.Status402PaymentRequired)]
        [ProducesResponseType(typeof(ApiResponse<CampaignSummaryDto>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<CampaignSummaryDto>>> CalculateCampaignSummary([FromBody] CreateCampaignDto campaignDto)
        {
            // بررسی null بودن campaignDto
            if (campaignDto == null)
            {
                return StatusCode(400, ApiResponse<CampaignSummaryDto>.BadRequest("campaignDto الزامی است"));
            }

            // اعتبارسنجی: اگر SendType = Scheduled باشد، ScheduledAt باید مشخص شود
            if (campaignDto.SendType == CampaignSendType.Scheduled && !campaignDto.ScheduledAt.HasValue)
            {
                return StatusCode(400, ApiResponse<CampaignSummaryDto>.BadRequest(
                    "برای ارسال زمان‌دار، تاریخ و زمان ارسال (scheduledAt) باید مشخص شود"));
            }

            if (!ModelState.IsValid)
            {
                var errors = ExtractModelStateErrors();
                return StatusCode(400, ApiResponse<CampaignSummaryDto>.BadRequest(
                    "خطای اعتبارسنجی اطلاعات ورودی", errors));
            }

            // خواندن Idempotency Key از Header
            var idempotencyKey = Request.Headers["Idempotency-Key"].FirstOrDefault();

            var userId = await GetCurrentUserIdAsync();
            var result = await _messageService.CalculateCampaignSummaryAsync(userId, campaignDto.MessageId, campaignDto, idempotencyKey);
            return StatusCode(result.StatusCode, result);
        }


        /// <summary>
        /// ایجاد کمپین بازاریابی جدید
        /// </summary>
        /// <param name="createDto">اطلاعات کمپین شامل پیام، گیرندگان و تنظیمات ارسال</param>
        /// <returns>پاسخ شامل اطلاعات کمپین ایجاد شده</returns>
        /// <remarks>
        /// این endpoint برای ایجاد یک کمپین بازاریابی جدید استفاده می‌شود.
        /// 
        /// **فرآیند ایجاد کمپین:**
        /// 1. انتخاب پیام
        /// 2. انتخاب گیرندگان (بر اساس تگ، دفترچه یا لیست)
        /// 3. تنظیم نوع ارسال (فوری یا زمان‌دار)
        /// 4. در صورت نیاز، تأیید و ارسال
        /// 
        /// **نکات مهم:**
        /// - کمپین ایجاد شده به صورت پیش‌فرض در وضعیت Pending است
        /// - برای ارسال کمپین از endpoint /campaign/{id}/confirm-and-send استفاده کنید
        /// - برای ارسال زمان‌دار، ScheduledAt باید مشخص شود
        /// </remarks>
        /// <response code="200">کمپین با موفقیت ایجاد شد</response>
        /// <response code="400">داده‌های ورودی نامعتبر است</response>
        /// <response code="404">پیام یا گیرندگان یافت نشدند</response>
        /// <response code="500">خطای سرور</response>
        [HttpPost("campaign")]
        [ProducesResponseType(typeof(ApiResponse<CampaignResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<CampaignResponseDto>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<CampaignResponseDto>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<CampaignResponseDto>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<CampaignResponseDto>>> CreateCampaign([FromBody] CreateCampaignDto createDto)
        {
            if (!ModelState.IsValid)
            {
                var errors = ExtractModelStateErrors();
                return StatusCode(400, ApiResponse<CampaignResponseDto>.BadRequest("داده‌های ورودی نامعتبر است", errors));
            }

            var userId = await GetCurrentUserIdAsync();
            var result = await _messageService.CreateCampaignAsync(userId, createDto);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// دریافت اطلاعات کمپین بر اساس شناسه
        /// </summary>
        /// <param name="id">شناسه کمپین</param>
        /// <returns>پاسخ شامل اطلاعات کامل کمپین</returns>
        /// <remarks>
        /// این endpoint برای دریافت اطلاعات کامل یک کمپین بر اساس شناسه استفاده می‌شود.
        /// 
        /// **اطلاعات برگردانده شده شامل:**
        /// - اطلاعات کمپین (پیام، گیرندگان، وضعیت)
        /// - آمار ارسال (موفق، ناموفق، در انتظار)
        /// - تاریخ ایجاد و ارسال
        /// - هزینه ارسال
        /// </remarks>
        /// <response code="200">اطلاعات کمپین با موفقیت برگردانده شد</response>
        /// <response code="404">کمپین یافت نشد</response>
        /// <response code="500">خطای سرور</response>
        [HttpGet("campaign/{id}")]
        [ProducesResponseType(typeof(ApiResponse<CampaignResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<CampaignResponseDto>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<CampaignResponseDto>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<CampaignResponseDto>>> GetCampaignById(int id)
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _messageService.GetCampaignByIdAsync(id, userId);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// دریافت لیست کمپین‌ها با pagination و فیلتر
        /// </summary>
        /// <param name="pageNumber">شماره صفحه (پیش‌فرض: 1)</param>
        /// <param name="pageSize">تعداد آیتم در هر صفحه (پیش‌فرض: 10، حداکثر: 100)</param>
        /// <param name="status">فیلتر بر اساس وضعیت کمپین (Pending, Sent, Cancelled و غیره) (اختیاری)</param>
        /// <returns>پاسخ شامل لیست کمپین‌ها و اطلاعات pagination</returns>
        /// <remarks>
        /// این endpoint برای دریافت لیست کمپین‌های کاربر فعلی با امکان pagination و فیلتر استفاده می‌شود.
        /// 
        /// **وضعیت‌های کمپین:**
        /// - Pending: در انتظار ارسال
        /// - Sent: ارسال شده
        /// - Cancelled: لغو شده
        /// - Failed: ناموفق
        /// </remarks>
        /// <response code="200">لیست کمپین‌ها با موفقیت برگردانده شد</response>
        /// <response code="400">پارامترهای ورودی نامعتبر است</response>
        /// <response code="500">خطای سرور</response>
        [HttpGet("campaign")]
        [ProducesResponseType(typeof(ApiResponse<CampaignListResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<CampaignListResponseDto>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<CampaignListResponseDto>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<CampaignListResponseDto>>> GetCampaigns(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? status = null)
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _messageService.GetCampaignsAsync(userId, pageNumber, pageSize, status);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// تأیید و ارسال کمپین
        /// </summary>
        /// <param name="id">شناسه کمپین</param>
        /// <returns>پاسخ شامل وضعیت ارسال</returns>
        /// <remarks>
        /// این endpoint برای تأیید و ارسال یک کمپین در وضعیت Pending استفاده می‌شود.
        /// 
        /// **فرآیند ارسال:**
        /// 1. بررسی موجودی کیف پول
        /// 2. شخصی‌سازی پیام برای هر گیرنده
        /// 3. ارسال پیام‌ها
        /// 4. به‌روزرسانی آمار کمپین
        /// 
        /// **نکات مهم:**
        /// - کمپین باید در وضعیت Pending باشد
        /// - موجودی کیف پول باید کافی باشد
        /// - پس از ارسال، وضعیت کمپین به Sent تغییر می‌کند
        /// </remarks>
        /// <response code="200">کمپین با موفقیت ارسال شد</response>
        /// <response code="400">کمپین در وضعیت نامعتبر است</response>
        /// <response code="402">موجودی کیف پول کافی نیست</response>
        /// <response code="404">کمپین یافت نشد</response>
        /// <response code="500">خطای سرور</response>
        [HttpPost("campaign/{id}/confirm-and-send")]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status402PaymentRequired)]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<bool>>> ConfirmAndSendCampaign(int id)
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _messageService.ConfirmAndSendCampaignAsync(id, userId);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// لغو کمپین
        /// </summary>
        /// <param name="id">شناسه کمپین</param>
        /// <returns>پاسخ شامل وضعیت لغو</returns>
        /// <remarks>
        /// این endpoint برای لغو یک کمپین استفاده می‌شود.
        /// 
        /// **نکات مهم:**
        /// - فقط کمپین‌های در وضعیت Pending قابل لغو هستند
        /// - کمپین‌های ارسال شده قابل لغو نیستند
        /// - پس از لغو، وضعیت کمپین به Cancelled تغییر می‌کند
        /// </remarks>
        /// <response code="200">کمپین با موفقیت لغو شد</response>
        /// <response code="400">کمپین در وضعیت نامعتبر است (قبلاً ارسال شده)</response>
        /// <response code="404">کمپین یافت نشد</response>
        /// <response code="500">خطای سرور</response>
        [HttpPost("campaign/{id}/cancel")]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<bool>>> CancelCampaign(int id)
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _messageService.CancelCampaignAsync(id, userId);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// تغییر وضعیت فعال/غیرفعال کمپین
        /// </summary>
        /// <param name="id">شناسه کمپین</param>
        /// <param name="isActive">وضعیت جدید (true = فعال، false = غیرفعال)</param>
        /// <returns>پاسخ شامل وضعیت تغییر</returns>
        /// <remarks>
        /// این endpoint برای تغییر وضعیت فعال/غیرفعال یک کمپین استفاده می‌شود.
        /// 
        /// **نکات مهم:**
        /// - کمپین غیرفعال در برخی لیست‌ها نمایش داده نمی‌شود
        /// - این با لغو کمپین متفاوت است
        /// - کمپین غیرفعال می‌تواند دوباره فعال شود
        /// </remarks>
        /// <response code="200">وضعیت کمپین با موفقیت تغییر کرد</response>
        /// <response code="404">کمپین یافت نشد</response>
        /// <response code="500">خطای سرور</response>
        [HttpPost("campaign/{id}/toggle-status")]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<bool>>> ToggleCampaignStatus(int id, [FromQuery] bool isActive)
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _messageService.ToggleCampaignStatusAsync(id, userId, isActive);
            return StatusCode(result.StatusCode, result);
        }

        #endregion


        #region Tag Operations

        /// <summary>
        /// ایجاد تگ جدید برای پیام‌ها
        /// </summary>
        /// <param name="createDto">اطلاعات تگ جدید شامل نام</param>
        /// <returns>پاسخ شامل اطلاعات تگ ایجاد شده</returns>
        /// <remarks>
        /// این endpoint برای ایجاد یک تگ جدید برای دسته‌بندی پیام‌ها استفاده می‌شود.
        /// 
        /// **نکات مهم:**
        /// - نام تگ باید منحصر به فرد باشد
        /// - تگ‌ها برای سازماندهی و فیلتر کردن پیام‌ها استفاده می‌شوند
        /// </remarks>
        /// <response code="200">تگ با موفقیت ایجاد شد</response>
        /// <response code="400">داده‌های ورودی نامعتبر است</response>
        /// <response code="409">تگ با این نام قبلاً ایجاد شده است</response>
        /// <response code="500">خطای سرور</response>
        [HttpPost("tags")]
        [ProducesResponseType(typeof(ApiResponse<MessageTagResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<MessageTagResponseDto>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<MessageTagResponseDto>), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(ApiResponse<MessageTagResponseDto>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<MessageTagResponseDto>>> CreateTag([FromBody] CreateMessageTagDto createDto)
        {
            if (!ModelState.IsValid)
            {
                var errors = ExtractModelStateErrors();
                return StatusCode(400, ApiResponse<MessageTagResponseDto>.BadRequest("داده‌های ورودی نامعتبر است", errors));
            }

            var userId = await GetCurrentUserIdAsync();
            var result = await _messageService.CreateTagAsync(userId, createDto);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// دریافت لیست تگ‌های کاربر با pagination
        /// </summary>
        /// <param name="pageNumber">شماره صفحه (پیش‌فرض: 1)</param>
        /// <param name="pageSize">تعداد آیتم در هر صفحه (پیش‌فرض: 10، حداکثر: 100)</param>
        /// <returns>پاسخ شامل لیست تگ‌ها و اطلاعات pagination</returns>
        /// <remarks>
        /// این endpoint برای دریافت لیست تگ‌های کاربر فعلی با امکان pagination استفاده می‌شود.
        /// </remarks>
        /// <response code="200">لیست تگ‌ها با موفقیت برگردانده شد</response>
        /// <response code="400">پارامترهای ورودی نامعتبر است</response>
        /// <response code="500">خطای سرور</response>
        [HttpGet("tags")]
        [ProducesResponseType(typeof(ApiResponse<MessageTagListResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<MessageTagListResponseDto>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<MessageTagListResponseDto>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<MessageTagListResponseDto>>> GetTags(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10)
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _messageService.GetTagsAsync(userId, pageNumber, pageSize);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// دریافت لیست تگ‌های کاربر همراه با تعداد مخاطبین هر تگ
        /// </summary>
        /// <param name="pageNumber">شماره صفحه (پیش‌فرض: 1)</param>
        /// <param name="pageSize">تعداد آیتم در هر صفحه (پیش‌فرض: 10، حداکثر: 100)</param>
        /// <returns>پاسخ شامل لیست تگ‌ها با تعداد مخاطبین هر تگ</returns>
        /// <remarks>
        /// این endpoint برای دریافت لیست تگ‌های کاربر همراه با تعداد مخاطبین هر تگ از تمام دفترچه‌ها استفاده می‌شود.
        /// 
        /// **استفاده:**
        /// این endpoint برای استفاده در انتخاب گیرندگان بر اساس تگ در کمپین‌ها طراحی شده است.
        /// 
        /// **اطلاعات برگردانده شده شامل:**
        /// - نام تگ
        /// - تعداد مخاطبین دارای این تگ
        /// - اطلاعات pagination
        /// </remarks>
        /// <response code="200">لیست تگ‌ها با تعداد مخاطبین با موفقیت برگردانده شد</response>
        /// <response code="400">پارامترهای ورودی نامعتبر است</response>
        /// <response code="500">خطای سرور</response>
        [HttpGet("tags/with-contact-count")]
        [ProducesResponseType(typeof(ApiResponse<MessageTagWithContactCountListResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<MessageTagWithContactCountListResponseDto>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<MessageTagWithContactCountListResponseDto>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<MessageTagWithContactCountListResponseDto>>> GetTagsWithContactCount(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10)
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _messageService.GetTagsWithContactCountAsync(userId, pageNumber, pageSize);
            return StatusCode(result.StatusCode, result);
        }

        #endregion

        #region Recipient Operations

        /// <summary>
        /// انتخاب گیرندگان برای کمپین
        /// </summary>
        /// <param name="selectDto">اطلاعات انتخاب گیرندگان شامل تگ‌ها، دفترچه‌ها یا لیست مخاطبین</param>
        /// <returns>پاسخ شامل لیست گیرندگان انتخاب شده و تعداد آن‌ها</returns>
        /// <remarks>
        /// این endpoint برای انتخاب گیرندگان یک کمپین استفاده می‌شود.
        /// 
        /// **روش‌های انتخاب گیرندگان:**
        /// - بر اساس تگ: انتخاب تمام مخاطبینی که تگ خاصی دارند
        /// - بر اساس دفترچه: انتخاب تمام مخاطبین یک یا چند دفترچه
        /// - بر اساس لیست: انتخاب مخاطبین خاص از لیست شناسه‌ها
        /// - ترکیبی: می‌توانید از چند روش همزمان استفاده کنید
        /// 
        /// **نکات مهم:**
        /// - می‌توانید فیلترهای اضافی اعمال کنید (مثلاً فقط مخاطبین فعال)
        /// - لیست گیرندگان برای استفاده در کمپین برگردانده می‌شود
        /// </remarks>
        /// <response code="200">لیست گیرندگان با موفقیت برگردانده شد</response>
        /// <response code="400">داده‌های ورودی نامعتبر است</response>
        /// <response code="404">تگ یا دفترچه یافت نشد</response>
        /// <response code="500">خطای سرور</response>
        [HttpPost("recipients/select")]
        [ProducesResponseType(typeof(ApiResponse<RecipientListResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<RecipientListResponseDto>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<RecipientListResponseDto>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<RecipientListResponseDto>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<RecipientListResponseDto>>> SelectRecipients([FromBody] SelectRecipientsDto selectDto)
        {
            if (!ModelState.IsValid)
            {
                var errors = ExtractModelStateErrors();
                return StatusCode(400, ApiResponse<RecipientListResponseDto>.BadRequest("داده‌های ورودی نامعتبر است", errors));
            }

            var userId = await GetCurrentUserIdAsync();
            var result = await _messageService.SelectRecipientsAsync(userId, selectDto);
            return StatusCode(result.StatusCode, result);
        }

        #endregion


        #region Report Operations

        /// <summary>
        /// دریافت گزارش امروز
        /// </summary>
        /// <returns>پاسخ شامل گزارش امروز شامل آمار پیام‌ها و کمپین‌ها</returns>
        /// <remarks>
        /// این endpoint برای دریافت گزارش امروز کاربر استفاده می‌شود.
        /// 
        /// **اطلاعات گزارش شامل:**
        /// - تعداد پیام‌های ارسال شده امروز
        /// - تعداد کمپین‌های فعال
        /// - هزینه ارسال امروز
        /// - آمار موفقیت/شکست
        /// - مقایسه با روز قبل
        /// </remarks>
        /// <response code="200">گزارش امروز با موفقیت برگردانده شد</response>
        /// <response code="500">خطای سرور</response>
        [HttpGet("report/today")]
        [ProducesResponseType(typeof(ApiResponse<TodayReportDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<TodayReportDto>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<TodayReportDto>>> GetTodayReport()
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _messageService.GetTodayReportAsync(userId);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// دریافت آخرین کمپین‌ها
        /// </summary>
        /// <param name="count">تعداد کمپین‌ها (پیش‌فرض: 5، حداکثر: 20)</param>
        /// <returns>پاسخ شامل لیست آخرین کمپین‌ها</returns>
        /// <remarks>
        /// این endpoint برای دریافت آخرین کمپین‌های کاربر استفاده می‌شود.
        /// 
        /// **اطلاعات هر کمپین شامل:**
        /// - عنوان و وضعیت
        /// - تعداد گیرندگان
        /// - آمار ارسال
        /// - تاریخ ایجاد و ارسال
        /// 
        /// **نکات مهم:**
        /// - کمپین‌ها به ترتیب تاریخ ایجاد (جدیدترین اول) برگردانده می‌شوند
        /// </remarks>
        /// <response code="200">لیست آخرین کمپین‌ها با موفقیت برگردانده شد</response>
        /// <response code="400">تعداد درخواستی نامعتبر است</response>
        /// <response code="500">خطای سرور</response>
        [HttpGet("report/latest-campaigns")]
        [ProducesResponseType(typeof(ApiResponse<List<LatestCampaignsDto>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<List<LatestCampaignsDto>>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<List<LatestCampaignsDto>>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<List<LatestCampaignsDto>>>> GetLatestCampaigns([FromQuery] int count = 5)
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _messageService.GetLatestCampaignsAsync(userId, count);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// دریافت گزارش جامع شامل آمار کمپین‌ها، عملکرد و بررسی داده‌ها
        /// </summary>
        /// <returns>پاسخ شامل گزارش جامع با تمام آمار و تحلیل‌ها</returns>
        /// <remarks>
        /// این endpoint برای دریافت گزارش جامع از عملکرد سیستم استفاده می‌شود.
        /// 
        /// **اطلاعات گزارش شامل:**
        /// - آمار کلی کمپین‌ها (موفق، ناموفق، در انتظار)
        /// - آمار پیام‌های ارسال شده (روزانه، هفتگی، ماهانه)
        /// - تحلیل عملکرد کمپین‌ها
        /// - آمار هزینه‌ها
        /// - نمودارها و روندها
        /// - مقایسه با دوره‌های قبلی
        /// 
        /// **نکات مهم:**
        /// - این گزارش شامل تمام داده‌های تاریخی است
        /// - برای داشبورد مدیریتی استفاده می‌شود
        /// </remarks>
        /// <response code="200">گزارش جامع با موفقیت برگردانده شد</response>
        /// <response code="500">خطای سرور</response>
        [HttpGet("report/comprehensive")]
        [ProducesResponseType(typeof(ApiResponse<ComprehensiveReportDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<ComprehensiveReportDto>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<ComprehensiveReportDto>>> GetComprehensiveReport()
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _messageService.GetComprehensiveReportAsync(userId);
            return StatusCode(result.StatusCode, result);
        }

        #endregion
    }
}


