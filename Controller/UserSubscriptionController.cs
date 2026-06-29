using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.Subscription;
using Api_Vapp.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api_Vapp.Controller
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    [Produces("application/json")]
    public class UserSubscriptionController : VappControllerBase
    {
        private readonly IUserSubscriptionService _catalogService;
        private readonly ISubscriptionPurchaseService _purchaseService;

        public UserSubscriptionController(
            IUserSubscriptionService catalogService,
            ISubscriptionPurchaseService purchaseService,
            IConfiguration configuration,
            IUserRepository userRepository)
            : base(configuration, userRepository)
        {
            _catalogService = catalogService;
            _purchaseService = purchaseService;
        }

        /// <summary>
        /// کاتالوگ اشتراک‌ها — اشتراک فعلی + لیست پلن‌ها
        /// </summary>
        [HttpGet("catalog")]
        [ProducesResponseType(typeof(ApiResponse<SubscriptionCatalogDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<SubscriptionCatalogDto>>> GetCatalog()
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _catalogService.GetCatalogAsync(userId);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// پیش‌نمایش خلاصه خرید (با کد تخفیف اختیاری)
        /// </summary>
        [HttpPost("checkout/preview")]
        [ProducesResponseType(typeof(ApiResponse<SubscriptionCheckoutDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<SubscriptionCheckoutDto>>> PreviewCheckout(
            [FromBody] SubscriptionCheckoutPreviewRequest request)
        {
            var invalid = InvalidModelStateResponse<SubscriptionCheckoutDto>();
            if (invalid != null) return invalid;

            var userId = await GetCurrentUserIdAsync();
            var result = await _purchaseService.GetCheckoutPreviewAsync(userId, request);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// شروع خرید اشتراک — ایجاد پرداخت و دریافت URL درگاه (یا فعال‌سازی مستقیم در تخفیف ۱۰۰٪)
        /// </summary>
        [HttpPost("purchase")]
        [ProducesResponseType(typeof(ApiResponse<SubscriptionPurchaseResultDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<SubscriptionPurchaseResultDto>>> Purchase(
            [FromBody] SubscriptionPurchaseRequest request)
        {
            var invalid = InvalidModelStateResponse<SubscriptionPurchaseResultDto>();
            if (invalid != null) return invalid;

            var userId = await GetCurrentUserIdAsync();
            var result = await _purchaseService.InitiatePurchaseAsync(userId, request);
            return StatusCode(result.StatusCode, result);
        }
    }
}
