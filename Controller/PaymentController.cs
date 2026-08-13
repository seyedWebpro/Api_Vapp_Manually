using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.Payment;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Api_Vapp.Controller
{
    /// <summary>
    /// کنترلر مدیریت پرداخت
    /// </summary>
    /// <remarks>
    /// این کنترلر شامل تمام endpoint های مربوط به مدیریت پرداخت‌ها می‌باشد.
    /// 
    /// **قابلیت‌های اصلی:**
    /// - ایجاد پرداخت جدید
    /// - دریافت اطلاعات پرداخت
    /// - تأیید پرداخت (Callback از درگاه)
    /// - مدیریت Callback های درگاه‌های پرداخت
    /// - لغو پرداخت
    /// 
    /// **انواع پرداخت:**
    /// - WalletCharge: شارژ کیف پول
    /// - Subscription: خرید اشتراک
    /// - SmsPurchase: خرید پیامک
    /// 
    /// **درگاه‌های پرداخت:**
    /// - Behpardakht: به‌پرداخت ملت
    /// 
    /// تمام endpoint های این کنترلر نیاز به احراز هویت دارند (به جز Callback ها).
    /// </remarks>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    [Produces("application/json")]
    public class PaymentController : VappControllerBase
    {
        private readonly IPaymentService _paymentService;
        private readonly IPaymentRepository _paymentRepository;

        public PaymentController(
            IPaymentService paymentService,
            IPaymentRepository paymentRepository,
            IConfiguration configuration,
            IUserRepository userRepository)
            : base(configuration, userRepository)
        {
            _paymentService = paymentService;
            _paymentRepository = paymentRepository;
        }

        /// <summary>
        /// دریافت لیست درگاه‌های پرداخت موجود
        /// </summary>
        /// <returns>پاسخ شامل لیست درگاه‌های پرداخت فعال</returns>
        /// <remarks>
        /// این endpoint لیست تمام درگاه‌های پرداخت فعال در سیستم را برمی‌گرداند.
        /// 
        /// **اطلاعات هر درگاه شامل:**
        /// - نام درگاه
        /// - شناسه درگاه
        /// - وضعیت فعال/غیرفعال
        /// - توضیحات
        /// 
        /// **نکات مهم:**
        /// - این endpoint نیاز به احراز هویت ندارد (AllowAnonymous)
        /// - برای نمایش لیست درگاه‌ها در فرم پرداخت استفاده می‌شود
        /// </remarks>
        /// <response code="200">لیست درگاه‌های پرداخت با موفقیت برگردانده شد</response>
        /// <response code="500">خطای سرور</response>
        [HttpGet("gateways")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(ApiResponse<List<PaymentGatewayInfoDto>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<List<PaymentGatewayInfoDto>>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<List<PaymentGatewayInfoDto>>>> GetAvailableGateways()
        {
            var result = await _paymentService.GetAvailableGatewaysAsync();
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// دریافت لیست پرداخت‌ها با pagination
        /// </summary>
        /// <param name="pageNumber">شماره صفحه (پیش‌فرض: 1)</param>
        /// <param name="pageSize">تعداد در هر صفحه (پیش‌فرض: 10، حداکثر: 100)</param>
        /// <returns>پاسخ شامل لیست پرداخت‌ها و اطلاعات pagination</returns>
        /// <remarks>
        /// این endpoint لیست تمام پرداخت‌های کاربر فعلی را با امکان pagination برمی‌گرداند.
        /// 
        /// **اطلاعات هر پرداخت شامل:**
        /// - نوع پرداخت
        /// - مبلغ
        /// - وضعیت (Pending, Success, Failed, Cancelled)
        /// - تاریخ و زمان
        /// - درگاه پرداخت
        /// </remarks>
        /// <response code="200">لیست پرداخت‌ها با موفقیت برگردانده شد</response>
        /// <response code="400">پارامترهای ورودی نامعتبر است</response>
        /// <response code="500">خطای سرور</response>
        [HttpGet]
        [ProducesResponseType(typeof(ApiResponse<PaymentListDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<PaymentListDto>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<PaymentListDto>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<PaymentListDto>>> GetPayments(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10)
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _paymentService.GetPaymentsAsync(userId, pageNumber, pageSize);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// دریافت اطلاعات پرداخت بر اساس شناسه
        /// </summary>
        /// <param name="id">شناسه پرداخت</param>
        /// <returns>پاسخ شامل اطلاعات کامل پرداخت</returns>
        /// <remarks>
        /// این endpoint اطلاعات کامل یک پرداخت را بر اساس شناسه برمی‌گرداند.
        /// 
        /// **اطلاعات شامل:**
        /// - نوع و مبلغ پرداخت
        /// - وضعیت پرداخت
        /// - اطلاعات درگاه
        /// - تاریخ و زمان
        /// - شماره سفارش و مرجع
        /// </remarks>
        /// <response code="200">اطلاعات پرداخت با موفقیت برگردانده شد</response>
        /// <response code="404">پرداخت یافت نشد</response>
        /// <response code="500">خطای سرور</response>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(ApiResponse<PaymentDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<PaymentDto>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<PaymentDto>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<PaymentDto>>> GetPaymentById(int id)
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _paymentService.GetPaymentByIdAsync(id, userId);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// دریافت اطلاعات پرداخت بر اساس شماره سفارش
        /// </summary>
        /// <param name="orderId">شماره سفارش (Order ID)</param>
        /// <returns>پاسخ شامل اطلاعات کامل پرداخت</returns>
        /// <remarks>
        /// این endpoint اطلاعات یک پرداخت را بر اساس شماره سفارش برمی‌گرداند.
        /// 
        /// **نکات مهم:**
        /// - شماره سفارش یکتا است
        /// - از این endpoint برای بررسی وضعیت پرداخت با استفاده از شماره سفارش استفاده می‌شود
        /// </remarks>
        /// <response code="200">اطلاعات پرداخت با موفقیت برگردانده شد</response>
        /// <response code="404">پرداخت با این شماره سفارش یافت نشد</response>
        /// <response code="500">خطای سرور</response>
        [HttpGet("order/{orderId}")]
        [ProducesResponseType(typeof(ApiResponse<PaymentDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<PaymentDto>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<PaymentDto>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<PaymentDto>>> GetPaymentByOrderId(string orderId)
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _paymentService.GetPaymentByOrderIdAsync(orderId, userId);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// ایجاد پرداخت جدید
        /// </summary>
        /// <param name="createDto">اطلاعات پرداخت شامل نوع، مبلغ و درگاه</param>
        /// <returns>پاسخ شامل اطلاعات پرداخت ایجاد شده و URL درگاه</returns>
        /// <remarks>
        /// این endpoint یک پرداخت جدید ایجاد می‌کند و URL درگاه پرداخت را برمی‌گرداند.
        /// 
        /// **انواع پرداخت:**
        /// - WalletCharge: شارژ کیف پول
        /// - Subscription: خرید اشتراک
        /// - SmsPurchase: خرید پیامک
        /// 
        /// **درگاه‌های موجود:**
        /// - Behpardakht: به‌پرداخت ملت
        /// 
        /// **فرآیند پرداخت:**
        /// 1. ایجاد پرداخت در سیستم
        /// 2. دریافت URL درگاه پرداخت
        /// 3. هدایت کاربر به درگاه
        /// 4. پس از پرداخت، Callback از درگاه دریافت می‌شود
        /// </remarks>
        /// <response code="200">پرداخت با موفقیت ایجاد شد و URL درگاه برگردانده شد</response>
        /// <response code="400">داده‌های ورودی نامعتبر است</response>
        /// <response code="500">خطای سرور</response>
        [HttpPost]
        [ProducesResponseType(typeof(ApiResponse<PaymentDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<PaymentDto>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<PaymentDto>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<PaymentDto>>> CreatePayment(
            [FromBody] CreatePaymentDto createDto)
        {
            if (!ModelState.IsValid)
            {
                var errors = ExtractModelStateErrors();
                return StatusCode(400, ApiResponse<PaymentDto>.BadRequest("داده‌های ورودی نامعتبر است", errors));
            }

            var userId = await GetCurrentUserIdAsync();
            var result = await _paymentService.CreatePaymentAsync(userId, createDto);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// تأیید پرداخت (Callback از درگاه)
        /// </summary>
        /// <param name="verifyDto">اطلاعات تأیید پرداخت از درگاه</param>
        /// <returns>پاسخ شامل نتیجه تأیید پرداخت</returns>
        /// <remarks>
        /// این endpoint توسط درگاه پرداخت بعد از بازگشت کاربر فراخوانی می‌شود.
        /// 
        /// **فرآیند تأیید:**
        /// 1. دریافت اطلاعات از درگاه
        /// 2. بررسی صحت اطلاعات
        /// 3. تأیید پرداخت در سیستم
        /// 4. به‌روزرسانی موجودی یا فعال‌سازی سرویس
        /// 
        /// **نکات مهم:**
        /// - اطلاعات پرداخت از Query String یا Body دریافت می‌شود
        /// - پس از تأیید موفق، موجودی کیف پول یا سرویس مربوطه فعال می‌شود
        /// </remarks>
        /// <response code="200">پرداخت با موفقیت تأیید شد</response>
        /// <response code="400">داده‌های ورودی نامعتبر است</response>
        /// <response code="404">پرداخت یافت نشد</response>
        /// <response code="500">خطای سرور</response>
        [HttpPost("verify")]
        [ProducesResponseType(typeof(ApiResponse<PaymentResultDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<PaymentResultDto>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<PaymentResultDto>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<PaymentResultDto>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<PaymentResultDto>>> VerifyPayment(
            [FromBody] VerifyPaymentRequestDto verifyDto)
        {
            if (!ModelState.IsValid)
            {
                var errors = ExtractModelStateErrors();
                return StatusCode(400, ApiResponse<PaymentResultDto>.BadRequest("داده‌های ورودی نامعتبر است", errors));
            }

            var userId = await GetCurrentUserIdAsync();
            var result = await _paymentService.VerifyPaymentAsync(userId, verifyDto);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// Callback درگاه به‌پرداخت (GET)
        /// </summary>
        [HttpGet("callback/behpardakht")]
        [AllowAnonymous]
        public async Task<ActionResult> BehpardakhtCallbackGet(
            [FromQuery] int? PaymentId,
            [FromQuery] string? RefId,
            [FromQuery] string? ResCode,
            [FromQuery] string? SaleOrderId,
            [FromQuery] string? SaleReferenceId,
            [FromQuery] string? CardHolderPan)
        {
            string? paymentType = null;
            if (PaymentId.HasValue)
            {
                var payment = await _paymentRepository.GetByIdAsync(PaymentId.Value);
                paymentType = payment?.PaymentType;
            }

            return Redirect(BuildFrontendCallbackUrl(
                PaymentId,
                paymentType,
                RefId,
                ResCode,
                SaleOrderId,
                SaleReferenceId,
                CardHolderPan));
        }

        /// <summary>
        /// Callback درگاه به‌پرداخت (POST)
        /// </summary>
        [HttpPost("callback/behpardakht")]
        [AllowAnonymous]
        public async Task<ActionResult> BehpardakhtCallbackPost(
            [FromForm] int? PaymentId,
            [FromForm] string? RefId,
            [FromForm] string? ResCode,
            [FromForm] string? SaleOrderId,
            [FromForm] string? SaleReferenceId,
            [FromForm] string? CardHolderPan)
        {
            return await BehpardakhtCallbackGet(PaymentId, RefId, ResCode, SaleOrderId, SaleReferenceId, CardHolderPan);
        }

        /// <summary>
        /// Callback درگاه زرین‌پال — Authority و Status از QueryString
        /// طبق مستندات: فقط وقتی Status=OK باید Verify فراخوانی شود.
        /// </summary>
        [HttpGet("callback/zarinpal")]
        [AllowAnonymous]
        [Produces("text/html")]
        public async Task<ContentResult> ZarinPalCallbackGet(
            [FromQuery] string? Authority,
            [FromQuery] string? Status,
            [FromQuery] string? authority,
            [FromQuery] string? status)
        {
            var auth = Authority ?? authority;
            var st = Status ?? status;
            var (_, html, _) = await _paymentService.HandleZarinPalCallbackAsync(auth, st);
            return Content(html, "text/html; charset=utf-8");
        }

        /// <summary>
        /// Callback زرین‌پال (POST) — برخی مرورگرها/پروکسی‌ها POST می‌فرستند
        /// </summary>
        [HttpPost("callback/zarinpal")]
        [AllowAnonymous]
        [Produces("text/html")]
        public async Task<ContentResult> ZarinPalCallbackPost(
            [FromQuery] string? Authority,
            [FromQuery] string? Status,
            [FromForm] string? authority,
            [FromForm] string? status)
        {
            return await ZarinPalCallbackGet(Authority, Status, authority, status);
        }

        /// <summary>
        /// لغو پرداخت در انتظار
        /// </summary>
        [HttpPost("{id}/cancel")]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<bool>>> CancelPayment(int id)
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _paymentService.CancelPaymentAsync(id, userId);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// تکمیل پرداخت شبیه‌سازی‌شده (بدون ریدایرکت) — برای اپ موبایل تا آماده‌شدن درگاه واقعی
        /// </summary>
        /// <remarks>
        /// وقتی Payment:UseSimulation=true است، این endpoint پرداخت را تأیید می‌کند.
        /// برای شارژ کیف پول موجودی واقعاً افزایش می‌یابد.
        /// فیلد payment.paymentType مشخص می‌کند پرداخت WalletCharge است یا Subscription.
        /// </remarks>
        [HttpPost("{paymentId}/simulate")]
        [ProducesResponseType(typeof(ApiResponse<PaymentResultDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<PaymentResultDto>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<PaymentResultDto>), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ApiResponse<PaymentResultDto>>> SimulatePayment(int paymentId)
        {
            var userId = await GetCurrentUserIdAsync();
            var payment = await _paymentRepository.GetByIdAsync(paymentId);
            if (payment == null || payment.UserId != userId)
            {
                return StatusCode(404, ApiResponse<PaymentResultDto>.NotFound("پرداخت یافت نشد"));
            }

            var result = await _paymentService.SimulateGatewayPaymentAsync(paymentId);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// ریدایرکت به درگاه پرداخت (در حالت شبیه‌سازی: تأیید خودکار و بازگشت به فرانت)
        /// </summary>
        [HttpGet("redirect/{paymentId}")]
        [AllowAnonymous]
        public async Task<ActionResult> RedirectToGateway(int paymentId)
        {
            var payment = await _paymentRepository.GetByIdAsync(paymentId);
            if (payment == null)
            {
                return NotFound("پرداخت یافت نشد");
            }

            // زرین‌پال: هدایت مستقیم به StartPay
            if (string.Equals(payment.Gateway, PaymentGateways.Zarinpal, StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(payment.RefId))
            {
                var sandbox = Configuration.GetValue("ZarinPal:Sandbox", false);
                var startPayBase = sandbox
                    ? "https://sandbox.zarinpal.com/pg/StartPay/"
                    : "https://payment.zarinpal.com/pg/StartPay/";
                return Redirect(startPayBase + payment.RefId);
            }

            var useSimulation = Configuration.GetValue("Payment:UseSimulation", false);
            if (!useSimulation)
            {
                if (string.IsNullOrEmpty(payment.RefId))
                {
                    return NotFound("پرداخت یافت نشد");
                }

                var paymentUrl = Configuration["Payment:Behpardakht:PaymentUrl"]
                    ?? "https://bpm.shaparak.ir/pgwchannel/startpay.mellat";
                return Content(
                    $"<html><body onload=\"document.forms[0].submit()\">" +
                    $"<form method=\"post\" action=\"{paymentUrl}\">" +
                    $"<input type=\"hidden\" name=\"RefId\" value=\"{System.Net.WebUtility.HtmlEncode(payment.RefId)}\" />" +
                    $"</form>در حال انتقال به درگاه...</body></html>",
                    "text/html");
            }

            var result = await _paymentService.SimulateGatewayPaymentAsync(paymentId);
            var paymentInfo = result.Data?.Payment;

            return Redirect(BuildFrontendCallbackUrl(
                paymentId,
                paymentInfo?.PaymentType,
                paymentInfo?.RefId,
                result.Data?.Success == true ? "0" : "15",
                paymentInfo?.OrderId,
                paymentInfo?.ReferenceNumber,
                paymentInfo?.CardNumber));
        }

        private string BuildFrontendCallbackUrl(
            int? paymentId,
            string? paymentType,
            string? refId,
            string? resCode,
            string? orderId,
            string? saleReferenceId,
            string? cardNumber)
        {
            var redirectUrl = Configuration["Payment:Behpardakht:FrontendCallbackUrl"] ?? "/payment/result";
            var queryParams = new List<string>();

            if (paymentId.HasValue) queryParams.Add($"paymentId={paymentId.Value}");
            if (!string.IsNullOrEmpty(paymentType)) queryParams.Add($"paymentType={Uri.EscapeDataString(paymentType)}");
            if (!string.IsNullOrEmpty(refId)) queryParams.Add($"refId={Uri.EscapeDataString(refId)}");
            if (!string.IsNullOrEmpty(resCode)) queryParams.Add($"resCode={Uri.EscapeDataString(resCode)}");
            if (!string.IsNullOrEmpty(orderId)) queryParams.Add($"orderId={Uri.EscapeDataString(orderId)}");
            if (!string.IsNullOrEmpty(saleReferenceId)) queryParams.Add($"saleReferenceId={Uri.EscapeDataString(saleReferenceId)}");
            if (!string.IsNullOrEmpty(cardNumber)) queryParams.Add($"cardNumber={Uri.EscapeDataString(cardNumber)}");

            return queryParams.Count > 0 ? $"{redirectUrl}?{string.Join("&", queryParams)}" : redirectUrl;
        }
    }
}




