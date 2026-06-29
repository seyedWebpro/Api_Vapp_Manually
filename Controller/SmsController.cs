using Api_Vapp.Constants;
using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.Sms;
using Api_Vapp.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System.Security.Claims;

namespace Api_Vapp.Controller
{
    /// <summary>
    /// کنترلر مدیریت ارسال پیامک
    /// </summary>
    /// <remarks>
    /// این کنترلر شامل تمام endpoint های مربوط به ارسال و مدیریت پیامک‌ها می‌باشد.
    /// 
    /// **قابلیت‌های اصلی:**
    /// - ارسال پیامک تکی
    /// - ارسال پیامک گروهی (Bulk)
    /// - ارسال پیامک نظیر به نظیر (هر شماره متن خودش)
    /// - دریافت وضعیت ارسال (Delivery Status)
    /// - دریافت پیامک‌های ورودی (Inbox)
    /// - دریافت اطلاعات موجودی کیف پول SMS
    /// 
    /// **نکات مهم:**
    /// - تمام عملیات ارسال از موجودی کیف پول SMS کسر می‌شود
    /// - برای دریافت پیامک‌های ورودی، نیاز به خط اختصاصی است
    /// 
    /// تمام endpoint های این کنترلر نیاز به احراز هویت دارند.
    /// </remarks>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    [Produces("application/json")]
    public class SmsController : ControllerBase
    {
        private readonly ISmsService _smsService;
        private readonly ISmsDeliveryTrackingService _deliveryTracking;
        private readonly IConfiguration _configuration;
        private readonly IUserRepository _userRepository;

        public SmsController(
            ISmsService smsService,
            ISmsDeliveryTrackingService deliveryTracking,
            IConfiguration configuration,
            IUserRepository userRepository)
        {
            _smsService = smsService;
            _deliveryTracking = deliveryTracking;
            _configuration = configuration;
            _userRepository = userRepository;
        }

        private static bool IsSmsSendSuccessful(long sid, int status) => sid > 0 || status > 0;

        private Task TrackManualSendAsync(int userId, string mobile, long sid, string? label = null) =>
            _deliveryTracking.TrackSuccessfulSendAsync(new SmsDeliveryTrackRequestDto
            {
                UserId = userId,
                SourceModule = SmsSourceModules.Manual,
                SourceEntityLabel = label ?? "ارسال دستی",
                Mobile = mobile,
                Sid = sid,
                SentAt = DateTime.UtcNow
            });

        /// <summary>
        /// استخراج خطاهای ModelState برای نمایش به کاربر
        /// </summary>
        private List<string> ExtractModelStateErrors() =>
            Api_Vapp.Utilities.ErrorTranslator.ExtractModelStateErrors(ModelState);

        /// <summary>
        /// دریافت شناسه کاربر از JWT Token یا برگرداندن کاربر پیش‌فرض در حالت DisableAuth
        /// </summary>
        private async Task<int?> GetCurrentUserIdAsync()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(userIdClaim) && int.TryParse(userIdClaim, out int userId))
            {
                return userId;
            }

            var disableAuth = _configuration.GetValue<bool>("Development:DisableAuth", false);
            if (disableAuth)
            {
                var defaultUser = await _userRepository.GetOrCreateDefaultUserAsync();
                return defaultUser.Id;
            }

