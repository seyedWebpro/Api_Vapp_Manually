using Api_Vapp.Constants;
using Api_Vapp.DTOs.Admin;
using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.Subscription;
using Api_Vapp.Exceptions;
using Api_Vapp.Models;
using Api_Vapp.Tests.Shared;
using Xunit;

namespace Api_Vapp.Tests.Subscription;

public class SubscriptionServiceTests : IAsyncLifetime
{
    private SubscriptionTestContext _ctx = null!;

    public async Task InitializeAsync()
    {
        _ctx = await SubscriptionTestContext.CreateAsync();
        await _ctx.BeginTestTransactionAsync();
    }

    public async Task DisposeAsync()
    {
        await _ctx.RollbackTestTransactionAsync();
        _ctx.Dispose();
    }

    [Fact]
    public async Task GetCatalog_Returns200_WithFreePlanAsCurrent()
    {
        var result = await _ctx.CatalogService.GetCatalogAsync(_ctx.UserId);

        ApiTestAssertions.AssertSuccess(result);
        Assert.NotNull(result.Data!.CurrentSubscription);
        Assert.True(result.Data.CurrentSubscription.IsFreePlan);
        Assert.Equal(_ctx.FreePlanId, result.Data.CurrentSubscription.PlanId);
        Assert.True(result.Data.Plans.Count >= 3);
    }

    [Fact]
    public async Task GetCatalog_PaidPlanMarkedAsPurchasable()
    {
        var result = await _ctx.CatalogService.GetCatalogAsync(_ctx.UserId);

        ApiTestAssertions.AssertSuccess(result);
        var plus = result.Data!.Plans.First(p => p.Id == _ctx.PlusPlanId);
        Assert.False(plus.IsFree);
        Assert.True(plus.CanPurchase);
        Assert.False(plus.IsCurrentPlan);
    }

    [Fact]
    public async Task CheckoutPreview_ValidPlusPlan_Returns200()
    {
        var result = await _ctx.PurchaseService.GetCheckoutPreviewAsync(
            _ctx.UserId,
            new SubscriptionCheckoutPreviewRequest { PlanId = _ctx.PlusPlanId });

        ApiTestAssertions.AssertSuccess(result);
        Assert.Equal(_ctx.PlusPlanId, result.Data!.PlanId);
        Assert.True(result.Data.RequiresPayment);
        Assert.True(result.Data.PayableAmount > 0);
        Assert.Equal(2, result.Data.PaymentGateways.Count);
    }

    [Fact]
    public async Task CheckoutPreview_InvalidPlanId_Returns404()
    {
        var result = await _ctx.PurchaseService.GetCheckoutPreviewAsync(
            _ctx.UserId,
            new SubscriptionCheckoutPreviewRequest { PlanId = 99999999 });

        ApiTestAssertions.AssertClientError(result, 404);
        Assert.Equal(ErrorCodes.NotFound, result.ErrorCode);
    }

    [Fact]
    public async Task CheckoutPreview_FreePlan_Returns400()
    {
        var result = await _ctx.PurchaseService.GetCheckoutPreviewAsync(
            _ctx.UserId,
            new SubscriptionCheckoutPreviewRequest { PlanId = _ctx.FreePlanId });

        ApiTestAssertions.AssertClientError(result, 400);
    }

    [Fact]
    public async Task CheckoutPreview_InvalidDiscountCode_Returns400()
    {
        var result = await _ctx.PurchaseService.GetCheckoutPreviewAsync(
            _ctx.UserId,
            new SubscriptionCheckoutPreviewRequest
            {
                PlanId = _ctx.PlusPlanId,
                DiscountCode = "INVALID-CODE-XYZ"
            });

        ApiTestAssertions.AssertClientError(result, 400);
    }

    [Fact]
    public async Task CheckoutPreview_ValidFixedDiscount_Returns200WithReducedAmount()
    {
        var code = await _ctx.CreateDiscountCodeAsync(50_000, planId: _ctx.PlusPlanId);

        var result = await _ctx.PurchaseService.GetCheckoutPreviewAsync(
            _ctx.UserId,
            new SubscriptionCheckoutPreviewRequest
            {
                PlanId = _ctx.PlusPlanId,
                DiscountCode = code
            });

        ApiTestAssertions.AssertSuccess(result);
        Assert.Equal(code, result.Data!.AppliedDiscountCode);
        Assert.Equal(50_000, result.Data.DiscountAmount);
        Assert.True(result.Data.PayableAmount < result.Data.OriginalAmount);
    }

