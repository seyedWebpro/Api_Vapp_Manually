using System.ComponentModel.DataAnnotations;
using Api_Vapp.DTOs.Payment;
using Api_Vapp.Models;

namespace Api_Vapp.DTOs.Subscription
{
    public class SubscriptionCheckoutPreviewRequest
    {
        [Required(ErrorMessage = "شناسه پلن الزامی است")]
        [Range(1, int.MaxValue, ErrorMessage = "شناسه پلن نامعتبر است")]
        public int PlanId { get; set; }

        [MaxLength(50, ErrorMessage = "کد تخفیف نامعتبر است")]
        public string? DiscountCode { get; set; }
    }

    public class SubscriptionCheckoutDto
    {
        public string OperationTitle { get; set; } = "خرید اشتراک";
        public int PlanId { get; set; }
        public string PlanName { get; set; } = string.Empty;
        public string TierCode { get; set; } = string.Empty;
        public int DurationDays { get; set; }
        public decimal OriginalAmount { get; set; }
        public string FormattedOriginalAmount { get; set; } = string.Empty;
        public decimal DiscountAmount { get; set; }
        public string FormattedDiscountAmount { get; set; } = string.Empty;
        public decimal PayableAmount { get; set; }
        public string FormattedPayableAmount { get; set; } = string.Empty;
        public string? AppliedDiscountCode { get; set; }
        public bool RequiresPayment { get; set; }
        public List<PaymentGatewayInfoDto> PaymentGateways { get; set; } = new();
    }

    public class SubscriptionPurchaseRequest
    {
        [Required(ErrorMessage = "شناسه پلن الزامی است")]
        [Range(1, int.MaxValue, ErrorMessage = "شناسه پلن نامعتبر است")]
        public int PlanId { get; set; }

        [MaxLength(50, ErrorMessage = "کد تخفیف نامعتبر است")]
        public string? DiscountCode { get; set; }

        [Required(ErrorMessage = "درگاه پرداخت الزامی است")]
        public string Gateway { get; set; } = PaymentGateways.Behpardakht;

        [MaxLength(500, ErrorMessage = "آدرس بازگشت نامعتبر است")]
        public string? CallbackUrl { get; set; }
    }

    public class SubscriptionPurchaseResultDto
    {
        public bool RequiresPayment { get; set; }
        public int? PaymentId { get; set; }
        public string? OrderId { get; set; }
        public string? RefId { get; set; }
        public string? RedirectUrl { get; set; }
        public SubscriptionCheckoutDto Checkout { get; set; } = new();
        public CurrentSubscriptionDto? ActivatedSubscription { get; set; }
    }
}