            return null;
        }

        /// <summary>
        /// ارسال پیامک تکی
        /// </summary>
        /// <param name="request">اطلاعات ارسال پیامک شامل شماره گیرنده و متن پیام</param>
        /// <returns>پاسخ شامل نتیجه ارسال و شناسه پیامک (Sid)</returns>
        /// <remarks>
        /// این endpoint برای ارسال یک پیامک به یک شماره استفاده می‌شود.
        /// 
        /// **اطلاعات مورد نیاز:**
        /// - شماره گیرنده (MobileNumber)
        /// - متن پیام (Message)
        /// - شماره فرستنده (SenderNumber) - اختیاری
        /// 
        /// **نکات مهم:**
        /// - هزینه ارسال از موجودی کیف پول SMS کسر می‌شود
        /// - در صورت ناکافی بودن موجودی، ارسال انجام نمی‌شود
        /// - شناسه پیامک (Sid) برای پیگیری وضعیت ارسال برگردانده می‌شود
        /// </remarks>
        /// <response code="200">پیامک با موفقیت ارسال شد</response>
        /// <response code="400">داده‌های ورودی نامعتبر است</response>
        /// <response code="401">عدم احراز هویت</response>
        /// <response code="402">موجودی کیف پول SMS کافی نیست</response>
        /// <response code="500">خطای سرور</response>
        [HttpPost("send")]
        [ProducesResponseType(typeof(ApiResponse<SendSmsResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<SendSmsResponseDto>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<SendSmsResponseDto>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<SendSmsResponseDto>), StatusCodes.Status402PaymentRequired)]
        [ProducesResponseType(typeof(ApiResponse<SendSmsResponseDto>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<SendSmsResponseDto>>> SendSms([FromBody] SendSmsRequestDto request)
        {
            if (!ModelState.IsValid)
            {
                var errors = ExtractModelStateErrors();
                return StatusCode(400, ApiResponse<SendSmsResponseDto>.BadRequest("داده‌های ورودی نامعتبر است", errors));
            }

            var userId = await GetCurrentUserIdAsync();
            if (!userId.HasValue)
            {
                return StatusCode(401, ApiResponse<SendSmsResponseDto>.Unauthorized("شناسه کاربر معتبر نیست"));
            }

            var result = await _smsService.SendSmsAsync(request);
            if (userId.HasValue && result.Success && result.Data != null && IsSmsSendSuccessful(result.Data.Sid, result.Data.Status))
            {
                await TrackManualSendAsync(userId.Value, request.Mobile, result.Data.Sid);
            }
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// ارسال پیامک گروهی (Bulk) - یک متن به چند شماره
        /// </summary>
        /// <param name="request">اطلاعات ارسال گروهی شامل لیست شماره‌ها و متن پیام</param>
        /// <returns>پاسخ شامل نتیجه ارسال و آمار</returns>
        /// <remarks>
        /// این endpoint برای ارسال یک پیامک به چندین شماره استفاده می‌شود.
        /// 
        /// **اطلاعات مورد نیاز:**
        /// - لیست شماره گیرندگان (MobileNumbers)
        /// - متن پیام (Message) - یکسان برای همه
        /// - شماره فرستنده (SenderNumber) - اختیاری
        /// 
        /// **نکات مهم:**
        /// - هزینه ارسال برای هر شماره از موجودی کیف پول SMS کسر می‌شود
        /// - در صورت ناکافی بودن موجودی، ارسال انجام نمی‌شود
        /// - می‌توانید حداکثر 1000 شماره را در یک درخواست ارسال کنید
        /// - آمار ارسال موفق/ناموفق در پاسخ برگردانده می‌شود
        /// </remarks>
        /// <response code="200">پیامک‌ها با موفقیت ارسال شدند</response>
        /// <response code="400">داده‌های ورودی نامعتبر است</response>
        /// <response code="401">عدم احراز هویت</response>
        /// <response code="402">موجودی کیف پول SMS کافی نیست</response>
        /// <response code="500">خطای سرور</response>
        [HttpPost("send-bulk")]
        [ProducesResponseType(typeof(ApiResponse<SendBulkResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<SendBulkResponseDto>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<SendBulkResponseDto>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<SendBulkResponseDto>), StatusCodes.Status402PaymentRequired)]
        [ProducesResponseType(typeof(ApiResponse<SendBulkResponseDto>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<SendBulkResponseDto>>> SendBulkSms([FromBody] SendBulkRequestDto request)
        {
            if (!ModelState.IsValid)
            {
                var errors = ExtractModelStateErrors();
                return StatusCode(400, ApiResponse<SendBulkResponseDto>.BadRequest("داده‌های ورودی نامعتبر است", errors));
            }

            var userId = await GetCurrentUserIdAsync();
            if (!userId.HasValue)
            {
                return StatusCode(401, ApiResponse<SendBulkResponseDto>.Unauthorized("شناسه کاربر معتبر نیست"));
            }

            var result = await _smsService.SendBulkSmsAsync(request);
            if (userId.HasValue && result.Success && result.Data != null && IsSmsSendSuccessful(result.Data.Sid, result.Data.Status))
            {
                foreach (var mobile in request.Mobiles)
                {
                    await TrackManualSendAsync(userId.Value, mobile, result.Data.Sid, "ارسال گروهی");
                }
            }
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// ارسال پیامک نظیر به نظیر (هر شماره متن خودش)
        /// </summary>
        /// <param name="request">اطلاعات ارسال نظیر به نظیر شامل لیست شماره‌ها و متن‌های متناظر</param>
        /// <returns>پاسخ شامل نتیجه ارسال و آمار</returns>
        /// <remarks>
        /// این endpoint برای ارسال پیامک‌های شخصی‌سازی شده به چندین شماره استفاده می‌شود.
        /// هر شماره متن مخصوص به خود را دریافت می‌کند.
        /// 
        /// **اطلاعات مورد نیاز:**
        /// - لیست شماره گیرندگان (MobileNumbers)
        /// - لیست متن‌های پیام (Messages) - هر متن برای شماره متناظر
        /// - شماره فرستنده (SenderNumber) - اختیاری
        /// 
        /// **نکات مهم:**
        /// - تعداد شماره‌ها و متن‌ها باید برابر باشد
        /// - هزینه ارسال برای هر شماره از موجودی کیف پول SMS کسر می‌شود
        /// - در صورت ناکافی بودن موجودی، ارسال انجام نمی‌شود
        /// - می‌توانید حداکثر 1000 شماره را در یک درخواست ارسال کنید
        /// </remarks>
        /// <response code="200">پیامک‌ها با موفقیت ارسال شدند</response>
        /// <response code="400">داده‌های ورودی نامعتبر است یا تعداد شماره‌ها و متن‌ها برابر نیست</response>
        /// <response code="401">عدم احراز هویت</response>
        /// <response code="402">موجودی کیف پول SMS کافی نیست</response>
        /// <response code="500">خطای سرور</response>
        [HttpPost("send-array")]
        [ProducesResponseType(typeof(ApiResponse<SendArrayResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<SendArrayResponseDto>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<SendArrayResponseDto>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<SendArrayResponseDto>), StatusCodes.Status402PaymentRequired)]
        [ProducesResponseType(typeof(ApiResponse<SendArrayResponseDto>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<SendArrayResponseDto>>> SendArraySms([FromBody] SendArrayRequestDto request)
        {
            if (!ModelState.IsValid)
            {
                var errors = ExtractModelStateErrors();
                return StatusCode(400, ApiResponse<SendArrayResponseDto>.BadRequest("داده‌های ورودی نامعتبر است", errors));
            }

            var userId = await GetCurrentUserIdAsync();
            if (!userId.HasValue)
            {
                return StatusCode(401, ApiResponse<SendArrayResponseDto>.Unauthorized("شناسه کاربر معتبر نیست"));
            }

            var result = await _smsService.SendArraySmsAsync(request);
            if (userId.HasValue && result.Success && result.Data != null && IsSmsSendSuccessful(result.Data.Sid, result.Data.Status))
            {
                foreach (var mobile in request.Mobiles)
                {
                    await TrackManualSendAsync(userId.Value, mobile, result.Data.Sid, "ارسال نظیر به نظیر");
                }
            }
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// دریافت وضعیت ارسال پیامک (Delivery Status) - POST
        /// </summary>
        /// <param name="request">اطلاعات درخواست شامل شناسه پیامک (Sid)</param>
        /// <returns>پاسخ شامل وضعیت ارسال پیامک</returns>
        /// <remarks>
        /// این endpoint برای دریافت وضعیت ارسال یک پیامک بر اساس شناسه پیامک (Sid) استفاده می‌شود.
        /// 
        /// **وضعیت‌های ممکن:**
        /// - Pending: در انتظار ارسال
        /// - Sent: ارسال شده
        /// - Delivered: تحویل داده شده
        /// - Failed: ناموفق
        /// 
        /// **نکات مهم:**
        /// - Sid از پاسخ endpoint ارسال پیامک دریافت می‌شود
        /// - می‌توانید از GET /delivery/{sid} نیز استفاده کنید
        /// </remarks>
        /// <response code="200">وضعیت ارسال با موفقیت برگردانده شد</response>
        /// <response code="400">داده‌های ورودی نامعتبر است</response>
        /// <response code="401">عدم احراز هویت</response>
        /// <response code="404">پیامک با این Sid یافت نشد</response>
        /// <response code="500">خطای سرور</response>
        [HttpPost("delivery")]
        [ProducesResponseType(typeof(ApiResponse<DeliveryResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<DeliveryResponseDto>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<DeliveryResponseDto>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<DeliveryResponseDto>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<DeliveryResponseDto>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<DeliveryResponseDto>>> GetDeliveryStatus([FromBody] DeliveryRequestDto request)
        {
            if (!ModelState.IsValid)
            {
                var errors = ExtractModelStateErrors();
                return StatusCode(400, ApiResponse<DeliveryResponseDto>.BadRequest("داده‌های ورودی نامعتبر است", errors));
            }

            var userId = await GetCurrentUserIdAsync();
            if (!userId.HasValue)
            {
                return StatusCode(401, ApiResponse<DeliveryResponseDto>.Unauthorized("شناسه کاربر معتبر نیست"));
            }

            var result = await _smsService.GetDeliveryStatusAsync(request.Sid);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// دریافت وضعیت ارسال پیامک با Sid (GET)
        /// </summary>
        /// <param name="sid">شناسه پیامک (Sid)</param>
        /// <returns>پاسخ شامل وضعیت ارسال پیامک</returns>
        /// <remarks>
        /// این endpoint برای دریافت وضعیت ارسال یک پیامک بر اساس شناسه پیامک (Sid) استفاده می‌شود.
        /// 
        /// **وضعیت‌های ممکن:**
        /// - Pending: در انتظار ارسال
        /// - Sent: ارسال شده
        /// - Delivered: تحویل داده شده
        /// - Failed: ناموفق
        /// 
        /// **نکات مهم:**
        /// - این endpoint از روش GET استفاده می‌کند (مناسب برای لینک مستقیم)
        /// - می‌توانید از POST /delivery نیز استفاده کنید
        /// </remarks>
        /// <response code="200">وضعیت ارسال با موفقیت برگردانده شد</response>
        /// <response code="401">عدم احراز هویت</response>
        /// <response code="404">پیامک با این Sid یافت نشد</response>
        /// <response code="500">خطای سرور</response>
        [HttpGet("delivery/{sid}")]
        [ProducesResponseType(typeof(ApiResponse<DeliveryResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<DeliveryResponseDto>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<DeliveryResponseDto>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<DeliveryResponseDto>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<DeliveryResponseDto>>> GetDeliveryStatusBySid(long sid)
        {
            var userId = await GetCurrentUserIdAsync();
            if (!userId.HasValue)
            {
                return StatusCode(401, ApiResponse<DeliveryResponseDto>.Unauthorized("شناسه کاربر معتبر نیست"));
            }

            var result = await _smsService.GetDeliveryStatusAsync(sid);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// دریافت پیامک‌های ورودی (Inbox) - فقط برای خطوط اختصاصی
        /// </summary>
        /// <param name="request">اطلاعات درخواست شامل شماره خط و فیلترها</param>
        /// <returns>پاسخ شامل لیست پیامک‌های ورودی</returns>
        /// <remarks>
        /// این endpoint برای دریافت پیامک‌های ورودی به خط اختصاصی استفاده می‌شود.
        /// 
        /// **نکات مهم:**
        /// - این قابلیت فقط برای خطوط اختصاصی (Dedicated Number) فعال است
        /// - می‌توانید فیلترهای مختلف اعمال کنید (تاریخ، شماره فرستنده و غیره)
        /// - پیامک‌های ورودی به صورت pagination برگردانده می‌شوند
        /// 
        /// **اطلاعات هر پیامک شامل:**
        /// - شماره فرستنده
        /// - متن پیام
        /// - تاریخ و زمان دریافت
        /// - وضعیت
        /// </remarks>
        /// <response code="200">لیست پیامک‌های ورودی با موفقیت برگردانده شد</response>
        /// <response code="400">داده‌های ورودی نامعتبر است</response>
        /// <response code="401">عدم احراز هویت</response>
        /// <response code="403">خط اختصاصی فعال نیست</response>
        /// <response code="500">خطای سرور</response>
        [HttpPost("inbox")]
        [ProducesResponseType(typeof(ApiResponse<InboxResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<InboxResponseDto>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<InboxResponseDto>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<InboxResponseDto>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<InboxResponseDto>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<InboxResponseDto>>> GetInbox([FromBody] InboxRequestDto request)
        {
            if (!ModelState.IsValid)
            {
                var errors = ExtractModelStateErrors();
                return StatusCode(400, ApiResponse<InboxResponseDto>.BadRequest("داده‌های ورودی نامعتبر است", errors));
            }

            var userId = await GetCurrentUserIdAsync();
            if (!userId.HasValue)
            {
                return StatusCode(401, ApiResponse<InboxResponseDto>.Unauthorized("شناسه کاربر معتبر نیست"));
            }

            var result = await _smsService.GetInboxAsync(request);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// دریافت اطلاعات موجودی کیف پول SMS
        /// </summary>
        /// <returns>پاسخ شامل اطلاعات موجودی و آمار</returns>
        /// <remarks>
        /// این endpoint اطلاعات موجودی کیف پول SMS کاربر را برمی‌گرداند.
        /// 
        /// **اطلاعات شامل:**
        /// - موجودی فعلی (Balance)
        /// - تعداد پیامک‌های ارسال شده
        /// - هزینه هر پیامک
        /// - آمار ارسال (موفق، ناموفق)
        /// 
        /// **نکات مهم:**
        /// - موجودی به تومان یا واحد اعتبار نمایش داده می‌شود
        /// - از این endpoint برای نمایش موجودی در داشبورد استفاده می‌شود
        /// </remarks>
        /// <response code="200">اطلاعات موجودی با موفقیت برگردانده شد</response>
        /// <response code="401">عدم احراز هویت</response>
        /// <response code="500">خطای سرور</response>
        [HttpGet("info")]
        [ProducesResponseType(typeof(ApiResponse<InfoResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<InfoResponseDto>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<InfoResponseDto>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<InfoResponseDto>>> GetWalletInfo()
        {
            var userId = await GetCurrentUserIdAsync();
            if (!userId.HasValue)
            {
                return StatusCode(401, ApiResponse<InfoResponseDto>.Unauthorized("شناسه کاربر معتبر نیست"));
            }

            var result = await _smsService.GetWalletInfoAsync();
            return StatusCode(result.StatusCode, result);
        }
    }
}



