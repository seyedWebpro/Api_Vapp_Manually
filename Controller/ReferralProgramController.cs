using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.ReferralProgram;
using Api_Vapp.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api_Vapp.Controller
{
    /// <summary>
    /// کنترلر برنامه پاداش و معرف
    /// </summary>
    /// <remarks>
    /// این کنترلر شامل endpoint های مربوط به ایجاد، مدیریت و مصرف برنامه‌های پاداش/معرف است.
    ///
    /// **قابلیت‌های اصلی:**
    /// - ویزارد سه‌مرحله‌ای ایجاد برنامه (اطلاعات پاداش، مخاطبین + تگ، تاریخ)
    /// - مدیریت برنامه‌ها (لیست، جزئیات، ویرایش، فعال/غیرفعال، حذف)
    /// - آمار داشبورد و تاریخچه مصرف کد
    /// - استعلام کد در فروشگاه (inquire) و ثبت مصرف (redeem)
    ///
    /// **انواع پاداش:**
    /// - Percentage: درصدی (نیاز به مبلغ خرید برای محاسبه)
    /// - FixedAmount: مبلغ ثابت
    ///
    /// **نوع مخاطبین:**
    /// - All: همه مخاطبین
    /// - SpecificNotebooks: دفترچه‌های خاص
    /// - Individual: انتخاب دستی مخاطبین
    ///
    /// **نکته:** هر برنامه یک کد عمومی (PublicCode) دارد که برای همه مخاطبین یکسان است.
    ///
    /// تمام endpoint های این کنترلر نیاز به احراز هویت دارند.
    /// </remarks>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    [Produces("application/json")]
    public class ReferralProgramController : VappControllerBase
    {
        private readonly IReferralProgramService _referralProgramService;

        public ReferralProgramController(
            IReferralProgramService referralProgramService,
            IConfiguration configuration,
            IUserRepository userRepository)
            : base(configuration, userRepository)
        {
            _referralProgramService = referralProgramService;
        }

        /// <summary>
        /// دریافت آمار داشبورد برنامه پاداش
        /// </summary>
        /// <returns>پاسخ شامل KPI های داشبورد (معرفی‌های موفق، پاداش پرداخت‌شده، برنامه‌های فعال و ...)</returns>
        /// <remarks>
        /// این endpoint برای نمایش کارت‌های آماری صفحه داشبورد برنامه پاداش استفاده می‌شود.
        ///
        /// **اطلاعات برگشتی:**
        /// - SuccessfulReferrals: تعداد مصرف‌های موفق کد
        /// - TotalRewardsPaid: مجموع پاداش‌های پرداخت‌شده
        /// - ActiveProgramsCount: تعداد برنامه‌های فعال
        /// - ActiveUsersCount: تعداد مخاطبین یکتا در مصرف‌ها
        /// </remarks>
        /// <response code="200">آمار با موفقیت برگردانده شد</response>
        /// <response code="500">خطای سرور</response>
        [HttpGet("dashboard/stats")]
        [ProducesResponseType(typeof(ApiResponse<ReferralDashboardStatsDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<ReferralDashboardStatsDto>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<ReferralDashboardStatsDto>>> GetDashboardStats()
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _referralProgramService.GetDashboardStatsAsync(userId);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// دریافت لیست برنامه‌های پاداش با pagination و فیلتر
        /// </summary>
        /// <param name="pageNumber">شماره صفحه (پیش‌فرض: 1)</param>
        /// <param name="pageSize">تعداد در هر صفحه (پیش‌فرض: 10، حداکثر: 100)</param>
        /// <param name="isActive">فیلتر وضعیت فعال (true = فقط فعال، false = فقط غیرفعال، null = همه)</param>
        /// <returns>پاسخ شامل لیست برنامه‌ها و اطلاعات pagination</returns>
        /// <remarks>
        /// این endpoint لیست تمام برنامه‌های پاداش کاربر فعلی را برمی‌گرداند.
        /// </remarks>
        /// <response code="200">لیست برنامه‌ها با موفقیت برگردانده شد</response>
        /// <response code="500">خطای سرور</response>
        [HttpGet]
        [ProducesResponseType(typeof(ApiResponse<ReferralProgramListDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<ReferralProgramListDto>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<ReferralProgramListDto>>> GetPrograms(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] bool? isActive = null)
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _referralProgramService.GetProgramsAsync(userId, pageNumber, pageSize, isActive);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// دریافت جزئیات یک برنامه پاداش
        /// </summary>
        /// <param name="id">شناسه برنامه پاداش</param>
        /// <returns>پاسخ شامل اطلاعات کامل برنامه</returns>
        /// <remarks>
        /// این endpoint اطلاعات کامل یک برنامه را شامل کد عمومی، نوع پاداش، مخاطبین هدف و بازه زمانی برمی‌گرداند.
        /// </remarks>
        /// <response code="200">اطلاعات برنامه با موفقیت برگردانده شد</response>
        /// <response code="404">برنامه یافت نشد</response>
        /// <response code="500">خطای سرور</response>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(ApiResponse<ReferralProgramDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<ReferralProgramDto>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<ReferralProgramDto>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<ReferralProgramDto>>> GetById(int id)
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _referralProgramService.GetByIdAsync(id, userId);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// تغییر وضعیت فعال/غیرفعال برنامه پاداش
        /// </summary>
        /// <param name="id">شناسه برنامه پاداش</param>
        /// <returns>پاسخ شامل اطلاعات برنامه با وضعیت جدید</returns>
        /// <remarks>
        /// با هر درخواست، وضعیت IsActive برنامه معکوس می‌شود (toggle).
        /// برنامه غیرفعال در استعلام و مصرف کد نامعتبر است.
        /// </remarks>
        /// <response code="200">وضعیت برنامه با موفقیت تغییر کرد</response>
        /// <response code="404">برنامه یافت نشد</response>
        /// <response code="500">خطای سرور</response>
        [HttpPost("{id}/toggle-status")]
        [ProducesResponseType(typeof(ApiResponse<ReferralProgramDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<ReferralProgramDto>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<ReferralProgramDto>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<ReferralProgramDto>>> ToggleStatus(int id)
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _referralProgramService.ToggleStatusAsync(id, userId);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// حذف نرم برنامه پاداش (Soft Delete)
        /// </summary>
        /// <param name="id">شناسه برنامه پاداش</param>
        /// <returns>پاسخ شامل وضعیت حذف</returns>
        /// <remarks>
        /// برنامه از دیتابیس حذف فیزیکی نمی‌شود؛ فقط علامت IsDeleted تنظیم می‌شود.
        /// تاریخچه مصرف‌های قبلی همچنان قابل مشاهده است.
        /// </remarks>
        /// <response code="200">برنامه با موفقیت حذف شد</response>
        /// <response code="404">برنامه یافت نشد</response>
        /// <response code="500">خطای سرور</response>
        [HttpPost("{id}/delete")]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<bool>>> Delete(int id)
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _referralProgramService.DeleteAsync(id, userId);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// به‌روزرسانی جزئی برنامه پاداش
        /// </summary>
        /// <param name="id">شناسه برنامه پاداش</param>
        /// <param name="updateDto">فیلدهای قابل ویرایش (عنوان، وضعیت، مقدار پاداش، تاریخ پایان)</param>
        /// <returns>پاسخ شامل اطلاعات برنامه به‌روزرسانی‌شده</returns>
        /// <remarks>
        /// **به‌روزرسانی جزئی (Partial Update):**
        /// - فقط فیلدهای ارسال‌شده تغییر می‌کنند
        /// - ارسال body خالی → 400
        /// - عنوان تکراری → 400
        /// </remarks>
        /// <response code="200">برنامه با موفقیت به‌روزرسانی شد</response>
        /// <response code="400">داده‌های ورودی نامعتبر است</response>
        /// <response code="404">برنامه یافت نشد</response>
        /// <response code="500">خطای سرور</response>
        [HttpPost("{id}/update")]
        [ProducesResponseType(typeof(ApiResponse<ReferralProgramDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<ReferralProgramDto>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<ReferralProgramDto>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<ReferralProgramDto>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<ReferralProgramDto>>> Update(
            int id,
            [FromBody] UpdateReferralProgramDto updateDto)
        {
            var invalid = InvalidModelStateResponse<ReferralProgramDto>();
            if (invalid != null)
            {
                return invalid;
            }

            var userId = await GetCurrentUserIdAsync();
            var result = await _referralProgramService.UpdateProgramAsync(id, userId, updateDto);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// دریافت تاریخچه مصرف کد یک برنامه
        /// </summary>
        /// <param name="id">شناسه برنامه پاداش</param>
        /// <param name="pageNumber">شماره صفحه (پیش‌فرض: 1)</param>
        /// <param name="pageSize">تعداد در هر صفحه (پیش‌فرض: 10، حداکثر: 100)</param>
        /// <param name="fromDate">فیلتر از تاریخ (اختیاری، UTC)</param>
        /// <param name="toDate">فیلتر تا تاریخ (اختیاری، UTC)</param>
        /// <returns>پاسخ شامل لیست مصرف‌ها، pagination و جمع تخفیف/پاداش</returns>
        /// <remarks>
        /// این endpoint برای صفحه تاریخچه مصرف کد در فروشگاه استفاده می‌شود.
        /// هر رکورد شامل مبلغ خرید، تخفیف مشتری، پاداش معرف و مخاطبین مرتبط است.
        /// </remarks>
        /// <response code="200">تاریخچه با موفقیت برگردانده شد</response>
        /// <response code="400">بازه تاریخ فیلتر نامعتبر است</response>
        /// <response code="404">برنامه یافت نشد</response>
        /// <response code="500">خطای سرور</response>
        [HttpGet("{id}/history")]
        [ProducesResponseType(typeof(ApiResponse<ReferralUsageHistoryListDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<ReferralUsageHistoryListDto>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<ReferralUsageHistoryListDto>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<ReferralUsageHistoryListDto>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<ReferralUsageHistoryListDto>>> GetHistory(
            int id,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] DateTime? fromDate = null,
            [FromQuery] DateTime? toDate = null)
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _referralProgramService.GetHistoryAsync(
                id, userId, pageNumber, pageSize, fromDate, toDate);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// دریافت لیست دفترچه‌های مخاطب برای مرحله ۲ ویزارد
        /// </summary>
        /// <returns>پاسخ شامل لیست دفترچه‌ها و تعداد اعضای هر دفترچه</returns>
        /// <remarks>
        /// این endpoint در مرحله انتخاب مخاطبین (SpecificNotebooks) ویزارد استفاده می‌شود.
        /// </remarks>
        /// <response code="200">لیست دفترچه‌ها با موفقیت برگردانده شد</response>
        /// <response code="500">خطای سرور</response>
        [HttpGet("notebooks")]
        [ProducesResponseType(typeof(ApiResponse<List<ReferralNotebookDto>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<List<ReferralNotebookDto>>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<List<ReferralNotebookDto>>>> GetNotebooks()
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _referralProgramService.GetNotebooksAsync(userId);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// اعتبارسنجی مرحله ۱ ویزارد (اطلاعات پاداش)
        /// </summary>
        /// <param name="step1Dto">نام برنامه، نوع پاداش، مقدار پاداش معرف و مشتری</param>
        /// <returns>پاسخ شامل draftId برای ادامه ویزارد</returns>
        /// <remarks>
        /// **مرحله ۱ — اطلاعات پاداش:**
        /// - در صورت موفقیت، یک پیش‌نویس (draft) ایجاد و draftId برگردانده می‌شود
        /// - draftId در مراحل بعدی الزامی است
        /// - پیش‌نویس ۲۴ ساعت اعتبار دارد
        /// </remarks>
        /// <response code="200">اطلاعات مرحله ۱ معتبر است و draftId صادر شد</response>
        /// <response code="400">داده‌های ورودی نامعتبر یا عنوان تکراری</response>
        /// <response code="500">خطای سرور</response>
        [HttpPost("validate-step1")]
        [ProducesResponseType(typeof(ApiResponse<ReferralStep1ValidationResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<ReferralStep1ValidationResponseDto>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<ReferralStep1ValidationResponseDto>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<ReferralStep1ValidationResponseDto>>> ValidateStep1(
            [FromBody] ReferralStep1Dto step1Dto)
        {
            var invalid = InvalidModelStateResponse<ReferralStep1ValidationResponseDto>();
            if (invalid != null)
            {
                return invalid;
            }

            var userId = await GetCurrentUserIdAsync();
            var result = await _referralProgramService.ValidateStep1Async(userId, step1Dto);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// اعتبارسنجی مرحله ۲ ویزارد (انتخاب مخاطبین)
        /// </summary>
        /// <param name="step2Dto">draftId، نوع مخاطبین و شناسه‌های دفترچه/مخاطب</param>
        /// <returns>پاسخ شامل تعداد مخاطبین و توضیح مخاطب هدف</returns>
        /// <remarks>
        /// **مرحله ۲ — مخاطبین:**
        /// - draftId از مرحله ۱ الزامی است
        /// - TargetAudience: All | SpecificNotebooks | Individual
        /// - فیلتر تگ (SendToSpecificTags / TargetTagIds) در همین مرحله اعمال می‌شود
        /// - totalContactsCount = تعداد نهایی گیرنده SMS (بعد از تگ)
        /// </remarks>
        /// <response code="200">اطلاعات مرحله ۲ معتبر است</response>
        /// <response code="400">draftId نامعتبر، مخاطبین نامعتبر یا خالی</response>
        /// <response code="500">خطای سرور</response>
        [HttpPost("validate-step2")]
        [ProducesResponseType(typeof(ApiResponse<ReferralStep2ValidationResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<ReferralStep2ValidationResponseDto>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<ReferralStep2ValidationResponseDto>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<ReferralStep2ValidationResponseDto>>> ValidateStep2(
            [FromBody] ReferralStep2Dto step2Dto)
        {
            var invalid = InvalidModelStateResponse<ReferralStep2ValidationResponseDto>();
            if (invalid != null)
            {
                return invalid;
            }

            var userId = await GetCurrentUserIdAsync();
            var result = await _referralProgramService.ValidateStep2Async(userId, step2Dto);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// دریافت خلاصه برنامه قبل از تأیید نهایی
        /// </summary>
        /// <param name="draftId">شناسه پیش‌نویس از مرحله ۱ (الزامی)</param>
        /// <returns>پاسخ شامل خلاصه عنوان، پاداش، تاریخ و تعداد مخاطبین</returns>
        /// <remarks>
        /// این endpoint برای نمایش صفحه «خلاصه و تأیید» ویزارد استفاده می‌شود.
        /// draftId باید از validate-step1 دریافت شده باشد.
        /// </remarks>
        /// <response code="200">خلاصه با موفقیت برگردانده شد</response>
        /// <response code="400">draftId ارسال نشده یا پیش‌نویس نامعتبر</response>
        /// <response code="500">خطای سرور</response>
        [HttpGet("summary")]
        [ProducesResponseType(typeof(ApiResponse<ReferralSummaryDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<ReferralSummaryDto>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<ReferralSummaryDto>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<ReferralSummaryDto>>> GetSummary(
            [FromQuery] string? draftId = null)
        {
            if (string.IsNullOrEmpty(draftId))
            {
                return StatusCode(400, ApiResponse<ReferralSummaryDto>.BadRequest(
                    "draftId الزامی است",
                    errorCode: ErrorCodes.ValidationFailed));
            }

            var userId = await GetCurrentUserIdAsync();
            var result = await _referralProgramService.GetSummaryAsync(userId, new GetReferralSummaryRequestDto
            {
                DraftId = draftId
            });
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// ذخیره تنظیمات مرحله ۳ ویزارد (تاریخ و تگ)
        /// </summary>
        /// <param name="request">شناسه پیش‌نویس (draftId) و تنظیمات تاریخ شروع/پایان و فیلتر تگ</param>
        /// <returns>پاسخ شامل خلاصه به‌روزشده برنامه</returns>
        /// <remarks>
        /// **مرحله ۳ — تاریخ:**
        /// - draftId از مرحله ۱ الزامی است
        /// - فقط startDate و endDate ذخیره می‌شود (تگ در مرحله ۲ است)
        /// - پاسخ شامل خلاصه و contactsCount (از مرحله ۲) است
        /// </remarks>
        /// <response code="200">تنظیمات مرحله ۳ ذخیره شد</response>
        /// <response code="400">تاریخ نامعتبر، تگ نامعتبر یا draftId نامعتبر</response>
        /// <response code="500">خطای سرور</response>
        [HttpPost("settings/save")]
        [ProducesResponseType(typeof(ApiResponse<ReferralSummaryDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<ReferralSummaryDto>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<ReferralSummaryDto>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<ReferralSummaryDto>>> SaveStep3Settings(
            [FromBody] SaveReferralStep3RequestDto request)
        {
            var invalid = InvalidModelStateResponse<ReferralSummaryDto>();
            if (invalid != null)
            {
                return invalid;
            }

            var userId = await GetCurrentUserIdAsync();
            var result = await _referralProgramService.SaveStep3SettingsAsync(userId, request);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// تأیید نهایی و ایجاد برنامه پاداش
        /// </summary>
        /// <param name="request">draftId پیش‌نویس تکمیل‌شده</param>
        /// <returns>پاسخ شامل برنامه ایجادشده، کد عمومی و آمار ارسال پیامک</returns>
        /// <remarks>
        /// **تأیید نهایی:**
        /// - هر سه مرحله ویزارد باید تکمیل شده باشند
        /// - یک کد عمومی (REFxxxxxx) برای برنامه صادر می‌شود
        /// - پیامک حاوی کد به مخاطبین هدف ارسال می‌شود
        /// - پیش‌نویس پس از تأیید حذف می‌شود
        /// </remarks>
        /// <response code="201">برنامه پاداش با موفقیت ثبت شد</response>
        /// <response code="400">پیش‌نویس نامعتبر، مراحل ناقص یا مخاطب خالی</response>
        /// <response code="500">خطای سرور</response>
        [HttpPost("confirm")]
        [ProducesResponseType(typeof(ApiResponse<ConfirmReferralProgramResponseDto>), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ApiResponse<ConfirmReferralProgramResponseDto>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<ConfirmReferralProgramResponseDto>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<ConfirmReferralProgramResponseDto>>> Confirm(
            [FromBody] ConfirmReferralProgramDto request)
        {
            var invalid = InvalidModelStateResponse<ConfirmReferralProgramResponseDto>();
            if (invalid != null)
            {
                return invalid;
            }

            var userId = await GetCurrentUserIdAsync();
            var result = await _referralProgramService.ConfirmAsync(userId, request);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// استعلام اعتبار کد معرف در فروشگاه (فقط خواندن)
        /// </summary>
        /// <param name="request">کد معرف و مبلغ خرید (برای محاسبه تخفیف درصدی)</param>
        /// <returns>پاسخ شامل وضعیت اعتبار، مبلغ تخفیف و اطلاعات برنامه</returns>
        /// <remarks>
        /// **استعلام کد (Inquire):**
        /// - فقط اعتبار کد را بررسی می‌کند؛ مصرف ثبت نمی‌شود
        /// - برای پاداش درصدی، ارسال purchaseAmount برای محاسبه تخفیف توصیه می‌شود
        /// - کد نامعتبر → IsValid=false (بدون خطای 404)
        /// - وضعیت‌های IsExpired و IsNotStarted جداگانه برگردانده می‌شوند
        /// </remarks>
        /// <response code="200">نتیجه استعلام برگردانده شد</response>
        /// <response code="400">داده‌های ورودی نامعتبر</response>
        /// <response code="500">خطای سرور</response>
        [HttpPost("inquire")]
        [ProducesResponseType(typeof(ApiResponse<InquireReferralCodeResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<InquireReferralCodeResponseDto>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<InquireReferralCodeResponseDto>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<InquireReferralCodeResponseDto>>> InquireCode(
            [FromBody] InquireReferralCodeDto request)
        {
            var invalid = InvalidModelStateResponse<InquireReferralCodeResponseDto>();
            if (invalid != null)
            {
                return invalid;
            }

            var userId = await GetCurrentUserIdAsync();
            var result = await _referralProgramService.InquireCodeAsync(userId, request);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// ثبت مصرف کد معرف در فروشگاه
        /// </summary>
        /// <param name="request">کد، مبلغ خرید، مخاطب مشتری/معرف (اختیاری) و توضیح</param>
        /// <returns>پاسخ شامل مبلغ تخفیف، پاداش معرف و وضعیت واریز به کش‌بک</returns>
        /// <remarks>
        /// **ثبت مصرف (Redeem):**
        /// - مصرف کد در تاریخچه ثبت می‌شود
        /// - برای پاداش درصدی، purchaseAmount الزامی است
        /// - در صورت ارسال customerContactId/referrerContactId، پاداش به موجودی کش‌بک مخاطب واریز می‌شود
        /// - کد نامعتبر یا منقضی → 400 یا 404
        /// </remarks>
        /// <response code="201">مصرف کد با موفقیت ثبت شد</response>
        /// <response code="400">کد غیرقابل استفاده، مبلغ خرید نامعتبر یا مخاطب نامعتبر</response>
        /// <response code="404">کد یافت نشد</response>
        /// <response code="500">خطای سرور</response>
        [HttpPost("redeem")]
        [ProducesResponseType(typeof(ApiResponse<RedeemReferralCodeResponseDto>), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ApiResponse<RedeemReferralCodeResponseDto>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<RedeemReferralCodeResponseDto>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<RedeemReferralCodeResponseDto>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<RedeemReferralCodeResponseDto>>> RedeemCode(
            [FromBody] RedeemReferralCodeDto request)
        {
            var invalid = InvalidModelStateResponse<RedeemReferralCodeResponseDto>();
            if (invalid != null)
            {
                return invalid;
            }

            var userId = await GetCurrentUserIdAsync();
            var result = await _referralProgramService.RedeemCodeAsync(userId, request);
            return StatusCode(result.StatusCode, result);
        }
    }
}
