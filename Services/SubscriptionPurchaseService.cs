using System.Text.Json;
using Api_Vapp.Constants;
using Api_Vapp.Data;
using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.Payment;
using Api_Vapp.DTOs.Subscription;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;
using Api_Vapp.Services.Audit;
using Api_Vapp.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Api_Vapp.Services
{
    public class SubscriptionPurchaseService : ISubscriptionPurchaseService
    {
        private readonly Api_Context _context;
        private readonly ISubscriptionDiscountService _discountService;
        private readonly ISubscriptionEntitlementService _entitlementService;
        private readonly ISubscriptionActivationService _activationService;
        private readonly IPaymentService _paymentService;
        private readonly IConfiguration _configuration;
        private readonly IAuditService _audit;
        private readonly ILogger<SubscriptionPurchaseService> _logger;

        public SubscriptionPurchaseService(
            Api_Context context,
            ISubscriptionDiscountService discountService,
            ISubscriptionEntitlementService entitlementService,
            ISubscriptionActivationService activationService,
            IPaymentService paymentService,
            IConfiguration configuration,
            IAuditService audit,
            ILogger<SubscriptionPurchaseService> logger)
        {
            _context = context;
            _discountService = discountService;
            _entitlementService = entitlementService;
            _activationService = activationService;
            _paymentService = paymentService;
            _configuration = configuration;
            _audit = audit;
            _logger = logger;
        }

        public async Task<ApiResponse<SubscriptionCheckoutDto>> GetCheckoutPreviewAsync(
            int userId,
            SubscriptionCheckoutPreviewRequest request)
        {
            try
            {
                var planResult = await LoadPurchasablePlanAsync(userId, request.PlanId);
                if (planResult.Error != null)
                    return planResult.Error;

                var plan = planResult.Plan!;
                var pricing = await BuildPricingAsync(userId, plan, request.DiscountCode);
                if (pricing.ErrorMessage != null)
                    return ApiResponse<SubscriptionCheckoutDto>.BadRequest(pricing.ErrorMessage);

                var checkout = await BuildCheckoutDtoAsync(plan, pricing);
                return ApiResponse<SubscriptionCheckoutDto>.CreateSuccess(checkout);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Checkout preview failed for user {UserId}, plan {PlanId}", userId, request.PlanId);
                return ApiResponse<SubscriptionCheckoutDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<SubscriptionPurchaseResultDto>> InitiatePurchaseAsync(
            int userId,
            SubscriptionPurchaseRequest request)
        {
            try
            {
                var planResult = await LoadPurchasablePlanAsync(userId, request.PlanId);
                if (planResult.Error != null)
                    return ApiResponseMapper.MapError<SubscriptionCheckoutDto, SubscriptionPurchaseResultDto>(planResult.Error);

                var plan = planResult.Plan!;
                var pricing = await BuildPricingAsync(userId, plan, request.DiscountCode);
                if (pricing.ErrorMessage != null)
                    return ApiResponse<SubscriptionPurchaseResultDto>.BadRequest(pricing.ErrorMessage);

                var checkout = await BuildCheckoutDtoAsync(plan, pricing);

                if (!pricing.RequiresPayment)
                {
                    var subscription = await _activationService.ActivateAsync(
                        userId,
                        plan,
                        pricing.OriginalAmount,
                        pricing.DiscountAmount,
                        pricing.DiscountCode,
                        pricing.DiscountCodeId,
                        paymentId: null);

                    await _audit.WriteAsync(new AuditEntry
                    {
                        Category = AuditCategories.Subscription,
                        Action = AuditActions.SubscriptionPurchased,
                        EntityType = AuditEntityTypes.UserSubscription,
                        EntityId = subscription.Id.ToString(),
                        ActorUserId = userId,
                        TargetUserId = userId,
                        After = new
                        {
                            occurredAtUtc = DateTime.UtcNow,
                            eventType = "SubscriptionZeroPayActivated",
                            user = PaymentAuditDetails.UserSnapshot(
                                await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId)),
                            planId = plan.Id,
                            planName = plan.Name,
                            tierCode = plan.TierCode,
                            userSubscriptionId = subscription.Id,
                            originalAmount = pricing.OriginalAmount,
                            discountAmount = pricing.DiscountAmount,
                            payableAmount = pricing.PayableAmount,
                            amountLabel = $"{pricing.PayableAmount:N0} تومان",
                            discountCode = pricing.DiscountCode,
                            zeroPayActivation = true
                        }
                    });

                    return ApiResponse<SubscriptionPurchaseResultDto>.CreateSuccess(new SubscriptionPurchaseResultDto
                    {
                        RequiresPayment = false,
                        Checkout = checkout,
                        ActivatedSubscription = MapActivatedSubscription(subscription, plan)
                    }, SubscriptionMessages.ActivationSuccess);
                }

                if (pricing.PayableAmount != decimal.Truncate(pricing.PayableAmount))
                    return ApiResponse<SubscriptionPurchaseResultDto>.BadRequest("مبلغ قابل پرداخت نامعتبر است");

                if (request.Gateway == PaymentGateways.Wallet)
                    return ApiResponse<SubscriptionPurchaseResultDto>.BadRequest(SubscriptionMessages.WalletGatewayComingSoon);

                var isZarinPal = string.Equals(request.Gateway, PaymentGateways.Zarinpal, StringComparison.OrdinalIgnoreCase);
                var isBehpardakht = string.Equals(request.Gateway, PaymentGateways.Behpardakht, StringComparison.OrdinalIgnoreCase);
                if (!isZarinPal && !isBehpardakht)
                    return ApiResponse<SubscriptionPurchaseResultDto>.BadRequest(SubscriptionMessages.UnsupportedGateway);

                var useSimulation = _configuration.GetValue("Payment:UseSimulation", false);
                if (isBehpardakht && !useSimulation)
                    return ApiResponse<SubscriptionPurchaseResultDto>.BadRequest(SubscriptionMessages.UnsupportedGateway);

                var metadata = BuildMetadata(plan, pricing);
                var callbackUrl = request.CallbackUrl
                    ?? _configuration["ZarinPal:AppReturnUrl"]
                    ?? _configuration["Payment:Behpardakht:FrontendCallbackUrl"]
                    ?? "/payment/result";

                var paymentResult = await _paymentService.CreatePaymentAsync(userId, new CreatePaymentDto
                {
                    Amount = pricing.PayableAmount,
                    PaymentType = PaymentTypes.Subscription,
                    Gateway = isZarinPal ? PaymentGateways.Zarinpal : PaymentGateways.Behpardakht,
                    Description = $"خرید اشتراک {plan.Name}",
                    CallbackUrl = callbackUrl
                });

                if (!paymentResult.Success || paymentResult.Data == null)
                {
                    _logger.LogWarning(
                        "Payment creation failed for user {UserId}, plan {PlanId}: {Message}",
                        userId,
                        plan.Id,
                        paymentResult.Message);
                    return ApiResponse<SubscriptionPurchaseResultDto>.BadRequest(SubscriptionMessages.PaymentCreateFailed);
                }

                var payment = await _context.Payments.FirstOrDefaultAsync(p => p.Id == paymentResult.Data.Id);
                if (payment == null)
                {
                    _logger.LogError("Payment {PaymentId} missing after creation for user {UserId}", paymentResult.Data.Id, userId);
                    return ApiResponse<SubscriptionPurchaseResultDto>.InternalServerError(ControlledErrorHelper.Unexpected);
                }

                payment.MetaData = JsonSerializer.Serialize(metadata);
                await _context.SaveChangesAsync();

                string? gatewayUrl;
                string? refId;

                if (isZarinPal)
                {
                    var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
                    var zarinResult = await _paymentService.RequestZarinPalPaymentAsync(
                        payment.Id,
                        payment.Amount,
                        payment.Description ?? $"خرید اشتراک {plan.Name}",
                        mobile: user?.PhoneNumber,
                        orderId: payment.OrderId);

                    if (!zarinResult.Success || string.IsNullOrEmpty(zarinResult.Authority) || string.IsNullOrEmpty(zarinResult.PaymentUrl))
                    {
                        payment.Status = PaymentStatuses.Failed;
                        payment.ErrorMessage = ControlledErrorHelper.PaymentFailed;
                        await _context.SaveChangesAsync();
                        _logger.LogWarning("ZarinPal request failed for subscription payment {PaymentId}", payment.Id);

                        await _audit.WriteAsync(new AuditEntry
                        {
                            Category = AuditCategories.Payment,
                            Action = AuditActions.PaymentRequestFailed,
                            EntityType = AuditEntityTypes.Payment,
                            EntityId = payment.Id.ToString(),
                            ActorUserId = userId,
                            TargetUserId = userId,
                            Succeeded = false,
                            ErrorMessage = zarinResult.ErrorMessage,
                            After = PaymentAuditDetails.SubscriptionRequest(
                                user, payment, plan.Id, plan.Name, plan.TierCode,
                                pricing.OriginalAmount, pricing.DiscountAmount, pricing.PayableAmount,
                                pricing.DiscountCode, null)
                        });

                        return ApiResponse<SubscriptionPurchaseResultDto>.BadRequest(
                            ControlledErrorHelper.PaymentFailed,
                            errorCode: ErrorCodes.PaymentFailed);
                    }

                    refId = zarinResult.Authority;
                    gatewayUrl = zarinResult.PaymentUrl;
                }
                else
                {
                    var tokenResult = await _paymentService.RequestBehpardakhtTokenAsync(
                        payment.Id,
                        payment.Amount,
                        payment.OrderId,
                        callbackUrl);

                    if (!tokenResult.Success || string.IsNullOrEmpty(tokenResult.RefId))
                    {
                        payment.Status = PaymentStatuses.Failed;
                        payment.ErrorMessage = ControlledErrorHelper.PaymentFailed;
                        await _context.SaveChangesAsync();
                        _logger.LogWarning("Gateway token failed for payment {PaymentId}", payment.Id);

                        await _audit.WriteAsync(new AuditEntry
                        {
                            Category = AuditCategories.Payment,
                            Action = AuditActions.PaymentRequestFailed,
                            EntityType = AuditEntityTypes.Payment,
                            EntityId = payment.Id.ToString(),
                            ActorUserId = userId,
                            Succeeded = false,
                            ErrorMessage = tokenResult.ErrorMessage,
                            Metadata = new
                            {
                                planId = plan.Id,
                                amount = payment.Amount,
                                gateway = request.Gateway
                            }
                        });

                        return ApiResponse<SubscriptionPurchaseResultDto>.BadRequest(
                            ControlledErrorHelper.PaymentFailed,
                            errorCode: ErrorCodes.PaymentFailed);
                    }

                    refId = tokenResult.RefId;
                    var apiBaseUrl = _configuration["Payment:ApiBaseUrl"]?.TrimEnd('/') ?? string.Empty;
                    var redirectPath = $"/api/Payment/redirect/{payment.Id}";
                    gatewayUrl = string.IsNullOrEmpty(apiBaseUrl) ? redirectPath : $"{apiBaseUrl}{redirectPath}";
                }

                payment.RefId = refId;
                payment.Status = PaymentStatuses.Processing;
                await _context.SaveChangesAsync();

                var purchaseUser = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
                _logger.LogInformation(
                    "درخواست خرید اشتراک. UserId={UserId} Phone={Phone} PlanId={PlanId} Plan={PlanName} Amount={Amount} PaymentId={PaymentId} Authority={Authority} Gateway={Gateway}",
                    userId, purchaseUser?.PhoneNumber, plan.Id, plan.Name, pricing.PayableAmount, payment.Id, payment.RefId, payment.Gateway);

                await _audit.WriteAsync(new AuditEntry
                {
                    Category = AuditCategories.Subscription,
                    Action = AuditActions.SubscriptionPurchased,
                    EntityType = AuditEntityTypes.Payment,
                    EntityId = payment.Id.ToString(),
                    ActorUserId = userId,
                    TargetUserId = userId,
                    After = PaymentAuditDetails.SubscriptionRequest(
                        purchaseUser,
                        payment,
                        plan.Id,
                        plan.Name,
                        plan.TierCode,
                        pricing.OriginalAmount,
                        pricing.DiscountAmount,
                        pricing.PayableAmount,
                        pricing.DiscountCode,
                        gatewayUrl)
                });

                return ApiResponse<SubscriptionPurchaseResultDto>.CreateSuccess(new SubscriptionPurchaseResultDto
                {
                    RequiresPayment = true,
                    PaymentId = payment.Id,
                    OrderId = payment.OrderId,
                    RefId = payment.RefId,
                    RedirectUrl = gatewayUrl,
                    GatewayUrl = gatewayUrl,
                    Checkout = checkout
                }, SubscriptionMessages.RedirectToGateway);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Purchase initiation failed for user {UserId}, plan {PlanId}", userId, request.PlanId);
                return ApiResponse<SubscriptionPurchaseResultDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        private async Task<(SubscriptionPlan? Plan, ApiResponse<SubscriptionCheckoutDto>? Error)> LoadPurchasablePlanAsync(
            int userId,
            int planId)
        {
            var plan = await _context.SubscriptionPlans.AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == planId && p.IsActive && !p.IsDeleted);

            if (plan == null)
                return (null, ApiResponse<SubscriptionCheckoutDto>.NotFound(SubscriptionMessages.PlanNotFound));

            if (IsFreePlan(plan))
                return (null, ApiResponse<SubscriptionCheckoutDto>.BadRequest(SubscriptionMessages.FreePlanNotPurchasable));

            var activeSubscription = await _entitlementService.GetActiveSubscriptionAsync(userId);
            if (activeSubscription?.SubscriptionPlanId == plan.Id)
                return (null, ApiResponse<SubscriptionCheckoutDto>.BadRequest(SubscriptionMessages.PlanAlreadyActive));

            return (plan, null);
        }

        private async Task<PricingResult> BuildPricingAsync(int userId, SubscriptionPlan plan, string? discountCode)
        {
            var originalAmount = plan.Price;
            var (discount, discountAmount, errorMessage) = await _discountService.CalculateAsync(
                userId,
                plan.Id,
                originalAmount,
                discountCode);

            if (!string.IsNullOrEmpty(errorMessage))
                return new PricingResult { ErrorMessage = errorMessage };

            var payableAmount = Math.Max(0, originalAmount - discountAmount);
            return new PricingResult
            {
                OriginalAmount = originalAmount,
                DiscountAmount = discountAmount,
                PayableAmount = payableAmount,
                DiscountCode = discount?.Code,
                DiscountCodeId = discount?.Id,
                RequiresPayment = payableAmount > 0
            };
        }

        private async Task<SubscriptionCheckoutDto> BuildCheckoutDtoAsync(SubscriptionPlan plan, PricingResult pricing)
        {
            var gatewaysResult = await _paymentService.GetAvailableGatewaysAsync();
            return new SubscriptionCheckoutDto
            {
                PlanId = plan.Id,
                PlanName = plan.Name,
                TierCode = plan.TierCode,
                DurationDays = plan.DurationDays,
                OriginalAmount = pricing.OriginalAmount,
                FormattedOriginalAmount = FormatAmount(pricing.OriginalAmount),
                DiscountAmount = pricing.DiscountAmount,
                FormattedDiscountAmount = pricing.DiscountAmount > 0
                    ? FormatAmount(pricing.DiscountAmount)
                    : "—",
                PayableAmount = pricing.PayableAmount,
                FormattedPayableAmount = FormatAmount(pricing.PayableAmount),
                AppliedDiscountCode = pricing.DiscountCode,
                RequiresPayment = pricing.RequiresPayment,
                PaymentGateways = gatewaysResult.Data ?? new List<PaymentGatewayInfoDto>()
            };
        }

        private static SubscriptionPaymentMetadata BuildMetadata(SubscriptionPlan plan, PricingResult pricing) => new()
        {
            SubscriptionPlanId = plan.Id,
            PlanName = plan.Name,
            TierCode = plan.TierCode,
            OriginalAmount = pricing.OriginalAmount,
            DiscountAmount = pricing.DiscountAmount,
            PayableAmount = pricing.PayableAmount,
            DiscountCode = pricing.DiscountCode,
            DiscountCodeId = pricing.DiscountCodeId,
            DurationDays = plan.DurationDays
        };

        private static CurrentSubscriptionDto MapActivatedSubscription(UserSubscription subscription, SubscriptionPlan plan)
        {
            var remainingDays = Math.Max(0, (int)Math.Ceiling((subscription.ExpiresAt - DateTime.UtcNow).TotalDays));
            return new CurrentSubscriptionDto
            {
                UserSubscriptionId = subscription.Id,
                PlanId = plan.Id,
                PlanName = plan.Name,
                TierCode = plan.TierCode,
                StartDate = subscription.StartDate,
                ExpiresAt = subscription.ExpiresAt,
                RemainingDays = remainingDays,
                IsActive = true,
                IsFreePlan = false
            };
        }

        private static bool IsFreePlan(SubscriptionPlan plan) =>
            plan.Price <= 0
            || string.Equals(plan.TierCode, SubscriptionPlanTierCodes.Free, StringComparison.OrdinalIgnoreCase);

        private static string FormatAmount(decimal amount) => $"{amount:N0} تومان";

        private sealed class PricingResult
        {
            public decimal OriginalAmount { get; set; }
            public decimal DiscountAmount { get; set; }
            public decimal PayableAmount { get; set; }
            public string? DiscountCode { get; set; }
            public int? DiscountCodeId { get; set; }
            public bool RequiresPayment { get; set; }
            public string? ErrorMessage { get; set; }
        }
    }
}