    [Fact]
    public async Task CheckoutPreview_FullDiscount_DoesNotRequirePayment()
    {
        var plusPrice = await _ctx.GetPlanPriceAsync(_ctx.PlusPlanId);
        var code = await _ctx.CreateDiscountCodeAsync(plusPrice, planId: _ctx.PlusPlanId);

        var result = await _ctx.PurchaseService.GetCheckoutPreviewAsync(
            _ctx.UserId,
            new SubscriptionCheckoutPreviewRequest
            {
                PlanId = _ctx.PlusPlanId,
                DiscountCode = code
            });

        ApiTestAssertions.AssertSuccess(result);
        Assert.False(result.Data!.RequiresPayment);
        Assert.Equal(0, result.Data.PayableAmount);
    }

    [Fact]
    public async Task Purchase_WalletGateway_Returns400_Controlled()
    {
        var result = await _ctx.PurchaseService.InitiatePurchaseAsync(
            _ctx.UserId,
            new SubscriptionPurchaseRequest
            {
                PlanId = _ctx.PlusPlanId,
                Gateway = PaymentGateways.Wallet
            });

        ApiTestAssertions.AssertClientError(result, 400);
    }

    [Fact]
    public async Task Purchase_UnsupportedGateway_Returns400_Controlled()
    {
        var result = await _ctx.PurchaseService.InitiatePurchaseAsync(
            _ctx.UserId,
            new SubscriptionPurchaseRequest
            {
                PlanId = _ctx.PlusPlanId,
                Gateway = "UnknownGateway"
            });

        ApiTestAssertions.AssertClientError(result, 400);
    }

    [Fact]
    public async Task Purchase_Behpardakht_Returns200WithRedirectUrl()
    {
        var result = await _ctx.PurchaseService.InitiatePurchaseAsync(
            _ctx.UserId,
            new SubscriptionPurchaseRequest
            {
                PlanId = _ctx.PlusPlanId,
                Gateway = PaymentGateways.Behpardakht
            });

        ApiTestAssertions.AssertSuccess(result);
        Assert.True(result.Data!.RequiresPayment);
        Assert.NotNull(result.Data.PaymentId);
        Assert.NotNull(result.Data.RedirectUrl);
        Assert.Contains("/api/Payment/redirect/", result.Data.RedirectUrl);
        Assert.NotNull(result.Data.RefId);
    }

    [Fact]
    public async Task Purchase_FullDiscount_ActivatesWithoutPayment()
    {
        var plusPrice = await _ctx.GetPlanPriceAsync(_ctx.PlusPlanId);
        var code = await _ctx.CreateDiscountCodeAsync(plusPrice, planId: _ctx.PlusPlanId);

        var result = await _ctx.PurchaseService.InitiatePurchaseAsync(
            _ctx.UserId,
            new SubscriptionPurchaseRequest
            {
                PlanId = _ctx.PlusPlanId,
                DiscountCode = code,
                Gateway = PaymentGateways.Behpardakht
            });

        ApiTestAssertions.AssertSuccess(result);
        Assert.False(result.Data!.RequiresPayment);
        Assert.NotNull(result.Data.ActivatedSubscription);
        Assert.Equal(_ctx.PlusPlanId, result.Data.ActivatedSubscription!.PlanId);
        Assert.True(result.Data.ActivatedSubscription.IsActive);
    }

    [Fact]
    public async Task Purchase_AlreadyActivePlan_Returns400()
    {
        await ActivatePlusPlanForUserAsync();

        var result = await _ctx.PurchaseService.InitiatePurchaseAsync(
            _ctx.UserId,
            new SubscriptionPurchaseRequest
            {
                PlanId = _ctx.PlusPlanId,
                Gateway = PaymentGateways.Behpardakht
            });

        ApiTestAssertions.AssertClientError(result, 400);
    }

