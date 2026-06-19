using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.Payment;
using Api_Vapp.Interfaces;
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

        public PaymentController(
            IPaymentService paymentService,
            IConfiguration configuration,
            IUserRepository userRepository)
            : base(configuration, userRepository)
        {
            _paymentService = paymentService;
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
        /// <param name="PaymentId">شناسه پرداخت</param>
        /// <param name="RefId">شماره مرجع</param>
        /// <param name="ResCode">کد نتیجه</param>
        /// <param name="SaleOrderId">شماره سفارش</param>
        /// <param name="SaleReferenceId">شماره مرجع فروش</param>
        /// <param name="CardHolderPan">شماره کارت</param>
        /// <returns>ریدایرکت به صفحه نتیجه پرداخت</returns>
        /// <remarks>
        /// این endpoint توسط درگاه به‌پرداخت بعد از پرداخت فراخوانی می‌شود.
        /// 
        /// **نکات مهم:**
        /// - این endpoint نیاز به احراز هویت ندارد (AllowAnonymous)
        /// - کاربر را به صفحه نتیجه پرداخت در فرانت‌اند هدایت می‌کند
        /// - اطلاعات پرداخت در Query String به فرانت‌اند ارسال می‌شود
        /// </remarks>
        /// <response code="302">ریدایرکت به صفحه نتیجه پرداخت</response>
        [HttpGet("callback/behpardakht")]
        [AllowAnonymous]
        public Task<ActionResult> BehpardakhtCallbackGet(
            [FromQuery] int? PaymentId,
            [FromQuery] string? RefId,
            [FromQuery] string? ResCode,
            [FromQuery] string? SaleOrderId,
            [FromQuery] string? SaleReferenceId,
            [FromQuery] string? CardHolderPan)
        {
            // ریدایرکت به فرانت‌اند با پارامترها
            var redirectUrl = Configuration["Payment:Behpardakht:FrontendCallbackUrl"] ?? "/payment/result";
            
            var queryParams = new List<string>();
            if (PaymentId.HasValue) queryParams.Add($"paymentId={PaymentId}");
            if (!string.IsNullOrEmpty(RefId)) queryParams.Add($"refId={RefId}");
            if (!string.IsNullOrEmpty(ResCode)) queryParams.Add($"resCode={ResCode}");
            if (!string.IsNullOrEmpty(SaleOrderId)) queryParams.Add($"orderId={SaleOrderId}");
            if (!string.IsNullOrEmpty(SaleReferenceId)) queryParams.Add($"saleReferenceId={SaleReferenceId}");
            if (!string.IsNullOrEmpty(CardHolderPan)) queryParams.Add($"cardNumber={CardHolderPan}");

            var fullUrl = queryParams.Any() ? $"{redirectUrl}?{string.Join("&", queryParams)}" : redirectUrl;
            
            return Task.FromResult<ActionResult>(Redirect(fullUrl));
        }

        /// <summary>
        /// Callback درگاه به‌پرداخت (POST)
        /// </summary>
        /// <param name="PaymentId">شناسه پرداخت</param>
        /// <param name="RefId">شماره مرجع</param>
        /// <param name="ResCode">کد نتیجه</param>
        /// <param name="SaleOrderId">شماره سفارش</param>
        /// <param name="SaleReferenceId">شماره مرجع فروش</param>
        /// <param name="CardHolderPan">شماره کارت</param>
        /// <returns>ریدایرکت به صفحه نتیجه پرداخت</returns>
        /// <remarks>
        /// این endpoint توسط درگاه به‌پرداخت بعد از پرداخت فراخوانی می‌شود (POST).
        /// 
        /// **نکات مهم:**
        /// - این endpoint نیاز به احراز هویت ندارد (AllowAnonymous)
        /// - اطلاعات از Form Data دریافت می‌شود
        /// - کاربر را به صفحه نتیجه پرداخت هدایت می‌کند
        /// </remarks>
        /// <response code="302">ریدایرکت به صفحه نتیجه پرداخت</response>
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
        /// لغو پرداخت در انتظار
        /// </summary>
        /// <param name="id">شناسه پرداخت</param>
        /// <returns>پاسخ شامل وضعیت لغو</returns>
        /// <remarks>
        /// این endpoint برای لغو یک پرداخت در وضعیت Pending استفاده می‌شود.
        /// 
        /// **نکات مهم:**
        /// - فقط پرداخت‌های در وضعیت Pending قابل لغو هستند
        /// - پرداخت‌های تأیید شده یا ناموفق قابل لغو نیستند
        /// - پس از لغو، وضعیت پرداخت به Cancelled تغییر می‌کند
        /// </remarks>
        /// <response code="200">پرداخت با موفقیت لغو شد</response>
        /// <response code="400">پرداخت در وضعیت نامعتبر است (قبلاً تأیید یا لغو شده)</response>
        /// <response code="404">پرداخت یافت نشد</response>
        /// <response code="500">خطای سرور</response>
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
        /// ریدایرکت به درگاه پرداخت
        /// </summary>
        /// <param name="paymentId">شناسه پرداخت</param>
        /// <returns>ریدایرکت به درگاه پرداخت یا صفحه شبیه‌سازی</returns>
        /// <remarks>
        /// این endpoint کاربر را به درگاه پرداخت منتقل می‌کند.
        /// 
        /// **نکات مهم:**
        /// - این endpoint نیاز به احراز هویت ندارد (AllowAnonymous)
        /// - در محیط توسعه، به صفحه شبیه‌سازی درگاه هدایت می‌شود
        /// - در محیط production، به درگاه واقعی هدایت می‌شود
        /// </remarks>
        /// <response code="302">ریدایرکت به درگاه پرداخت</response>
        [HttpGet("redirect/{paymentId}")]
        [AllowAnonymous]
        public Task<ActionResult> RedirectToGateway(int paymentId)
        {
            // در اینجا باید پرداخت را از دیتابیس بخوانیم و به درگاه ریدایرکت کنیم
            // برای حالت توسعه، یک صفحه شبیه‌سازی نشان می‌دهیم
            
            var simulationUrl = Configuration["Payment:SimulationUrl"] ?? "/payment/simulation";
            return Task.FromResult<ActionResult>(Redirect($"{simulationUrl}?paymentId={paymentId}"));
        }
    }
}




