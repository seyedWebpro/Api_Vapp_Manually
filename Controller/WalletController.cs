using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.Wallet;
using Api_Vapp.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Api_Vapp.Controller
{
    /// <summary>
    /// کنترلر مدیریت کیف پول
    /// </summary>
    /// <remarks>
    /// این کنترلر شامل تمام endpoint های مربوط به مدیریت کیف پول کاربر می‌باشد.
    /// 
    /// **قابلیت‌های اصلی:**
    /// - مشاهده موجودی و اطلاعات کیف پول
    /// - دریافت تاریخچه تراکنش‌ها
    /// - شارژ کیف پول از طریق درگاه پرداخت
    /// - بررسی موجودی کافی
    /// 
    /// تمام endpoint های این کنترلر نیاز به احراز هویت دارند.
    /// </remarks>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    [Produces("application/json")]
    public class WalletController : VappControllerBase
    {
        private readonly IWalletService _walletService;

        public WalletController(
            IWalletService walletService,
            IConfiguration configuration,
            IUserRepository userRepository)
            : base(configuration, userRepository)
        {
            _walletService = walletService;
        }

        /// <summary>
        /// دریافت اطلاعات کامل صفحه کیف پول
        /// </summary>
        /// <param name="recentTransactionsCount">تعداد تراکنش‌های اخیر (پیش‌فرض: 10، حداکثر: 50)</param>
        /// <returns>پاسخ شامل اطلاعات کامل صفحه کیف پول</returns>
        /// <remarks>
        /// این endpoint تمام اطلاعات مورد نیاز صفحه کیف پول را یکجا برمی‌گرداند:
        /// - موجودی کیف پول
        /// - لیست کش‌بک‌های فعال با جزئیات
        /// - تاریخچه مالی (آخرین تراکنش‌ها)
        /// 
        /// **نکات مهم:**
        /// - این endpoint برای بهینه‌سازی درخواست‌ها طراحی شده است
        /// - تمام اطلاعات مورد نیاز در یک درخواست برگردانده می‌شود
        /// </remarks>
        /// <response code="200">اطلاعات صفحه کیف پول با موفقیت برگردانده شد</response>
        /// <response code="500">خطای سرور</response>
        [HttpGet("page")]
        [ProducesResponseType(typeof(ApiResponse<WalletPageDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<WalletPageDto>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<WalletPageDto>>> GetWalletPage(
            [FromQuery] int recentTransactionsCount = 10)
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _walletService.GetWalletPageAsync(userId, recentTransactionsCount);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// دریافت اطلاعات کیف پول
        /// </summary>
        /// <returns>پاسخ شامل اطلاعات کیف پول</returns>
        /// <remarks>
        /// این endpoint اطلاعات کلی کیف پول را برمی‌گرداند.
        /// 
        /// **اطلاعات شامل:**
        /// - موجودی فعلی
        /// - تعداد کش‌بک‌های فعال
        /// - تعداد کل تراکنش‌ها
        /// </remarks>
        /// <response code="200">اطلاعات کیف پول با موفقیت برگردانده شد</response>
        /// <response code="500">خطای سرور</response>
        [HttpGet("info")]
        [ProducesResponseType(typeof(ApiResponse<WalletInfoDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<WalletInfoDto>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<WalletInfoDto>>> GetWalletInfo()
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _walletService.GetWalletInfoAsync(userId);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// دریافت موجودی کیف پول
        /// </summary>
        /// <returns>پاسخ شامل موجودی فعلی کیف پول (به تومان)</returns>
        /// <remarks>
        /// این endpoint موجودی فعلی کیف پول کاربر را برمی‌گرداند.
        /// 
        /// **نکات مهم:**
        /// - موجودی به تومان برگردانده می‌شود
        /// - همچنین موجودی فرمت شده (با جداکننده هزارگان) نیز برگردانده می‌شود
        /// </remarks>
        /// <response code="200">موجودی کیف پول با موفقیت برگردانده شد</response>
        /// <response code="500">خطای سرور</response>
        [HttpGet("balance")]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<decimal>>> GetBalance()
        {
            var userId = await GetCurrentUserIdAsync();
            var balance = await _walletService.GetBalanceAsync(userId);
            return StatusCode(200, ApiResponse<object>.CreateSuccess(new 
            { 
                Balance = balance,
                FormattedBalance = $"{balance:N0} تومان"
            }));
        }

        /// <summary>
        /// دریافت لیست تراکنش‌های کیف پول با pagination
        /// </summary>
        /// <param name="pageNumber">شماره صفحه (پیش‌فرض: 1)</param>
        /// <param name="pageSize">تعداد در هر صفحه (پیش‌فرض: 10، حداکثر: 100)</param>
        /// <returns>پاسخ شامل لیست تراکنش‌ها و اطلاعات pagination</returns>
        /// <remarks>
        /// این endpoint لیست تمام تراکنش‌های کیف پول کاربر را با امکان pagination برمی‌گرداند.
        /// 
        /// **اطلاعات هر تراکنش شامل:**
        /// - نوع تراکنش (شارژ، برداشت، کش‌بک)
        /// - مبلغ
        /// - تاریخ و زمان
        /// - وضعیت
        /// - توضیحات
        /// </remarks>
        /// <response code="200">لیست تراکنش‌ها با موفقیت برگردانده شد</response>
        /// <response code="400">پارامترهای ورودی نامعتبر است</response>
        /// <response code="500">خطای سرور</response>
        [HttpGet("transactions")]
        [ProducesResponseType(typeof(ApiResponse<WalletTransactionListDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<WalletTransactionListDto>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<WalletTransactionListDto>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<WalletTransactionListDto>>> GetTransactions(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10)
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _walletService.GetTransactionsAsync(userId, pageNumber, pageSize);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// دریافت آخرین تراکنش‌های کیف پول
        /// </summary>
        /// <param name="count">تعداد تراکنش‌ها (پیش‌فرض: 5، حداکثر: 20)</param>
        /// <returns>پاسخ شامل لیست آخرین تراکنش‌ها</returns>
        /// <remarks>
        /// این endpoint آخرین تراکنش‌های کیف پول کاربر را برمی‌گرداند.
        /// 
        /// **نکات مهم:**
        /// - تراکنش‌ها به ترتیب تاریخ (جدیدترین اول) برگردانده می‌شوند
        /// - برای نمایش در داشبورد یا صفحه اصلی استفاده می‌شود
        /// </remarks>
        /// <response code="200">لیست آخرین تراکنش‌ها با موفقیت برگردانده شد</response>
        /// <response code="400">تعداد درخواستی نامعتبر است</response>
        /// <response code="500">خطای سرور</response>
        [HttpGet("transactions/recent")]
        [ProducesResponseType(typeof(ApiResponse<List<WalletTransactionDto>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<List<WalletTransactionDto>>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<List<WalletTransactionDto>>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<List<WalletTransactionDto>>>> GetRecentTransactions(
            [FromQuery] int count = 5)
        {
            if (count < 1) count = 1;
            if (count > 20) count = 20;

            var userId = await GetCurrentUserIdAsync();
            var result = await _walletService.GetRecentTransactionsAsync(userId, count);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// درخواست شارژ کیف پول
        /// </summary>
        /// <param name="request">اطلاعات درخواست شارژ شامل مبلغ و درگاه پرداخت</param>
        /// <returns>پاسخ شامل URL درگاه پرداخت و اطلاعات پرداخت</returns>
        /// <remarks>
        /// این endpoint یک درخواست پرداخت ایجاد می‌کند و URL درگاه پرداخت را برمی‌گرداند.
        /// 
        /// **محدودیت‌های مبلغ:**
        /// - حداقل: 10,000 تومان
        /// - حداکثر: 100,000,000 تومان
        /// 
        /// **درگاه‌های موجود:**
        /// - Behpardakht: به‌پرداخت ملت
        /// 
        /// **فرآیند شارژ:**
        /// 1. ایجاد درخواست پرداخت
        /// 2. دریافت URL درگاه پرداخت
        /// 3. هدایت کاربر به درگاه
        /// 4. پس از پرداخت موفق، موجودی کیف پول افزایش می‌یابد
        /// </remarks>
        /// <response code="200">درخواست شارژ با موفقیت ایجاد شد و URL درگاه برگردانده شد</response>
        /// <response code="400">داده‌های ورودی نامعتبر است یا مبلغ خارج از محدوده است</response>
        /// <response code="500">خطای سرور</response>
        [HttpPost("charge")]
        [ProducesResponseType(typeof(ApiResponse<ChargeWalletResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<ChargeWalletResponseDto>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<ChargeWalletResponseDto>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<ChargeWalletResponseDto>>> ChargeWallet(
            [FromBody] ChargeWalletRequestDto request)
        {
            if (!ModelState.IsValid)
            {
                var errors = ExtractModelStateErrors();
                return StatusCode(400, ApiResponse<ChargeWalletResponseDto>.BadRequest("داده‌های ورودی نامعتبر است", errors));
            }

            var userId = await GetCurrentUserIdAsync();
            var result = await _walletService.ChargeWalletAsync(userId, request);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// بررسی موجودی کافی برای مبلغ مشخص
        /// </summary>
        /// <param name="amount">مبلغ مورد نیاز (تومان)</param>
        /// <returns>پاسخ شامل وضعیت موجودی و اطلاعات مرتبط</returns>
        /// <remarks>
        /// این endpoint بررسی می‌کند که آیا موجودی کیف پول برای مبلغ مشخص کافی است یا نه.
        /// 
        /// **اطلاعات برگردانده شده شامل:**
        /// - وضعیت موجودی کافی (HasSufficientBalance)
        /// - موجودی فعلی
        /// - مبلغ مورد نیاز
        /// - کمبود (در صورت ناکافی بودن)
        /// 
        /// **نکات مهم:**
        /// - مبلغ باید بزرگتر از صفر باشد
        /// - از این endpoint قبل از انجام عملیات هزینه‌بر استفاده کنید
        /// </remarks>
        /// <response code="200">بررسی موجودی با موفقیت انجام شد</response>
        /// <response code="400">مبلغ باید بزرگتر از صفر باشد</response>
        /// <response code="500">خطای سرور</response>
        [HttpGet("check-balance/{amount}")]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<object>>> CheckBalance(decimal amount)
        {
            if (amount <= 0)
            {
                return StatusCode(400, ApiResponse<object>.BadRequest("مبلغ باید بزرگتر از صفر باشد"));
            }

            var userId = await GetCurrentUserIdAsync();
            var hasSufficient = await _walletService.HasSufficientBalanceAsync(userId, amount);
            var balance = await _walletService.GetBalanceAsync(userId);

            return StatusCode(200, ApiResponse<object>.CreateSuccess(new
            {
                HasSufficientBalance = hasSufficient,
                CurrentBalance = balance,
                FormattedBalance = $"{balance:N0} تومان",
                RequiredAmount = amount,
                FormattedRequired = $"{amount:N0} تومان",
                Shortage = hasSufficient ? 0 : amount - balance,
                FormattedShortage = hasSufficient ? "0 تومان" : $"{(amount - balance):N0} تومان"
            }));
        }
    }
}