    [Fact]
    public async Task HasFeature_FreeUser_HasMessaging_NotFormBuilder()
    {
        var hasMessaging = await _ctx.EntitlementService.HasFeatureAsync(
            _ctx.UserId,
            SubscriptionFeatureCodes.Messaging);
        var hasFormBuilder = await _ctx.EntitlementService.HasFeatureAsync(
            _ctx.UserId,
            SubscriptionFeatureCodes.FormBuilder);

        Assert.True(hasMessaging);
        Assert.False(hasFormBuilder);
    }

    [Fact]
    public async Task HasFeature_AfterPlusPurchase_HasFormBuilder()
    {
        await ActivatePlusPlanForUserAsync();

        var hasFormBuilder = await _ctx.EntitlementService.HasFeatureAsync(
            _ctx.UserId,
            SubscriptionFeatureCodes.FormBuilder);

        Assert.True(hasFormBuilder);
    }

    [Fact]
    public async Task HasFeature_UnknownCode_ReturnsFalse()
    {
        var result = await _ctx.EntitlementService.HasFeatureAsync(_ctx.UserId, "unknown_feature_xyz");
        Assert.False(result);
    }

    [Fact]
    public async Task FulfillPayment_ValidSubscriptionPayment_ActivatesPlan()
    {
        var purchase = await _ctx.PurchaseService.InitiatePurchaseAsync(
            _ctx.UserId,
            new SubscriptionPurchaseRequest
            {
                PlanId = _ctx.PlusPlanId,
                Gateway = PaymentGateways.Behpardakht
            });

        ApiTestAssertions.AssertSuccess(purchase);
        var payment = await _ctx.GetPaymentAsync(purchase.Data!.PaymentId!.Value);

        await _ctx.ActivationService.FulfillVerifiedPaymentAsync(payment);

        var active = await _ctx.EntitlementService.GetActiveSubscriptionAsync(_ctx.UserId);
        Assert.NotNull(active);
        Assert.Equal(_ctx.PlusPlanId, active!.SubscriptionPlanId);
    }

    [Fact]
    public async Task FulfillPayment_CalledTwice_IsIdempotent()
    {
        var purchase = await _ctx.PurchaseService.InitiatePurchaseAsync(
            _ctx.UserId,
            new SubscriptionPurchaseRequest
            {
                PlanId = _ctx.GoldPlanId,
                Gateway = PaymentGateways.Behpardakht
            });

        var payment = await _ctx.GetPaymentAsync(purchase.Data!.PaymentId!.Value);

        await _ctx.ActivationService.FulfillVerifiedPaymentAsync(payment);
        await _ctx.ActivationService.FulfillVerifiedPaymentAsync(payment);

        var count = await _ctx.CountSubscriptionsForPaymentAsync(payment.Id);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task FulfillPayment_AmountMismatch_ThrowsControlledAppException()
    {
        var purchase = await _ctx.PurchaseService.InitiatePurchaseAsync(
            _ctx.UserId,
            new SubscriptionPurchaseRequest
            {
                PlanId = _ctx.PlusPlanId,
                Gateway = PaymentGateways.Behpardakht
            });

        var payment = await _ctx.GetPaymentAsync(purchase.Data!.PaymentId!.Value);
        payment.Amount += 1;
        await _ctx.SavePaymentAsync(payment);

        var ex = await Assert.ThrowsAsync<AppException>(() =>
            _ctx.ActivationService.FulfillVerifiedPaymentAsync(payment));

        Assert.Equal(ErrorCodes.PaymentFailed, ex.ErrorCode);
        Assert.NotEqual(500, ex.StatusCode);
    }

    [Fact]
    public async Task Discount_CreateDuplicateCode_Returns400()
    {
        var code = await _ctx.CreateDiscountCodeAsync(10_000);

        var duplicate = await _ctx.DiscountService.CreateAsync(new CreateSubscriptionDiscountCodeDto
        {
            Code = code,
            DiscountType = SubscriptionDiscountTypes.Fixed,
            Value = 5_000,
            IsActive = true
        });

        ApiTestAssertions.AssertClientError(duplicate, 400);
    }

    private async Task ActivatePlusPlanForUserAsync()
    {
        var plan = await _ctx.GetPlanAsync(_ctx.PlusPlanId);
        await _ctx.ActivationService.ActivateAsync(
            _ctx.UserId,
            plan,
            plan.Price,
            0,
            null,
            null,
            null);
    }
}
