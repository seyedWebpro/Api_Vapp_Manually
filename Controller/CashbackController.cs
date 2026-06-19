using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.Cashback;
using Api_Vapp.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api_Vapp.Controller
{
    /// <summary>
    /// کنترلر مدیریت کش‌بک
    /// </summary>
    /// <remarks>
    /// این کنترلر شامل تمام endpoint های مربوط به مدیریت کش‌بک‌ها می‌باشد.
    /// 
    /// **قابلیت‌های اصلی:**
    /// - ایجاد و مدیریت کش‌بک‌ها
    /// - اعمال کش‌بک به مخاطبین
    /// - مدیریت کش‌بک دستی
    /// - دریافت تاریخچه تراکنش‌های کش‌بک
    /// 
    /// **انواع کش‌بک:**
    /// - Percentage: درصدی (درصد بازگشت از مبلغ خرید)
    /// - FixedAmount: مبلغ ثابت (یک مبلغ ثابت پس از خرید)
    /// 
    /// **نوع مخاطبین:**
    /// - All: همه مخاطبین
    /// - NewContacts: مخاطبین جدید (15 روز اخیر)
    /// - SpecificNotebooks: دفترچه‌های خاص
    /// 
    /// **زمان واریز:**
    /// - Immediate: فوری
    /// - Scheduled: زمان‌بندی شده
    /// 
    /// تمام endpoint های این کنترلر نیاز به احراز هویت دارند.
    /// </remarks>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    [Produces("application/json")]
    public class CashbackController : VappControllerBase
    {
        private readonly ICashbackService _cashbackService;

        public CashbackController(
            ICashbackService cashbackService,
            IConfiguration configuration,
            IUserRepository userRepository)
            : base(configuration, userRepository)
        {
            _cashbackService = cashbackService;
        }

        /// <summary>
        /// دریافت لیست کش‌بک‌ها با pagination و فیلتر
        /// </summary>
        /// <param name="pageNumber">شماره صفحه (پیش‌فرض: 1)</param>
        /// <param name="pageSize">تعداد در هر صفحه (پیش‌فرض: 10، حداکثر: 100)</param>
        /// <param name="isActive">فیلتر بر اساس وضعیت فعال (true = فقط فعال، false = فقط غیرفعال، null = همه) (اختیاری)</param>
        /// <returns>پاسخ شامل لیست کش‌بک‌ها و اطلاعات pagination</returns>
        /// <remarks>
        /// این endpoint لیست تمام کش‌بک‌های کاربر فعلی را با امکان pagination و فیلتر برمی‌گرداند.
        /// </remarks>
        /// <response code="200">لیست کش‌بک‌ها با موفقیت برگردانده شد</response>
        /// <response code="400">پارامترهای ورودی نامعتبر است</response>
        /// <response code="500">خطای سرور</response>
        [HttpGet]
        [ProducesResponseType(typeof(ApiResponse<CashbackListDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<CashbackListDto>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<CashbackListDto>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<CashbackListDto>>> GetCashbacks(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] bool? isActive = null)
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _cashbackService.GetCashbacksAsync(userId, pageNumber, pageSize, isActive);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// دریافت لیست کش‌بک‌های فعال
        /// </summary>
        /// <returns>پاسخ شامل لیست کش‌بک‌های فعال</returns>
        /// <remarks>
        /// این endpoint لیست تمام کش‌بک‌های فعال کاربر فعلی را برمی‌گرداند.
        /// 
        /// **نکات مهم:**
        /// - فقط کش‌بک‌های فعال برگردانده می‌شوند
        /// - برای نمایش در لیست انتخاب کش‌بک استفاده می‌شود
        /// </remarks>
        /// <response code="200">لیست کش‌بک‌های فعال با موفقیت برگردانده شد</response>
        /// <response code="500">خطای سرور</response>
        [HttpGet("active")]
        [ProducesResponseType(typeof(ApiResponse<List<CashbackDto>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<List<CashbackDto>>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<List<CashbackDto>>>> GetActiveCashbacks()
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _cashbackService.GetActiveCashbacksAsync(userId);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// دریافت اطلاعات کش‌بک بر اساس شناسه
        /// </summary>
        /// <param name="id">شناسه کش‌بک</param>
        /// <returns>پاسخ شامل اطلاعات کامل کش‌بک</returns>
        /// <remarks>
        /// این endpoint اطلاعات کامل یک کش‌بک را بر اساس شناسه برمی‌گرداند.
        /// 
        /// **اطلاعات شامل:**
        /// - نوع کش‌بک (درصدی یا مبلغ ثابت)
        /// - تنظیمات و شرایط
        /// - مخاطبین هدف
        /// - وضعیت فعال/غیرفعال
        /// - تاریخ ایجاد و به‌روزرسانی
        /// </remarks>
        /// <response code="200">اطلاعات کش‌بک با موفقیت برگردانده شد</response>
        /// <response code="404">کش‌بک یافت نشد</response>
        /// <response code="500">خطای سرور</response>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(ApiResponse<CashbackDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<CashbackDto>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<CashbackDto>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<CashbackDto>>> GetCashbackById(int id)
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _cashbackService.GetCashbackByIdAsync(id, userId);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// ایجاد کش‌بک جدید
        /// </summary>
        /// <param name="createDto">اطلاعات کش‌بک جدید شامل نوع، تنظیمات و مخاطبین</param>
        /// <returns>پاسخ شامل اطلاعات کش‌بک ایجاد شده</returns>
        /// <remarks>
        /// این endpoint برای ایجاد یک کش‌بک جدید استفاده می‌شود.
        /// 
        /// **انواع کش‌بک:**
        /// - Percentage: درصدی (درصد بازگشت از مبلغ خرید)
        /// - FixedAmount: مبلغ ثابت (یک مبلغ ثابت پس از خرید)
        /// 
        /// **نوع مخاطبین:**
        /// - All: همه مخاطبین
        /// - NewContacts: مخاطبین جدید (15 روز اخیر)
        /// - SpecificNotebooks: دفترچه‌های خاص
        /// 
        /// **زمان واریز:**
        /// - Immediate: فوری
        /// - Scheduled: زمان‌بندی شده
        /// 
        /// **نکات مهم:**
        /// - کش‌بک ایجاد شده به صورت پیش‌فرض فعال است
        /// - می‌توانید بعداً از endpoint های دیگر برای ویرایش استفاده کنید
        /// </remarks>
        /// <response code="200">کش‌بک با موفقیت ایجاد شد</response>
        /// <response code="400">داده‌های ورودی نامعتبر است</response>
        /// <response code="500">خطای سرور</response>
        [HttpPost]
        [ProducesResponseType(typeof(ApiResponse<CashbackDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<CashbackDto>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<CashbackDto>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<CashbackDto>>> CreateCashback(
            [FromBody] CreateCashbackDto createDto)
        {
            if (!ModelState.IsValid)
            {
                var errors = ExtractModelStateErrors();
                return StatusCode(400, ApiResponse<CashbackDto>.BadRequest("داده‌های ورودی نامعتبر است", errors));
            }

            var userId = await GetCurrentUserIdAsync();
            var result = await _cashbackService.CreateCashbackAsync(userId, createDto);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// به‌روزرسانی کش‌بک
        /// </summary>
        /// <param name="id">شناسه کش‌بک</param>
        /// <param name="updateDto">اطلاعات به‌روزرسانی</param>
        /// <returns>پاسخ شامل اطلاعات کش‌بک به‌روزرسانی شده</returns>
        /// <remarks>
        /// این endpoint برای به‌روزرسانی اطلاعات یک کش‌بک استفاده می‌شود.
        /// 
        /// **نکات مهم:**
        /// - فقط فیلدهای ارسال شده به‌روزرسانی می‌شوند
        /// - کش‌بک باید متعلق به کاربر فعلی باشد
        /// - می‌توانید نوع، تنظیمات یا مخاطبین را به‌روزرسانی کنید
        /// </remarks>
        /// <response code="200">اطلاعات کش‌بک با موفقیت به‌روزرسانی شد</response>
        /// <response code="400">داده‌های ورودی نامعتبر است</response>
        /// <response code="404">کش‌بک یافت نشد</response>
        /// <response code="500">خطای سرور</response>
        [HttpPost("{id}/update")]
        [ProducesResponseType(typeof(ApiResponse<CashbackDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<CashbackDto>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<CashbackDto>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<CashbackDto>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<CashbackDto>>> UpdateCashback(
            int id,
            [FromBody] UpdateCashbackDto updateDto)
        {
            if (!ModelState.IsValid)
            {
                var errors = ExtractModelStateErrors();
                return StatusCode(400, ApiResponse<CashbackDto>.BadRequest("داده‌های ورودی نامعتبر است", errors));
            }

            var userId = await GetCurrentUserIdAsync();
            var result = await _cashbackService.UpdateCashbackAsync(id, userId, updateDto);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// فعال‌سازی کش‌بک
        /// </summary>
        /// <param name="id">شناسه کش‌بک</param>
        /// <returns>پاسخ شامل اطلاعات کش‌بک فعال شده</returns>
        /// <remarks>
        /// این endpoint برای فعال کردن یک کش‌بک استفاده می‌شود.
        /// 
        /// **نکات مهم:**
        /// - کش‌بک فعال می‌تواند به مخاطبین اعمال شود
        /// - کش‌بک باید متعلق به کاربر فعلی باشد
        /// </remarks>
        /// <response code="200">کش‌بک با موفقیت فعال شد</response>
        /// <response code="404">کش‌بک یافت نشد</response>
        /// <response code="500">خطای سرور</response>
        [HttpPost("{id}/activate")]
        [ProducesResponseType(typeof(ApiResponse<CashbackDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<CashbackDto>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<CashbackDto>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<CashbackDto>>> ActivateCashback(int id)
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _cashbackService.ToggleStatusAsync(id, userId, true);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// غیرفعال‌سازی کش‌بک
        /// </summary>
        /// <param name="id">شناسه کش‌بک</param>
        /// <returns>پاسخ شامل اطلاعات کش‌بک غیرفعال شده</returns>
        /// <remarks>
        /// این endpoint برای غیرفعال کردن یک کش‌بک استفاده می‌شود.
        /// 
        /// **نکات مهم:**
        /// - کش‌بک غیرفعال نمی‌تواند به مخاطبین جدید اعمال شود
        /// - کش‌بک‌های قبلاً اعمال شده همچنان معتبر هستند
        /// - کش‌بک باید متعلق به کاربر فعلی باشد
        /// </remarks>
        /// <response code="200">کش‌بک با موفقیت غیرفعال شد</response>
        /// <response code="404">کش‌بک یافت نشد</response>
        /// <response code="500">خطای سرور</response>
        [HttpPost("{id}/deactivate")]
        [ProducesResponseType(typeof(ApiResponse<CashbackDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<CashbackDto>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<CashbackDto>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<CashbackDto>>> DeactivateCashback(int id)
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _cashbackService.ToggleStatusAsync(id, userId, false);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// تغییر وضعیت کش‌بک (فعال/غیرفعال)
        /// </summary>
        /// <param name="id">شناسه کش‌بک</param>
        /// <param name="statusDto">وضعیت جدید (IsActive: true = فعال، false = غیرفعال)</param>
        /// <returns>پاسخ شامل اطلاعات کش‌بک با وضعیت جدید</returns>
        /// <remarks>
        /// این endpoint برای تغییر وضعیت فعال/غیرفعال یک کش‌بک استفاده می‌شود.
        /// 
        /// **نکات مهم:**
        /// - این endpoint جایگزین endpoint های /activate و /deactivate است
        /// - می‌توانید با یک درخواست وضعیت را تغییر دهید
        /// </remarks>
        /// <response code="200">وضعیت کش‌بک با موفقیت تغییر کرد</response>
        /// <response code="400">داده‌های ورودی نامعتبر است</response>
        /// <response code="404">کش‌بک یافت نشد</response>
        /// <response code="500">خطای سرور</response>
        [HttpPost("{id}/toggle-status")]
        [ProducesResponseType(typeof(ApiResponse<CashbackDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<CashbackDto>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<CashbackDto>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<CashbackDto>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<CashbackDto>>> ToggleStatus(
            int id,
            [FromBody] ToggleCashbackStatusDto statusDto)
        {
            if (!ModelState.IsValid)
            {
                var errors = ExtractModelStateErrors();
                return StatusCode(400, ApiResponse<CashbackDto>.BadRequest("داده‌های ورودی نامعتبر است", errors));
            }

            var userId = await GetCurrentUserIdAsync();
            var result = await _cashbackService.ToggleStatusAsync(id, userId, statusDto.IsActive);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// حذف نرم کش‌بک (Soft Delete)
        /// </summary>
        /// <param name="id">شناسه کش‌بک</param>
        /// <returns>پاسخ شامل وضعیت حذف</returns>
        /// <remarks>
        /// این endpoint برای حذف نرم یک کش‌بک استفاده می‌شود.
        /// 
        /// **نکات مهم:**
        /// - کش‌بک از دیتابیس حذف نمی‌شود، فقط علامت IsDeleted تنظیم می‌شود
        /// - کش‌بک حذف شده در لیست‌ها نمایش داده نمی‌شود
        /// - تراکنش‌های قبلی همچنان معتبر هستند
        /// - این عملیات قابل بازگشت است
        /// </remarks>
        /// <response code="200">کش‌بک با موفقیت حذف شد</response>
        /// <response code="404">کش‌بک یافت نشد</response>
        /// <response code="500">خطای سرور</response>
        [HttpPost("{id}/delete")]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<bool>>> DeleteCashback(int id)
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _cashbackService.DeleteCashbackAsync(id, userId);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// دریافت لیست دفترچه‌ها برای انتخاب در مرحله 2
        /// </summary>
        /// <returns>پاسخ شامل لیست دفترچه‌های فعال با تعداد اعضا</returns>
        /// <remarks>
        /// این endpoint لیست تمام دفترچه‌های فعال کاربر را همراه با تعداد اعضای هر دفترچه برمی‌گرداند.
        /// 
        /// **استفاده:**
        /// این endpoint برای استفاده در مرحله 2 ایجاد کش‌بک (انتخاب مخاطبین) طراحی شده است.
        /// 
        /// **اطلاعات هر دفترچه شامل:**
        /// - نام دفترچه
        /// - تعداد مخاطبین
        /// - شناسه دفترچه
        /// </remarks>
        /// <response code="200">لیست دفترچه‌ها با موفقیت برگردانده شد</response>
        /// <response code="500">خطای سرور</response>
        [HttpGet("notebooks")]
        [ProducesResponseType(typeof(ApiResponse<List<CashbackNotebookDto>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<List<CashbackNotebookDto>>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<List<CashbackNotebookDto>>>> GetNotebooks()
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _cashbackService.GetNotebooksForCashbackAsync(userId);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// اعتبارسنجی مرحله 1 ایجاد کش‌بک
        /// </summary>
        /// <param name="step1Dto">اطلاعات مرحله 1 شامل نوع کش‌بک و تنظیمات اولیه</param>
        /// <returns>پاسخ شامل نتیجه اعتبارسنجی و اطلاعات محاسبه شده</returns>
        /// <remarks>
        /// این endpoint برای اعتبارسنجی اطلاعات مرحله 1 (نوع کش‌بک و تنظیمات اولیه) استفاده می‌شود.
        /// 
        /// **برای کش‌بک درصدی:**
        /// - درصد کش‌بک (1-50) الزامی است
        /// - مبلغ کل خرید (اختیاری) برای محاسبه مبلغ کش‌بک
        /// 
        /// **برای کش‌بک مبلغ ثابت:**
        /// - مبلغ ثابت (1,000 - 10,000,000 تومان) الزامی است
        /// 
        /// **مدت اعتبار:**
        /// - مدت اعتبار (1-365 روز) برای هر دو نوع الزامی است
        /// 
        /// **نکات مهم:**
        /// - این endpoint فقط اعتبارسنجی می‌کند و داده‌ای ذخیره نمی‌کند
        /// - در صورت اعتبارسنجی موفق، اطلاعات محاسبه شده برگردانده می‌شود
        /// </remarks>
        /// <response code="200">اعتبارسنجی با موفقیت انجام شد</response>
        /// <response code="400">داده‌های ورودی نامعتبر است</response>
        /// <response code="500">خطای سرور</response>
        [HttpPost("validate-step1")]
        [ProducesResponseType(typeof(ApiResponse<CashbackStep1ValidationResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<CashbackStep1ValidationResponseDto>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<CashbackStep1ValidationResponseDto>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<CashbackStep1ValidationResponseDto>>> ValidateStep1(
            [FromBody] CashbackStep1Dto step1Dto)
        {
            if (!ModelState.IsValid)
            {
                var errors = ExtractModelStateErrors();
                return StatusCode(400, ApiResponse<CashbackStep1ValidationResponseDto>.BadRequest("داده‌های ورودی نامعتبر است", errors));
            }

            var userId = await GetCurrentUserIdAsync();
            var result = await _cashbackService.ValidateCashbackStep1Async(userId, step1Dto);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// اعتبارسنجی مرحله 2 ایجاد کش‌بک
        /// </summary>
        /// <param name="step2Dto">اطلاعات مرحله 2 شامل نوع مخاطبین و دفترچه‌های انتخاب شده</param>
        /// <returns>پاسخ شامل نتیجه اعتبارسنجی و تعداد مخاطبین هدف</returns>
        /// <remarks>
        /// این endpoint برای اعتبارسنجی اطلاعات مرحله 2 (انتخاب مخاطبین) استفاده می‌شود.
        /// 
        /// **نوع مخاطبین:**
        /// - All: همه مخاطبین
        /// - NewContacts: مخاطبین جدید (15 روز اخیر)
        /// - SpecificNotebooks: دفترچه‌های خاص (نیاز به ارسال TargetNotebookIds)
        /// 
        /// **نکات مهم:**
        /// - در صورت انتخاب SpecificNotebooks، حداقل یک دفترچه باید انتخاب شود
        /// - این endpoint فقط اعتبارسنجی می‌کند و داده‌ای ذخیره نمی‌کند
        /// - تعداد مخاطبین هدف در پاسخ برگردانده می‌شود
        /// </remarks>
        /// <response code="200">اعتبارسنجی با موفقیت انجام شد</response>
        /// <response code="400">داده‌های ورودی نامعتبر است</response>
        /// <response code="500">خطای سرور</response>
        [HttpPost("validate-step2")]
        [ProducesResponseType(typeof(ApiResponse<CashbackStep2ValidationResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<CashbackStep2ValidationResponseDto>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<CashbackStep2ValidationResponseDto>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<CashbackStep2ValidationResponseDto>>> ValidateStep2(
            [FromBody] CashbackStep2Dto step2Dto)
        {
            if (!ModelState.IsValid)
            {
                var errors = ExtractModelStateErrors();
                return StatusCode(400, ApiResponse<CashbackStep2ValidationResponseDto>.BadRequest("داده‌های ورودی نامعتبر است", errors));
            }

            var userId = await GetCurrentUserIdAsync();
            var result = await _cashbackService.ValidateCashbackStep2Async(userId, step2Dto);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// دریافت خلاصه کش‌بک برای نمایش در پایین صفحه (مرحله 3)
        /// </summary>
        /// <param name="draftId">شناسه پیش‌نویس کش‌بک (الزامی)</param>
        /// <returns>پاسخ شامل خلاصه کش‌بک</returns>
        /// <remarks>
        /// این endpoint خلاصه ساده کش‌بک را بر اساس اطلاعات مرحله 1 و 2 (و تنظیمات مرحله 3 در صورت وجود) برمی‌گرداند.
        /// 
        /// **استفاده:**
        /// برای نمایش در بخش "خلاصه" صفحه مرحله 3 استفاده می‌شود.
        /// 
        /// **نکات مهم:**
        /// - این endpoint فقط برای خواندن است و هیچ داده‌ای را ذخیره نمی‌کند
        /// - draftId الزامی است
        /// </remarks>
        /// <response code="200">خلاصه کش‌بک با موفقیت برگردانده شد</response>
        /// <response code="400">draftId الزامی است</response>
        /// <response code="500">خطای سرور</response>
        [HttpGet("summary")]
        [ProducesResponseType(typeof(ApiResponse<CashbackSummaryDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<CashbackSummaryDto>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<CashbackSummaryDto>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<CashbackSummaryDto>>> GetSummary(
            [FromQuery] string? draftId = null)
        {
            if (string.IsNullOrEmpty(draftId))
            {
                return StatusCode(400, ApiResponse<CashbackSummaryDto>.BadRequest("draftId الزامی است"));
            }

            var userId = await GetCurrentUserIdAsync();
            var request = new GetCashbackSummaryRequestDto { DraftId = draftId };
            var result = await _cashbackService.GetCashbackSummaryAsync(userId, request);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// ذخیره تنظیمات مرحله 3 (فیلتر تگ و زمان ارسال)
        /// </summary>
        /// <param name="request">تنظیمات مرحله 3 شامل فیلتر تگ و زمان ارسال</param>
        /// <returns>پاسخ شامل خلاصه به‌روز شده</returns>
        /// <remarks>
        /// این endpoint تنظیمات مرحله 3 را ذخیره می‌کند و خلاصه به‌روز شده را برمی‌گرداند.
        /// 
        /// **تنظیمات شامل:**
        /// - فیلتر تگ (اختیاری)
        /// - زمان ارسال (اختیاری)
        /// </remarks>
        /// <response code="200">تنظیمات با موفقیت ذخیره شد</response>
        /// <response code="400">داده‌های ورودی نامعتبر است</response>
        /// <response code="500">خطای سرور</response>
        [HttpPost("settings/save")]
        [ProducesResponseType(typeof(ApiResponse<CashbackSummaryDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<CashbackSummaryDto>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<CashbackSummaryDto>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<CashbackSummaryDto>>> SaveStep3Settings(
            [FromBody] SaveCashbackStep3RequestDto request)
        {
            if (!ModelState.IsValid)
            {
                var errors = ExtractModelStateErrors();
                return StatusCode(400, ApiResponse<CashbackSummaryDto>.BadRequest("داده‌های ورودی نامعتبر است", errors));
            }

            var userId = await GetCurrentUserIdAsync();
            var result = await _cashbackService.SaveCashbackStep3SettingsAsync(userId, request);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// اعمال کش‌بک به یک مخاطب خاص (حالت خرید در مغازه)
        /// </summary>
        /// <param name="request">اطلاعات اعمال کش‌بک شامل شناسه مخاطب و مبلغ خرید</param>
        /// <returns>پاسخ شامل نتیجه اعمال کش‌بک</returns>
        /// <remarks>
        /// این endpoint برای زمانی استفاده می‌شود که مشتری در مغازه خرید می‌کند و کاربر می‌خواهد کش‌بک را به او ارسال کند.
        /// 
        /// **نکات مهم:**
        /// - اگر CashbackId ارسال نشود، از اولین کش‌بک فعال استفاده می‌شود
        /// - برای کش‌بک درصدی، PurchaseAmount الزامی است
        /// - کش‌بک به کیف پول مخاطب اضافه می‌شود
        /// - پیامک اطلاع‌رسانی به مخاطب ارسال می‌شود
        /// </remarks>
        /// <response code="200">کش‌بک با موفقیت اعمال شد</response>
        /// <response code="400">داده‌های ورودی نامعتبر است</response>
        /// <response code="404">مخاطب یا کش‌بک یافت نشد</response>
        /// <response code="500">خطای سرور</response>
        [HttpPost("apply-to-contact")]
        [ProducesResponseType(typeof(ApiResponse<ApplyCashbackToContactResultDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<ApplyCashbackToContactResultDto>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<ApplyCashbackToContactResultDto>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<ApplyCashbackToContactResultDto>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<ApplyCashbackToContactResultDto>>> ApplyCashbackToContact(
            [FromBody] ApplyCashbackToContactDto request)
        {
            if (!ModelState.IsValid)
            {
                var errors = ExtractModelStateErrors();
                return StatusCode(400, ApiResponse<ApplyCashbackToContactResultDto>.BadRequest("داده‌های ورودی نامعتبر است", errors));
            }

            var userId = await GetCurrentUserIdAsync();
            var result = await _cashbackService.ApplyCashbackToContactAsync(userId, request);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// اعمال کش‌بک به مخاطبین و ارسال پیامک
        /// </summary>
        /// <param name="id">شناسه کش‌بک</param>
        /// <param name="purchaseAmount">مبلغ خرید (برای کش‌بک درصدی الزامی است)</param>
        /// <returns>پاسخ شامل نتیجه اعمال کش‌بک و آمار</returns>
        /// <remarks>
        /// این endpoint کش‌بک را به تمام مخاطبین تعیین شده اعمال می‌کند:
        /// 1. محاسبه مبلغ کش‌بک برای هر مخاطب (بر اساس نوع کش‌بک)
        /// 2. ایجاد تراکنش کش‌بک
        /// 3. ارسال پیامک به مخاطب
        /// 4. کسر هزینه ارسال پیامک از کیف پول
        /// 
        /// **نکات مهم:**
        /// - برای کش‌بک درصدی، باید purchaseAmount ارسال شود
        /// - برای کش‌بک مبلغ ثابت، purchaseAmount اختیاری است
        /// - موجودی کیف پول باید برای ارسال پیامک کافی باشد
        /// </remarks>
        /// <response code="200">کش‌بک با موفقیت به مخاطبین اعمال شد</response>
        /// <response code="400">داده‌های ورودی نامعتبر است یا مبلغ خرید برای کش‌بک درصدی ارسال نشده</response>
        /// <response code="402">موجودی کیف پول برای ارسال پیامک کافی نیست</response>
        /// <response code="404">کش‌بک یافت نشد</response>
        /// <response code="500">خطای سرور</response>
        [HttpPost("{id}/apply")]
        [ProducesResponseType(typeof(ApiResponse<ApplyCashbackResultDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<ApplyCashbackResultDto>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<ApplyCashbackResultDto>), StatusCodes.Status402PaymentRequired)]
        [ProducesResponseType(typeof(ApiResponse<ApplyCashbackResultDto>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<ApplyCashbackResultDto>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<ApplyCashbackResultDto>>> ApplyCashback(
            int id,
            [FromQuery] decimal? purchaseAmount = null)
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _cashbackService.ApplyCashbackAsync(id, userId, purchaseAmount);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// دریافت تراکنش‌های یک کش‌بک با pagination
        /// </summary>
        /// <param name="id">شناسه کش‌بک</param>
        /// <param name="pageNumber">شماره صفحه (پیش‌فرض: 1)</param>
        /// <param name="pageSize">تعداد در هر صفحه (پیش‌فرض: 10، حداکثر: 100)</param>
        /// <returns>پاسخ شامل لیست تراکنش‌های کش‌بک</returns>
        /// <remarks>
        /// این endpoint لیست تمام تراکنش‌های یک کش‌بک را با امکان pagination برمی‌گرداند.
        /// 
        /// **اطلاعات هر تراکنش شامل:**
        /// - شناسه مخاطب
        /// - مبلغ کش‌بک
        /// - تاریخ و زمان
        /// - وضعیت
        /// </remarks>
        /// <response code="200">لیست تراکنش‌ها با موفقیت برگردانده شد</response>
        /// <response code="404">کش‌بک یافت نشد</response>
        /// <response code="500">خطای سرور</response>
        [HttpGet("{id}/transactions")]
        [ProducesResponseType(typeof(ApiResponse<List<CashbackTransactionDto>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<List<CashbackTransactionDto>>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<List<CashbackTransactionDto>>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<List<CashbackTransactionDto>>>> GetCashbackTransactions(
            int id,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10)
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _cashbackService.GetCashbackTransactionsAsync(id, userId, pageNumber, pageSize);
            return StatusCode(result.StatusCode, result);
        }

        #region Manual Cashback Endpoints

        /// <summary>
        /// دریافت خلاصه کش‌بک یک مخاطب
        /// </summary>
        /// <param name="contactId">شناسه مخاطب</param>
        /// <returns>پاسخ شامل اطلاعات کامل کش‌بک مخاطب</returns>
        /// <remarks>
        /// این endpoint اطلاعات کامل کش‌بک یک مخاطب را برمی‌گرداند:
        /// - کش‌بک فعلی (موجودی کل)
        /// - کش‌بک قابل استفاده
        /// - روزهای باقیمانده تا انقضا
        /// - درصد کش‌بک فعال
        /// </remarks>
        /// <response code="200">خلاصه کش‌بک مخاطب با موفقیت برگردانده شد</response>
        /// <response code="404">مخاطب یافت نشد</response>
        /// <response code="500">خطای سرور</response>
        [HttpGet("contact/{contactId}/summary")]
        [ProducesResponseType(typeof(ApiResponse<ContactCashbackSummaryDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<ContactCashbackSummaryDto>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<ContactCashbackSummaryDto>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<ContactCashbackSummaryDto>>> GetContactCashbackSummary(int contactId)
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _cashbackService.GetContactCashbackSummaryAsync(userId, contactId);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// افزودن دستی کش‌بک به مخاطب
        /// </summary>
        /// <param name="request">اطلاعات افزودن کش‌بک دستی</param>
        /// <returns>پاسخ شامل نتیجه افزودن کش‌بک</returns>
        /// <remarks>
        /// با این endpoint می‌توانید به صورت دستی کش‌بک به یک مخاطب اضافه کنید.
        /// 
        /// **پارامترها:**
        /// - ContactId: شناسه مخاطب (الزامی)
        /// - Amount: مبلغ کش‌بک به تومان (الزامی، 1,000 تا 100,000,000)
        /// - Description: توضیحات (اختیاری)
        /// - ValidityDays: روزهای اعتبار (اختیاری، پیش‌فرض: 30 روز)
        /// 
        /// **نکات مهم:**
        /// - کش‌بک به کیف پول مخاطب اضافه می‌شود
        /// - می‌توانید توضیحات دلخواه اضافه کنید
        /// </remarks>
        /// <response code="200">کش‌بک با موفقیت به مخاطب اضافه شد</response>
        /// <response code="400">داده‌های ورودی نامعتبر است یا مبلغ خارج از محدوده است</response>
        /// <response code="404">مخاطب یافت نشد</response>
        /// <response code="500">خطای سرور</response>
        [HttpPost("contact/add-manual")]
        [ProducesResponseType(typeof(ApiResponse<AddManualCashbackResultDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<AddManualCashbackResultDto>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<AddManualCashbackResultDto>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<AddManualCashbackResultDto>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<AddManualCashbackResultDto>>> AddManualCashback(
            [FromBody] AddManualCashbackDto request)
        {
            if (!ModelState.IsValid)
            {
                var errors = ExtractModelStateErrors();
                return StatusCode(400, ApiResponse<AddManualCashbackResultDto>.BadRequest("داده‌های ورودی نامعتبر است", errors));
            }

            var userId = await GetCurrentUserIdAsync();
            var result = await _cashbackService.AddManualCashbackAsync(userId, request);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// برداشت کش‌بک از مخاطب
        /// </summary>
        /// <param name="request">اطلاعات برداشت کش‌بک</param>
        /// <returns>پاسخ شامل نتیجه برداشت</returns>
        /// <remarks>
        /// با این endpoint می‌توانید کش‌بک یک مخاطب را کاهش دهید.
        /// 
        /// **پارامترها:**
        /// - ContactId: شناسه مخاطب (الزامی)
        /// - Amount: مبلغ برداشت به تومان (الزامی، نباید بیشتر از موجودی فعلی باشد)
        /// - Reason: دلیل برداشت (اختیاری)
        /// 
        /// **نکات مهم:**
        /// - مبلغ برداشت نمی‌تواند بیشتر از موجودی فعلی کش‌بک مخاطب باشد
        /// - می‌توانید دلیل برداشت را ثبت کنید
        /// </remarks>
        /// <response code="200">کش‌بک با موفقیت از مخاطب برداشت شد</response>
        /// <response code="400">داده‌های ورودی نامعتبر است یا مبلغ بیشتر از موجودی است</response>
        /// <response code="404">مخاطب یافت نشد</response>
        /// <response code="500">خطای سرور</response>
        [HttpPost("contact/withdraw")]
        [ProducesResponseType(typeof(ApiResponse<WithdrawCashbackResultDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<WithdrawCashbackResultDto>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<WithdrawCashbackResultDto>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<WithdrawCashbackResultDto>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<WithdrawCashbackResultDto>>> WithdrawCashback(
            [FromBody] WithdrawCashbackDto request)
        {
            if (!ModelState.IsValid)
            {
                var errors = ExtractModelStateErrors();
                return StatusCode(400, ApiResponse<WithdrawCashbackResultDto>.BadRequest("داده‌های ورودی نامعتبر است", errors));
            }

            var userId = await GetCurrentUserIdAsync();
            var result = await _cashbackService.WithdrawCashbackAsync(userId, request);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// دریافت تاریخچه تراکنش‌های کش‌بک دستی مخاطب با pagination
        /// </summary>
        /// <param name="contactId">شناسه مخاطب</param>
        /// <param name="pageNumber">شماره صفحه (پیش‌فرض: 1)</param>
        /// <param name="pageSize">تعداد در هر صفحه (پیش‌فرض: 10، حداکثر: 100)</param>
        /// <returns>پاسخ شامل لیست تراکنش‌های کش‌بک دستی</returns>
        /// <remarks>
        /// این endpoint تاریخچه تمام تراکنش‌های کش‌بک دستی (افزودن و برداشت) یک مخاطب را برمی‌گرداند.
        /// 
        /// **اطلاعات هر تراکنش شامل:**
        /// - نوع تراکنش (افزودن یا برداشت)
        /// - مبلغ
        /// - تاریخ و زمان
        /// - توضیحات
        /// </remarks>
        /// <response code="200">لیست تراکنش‌ها با موفقیت برگردانده شد</response>
        /// <response code="404">مخاطب یافت نشد</response>
        /// <response code="500">خطای سرور</response>
        [HttpGet("contact/{contactId}/transactions")]
        [ProducesResponseType(typeof(ApiResponse<ManualCashbackTransactionListDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<ManualCashbackTransactionListDto>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<ManualCashbackTransactionListDto>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<ManualCashbackTransactionListDto>>> GetManualCashbackTransactions(
            int contactId,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10)
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _cashbackService.GetManualCashbackTransactionsAsync(userId, contactId, pageNumber, pageSize);
            return StatusCode(result.StatusCode, result);
        }

        #endregion

    }
}




