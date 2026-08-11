using Api_Vapp.Constants;
using Api_Vapp.DTOs.Sms;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;
using Api_Vapp.Utilities;
using Microsoft.Extensions.Logging;

namespace Api_Vapp.Services
{
    /// <summary>
    /// لایه یکپارچه ارسال پیامک پولی از کیف پول کاربر اپ.
    /// الگوی مالی: اول کسر اتمیک → سپس ارسال → در صورت شکست ارسال، برگشت موجودی.
    /// کمبود موجودی = پیامک ارسال نمی‌شود؛ عملیات اصلی فراخوان‌کننده می‌تواند ادامه یابد.
    /// </summary>
    public class UserSmsBillingService : IUserSmsBillingService
    {
        private readonly ISmsService _smsService;
        private readonly ISmsPricingService _smsPricing;
        private readonly IWalletService _walletService;
        private readonly ISmsDeliveryTrackingService _deliveryTracking;
        private readonly ILogger<UserSmsBillingService> _logger;

        public UserSmsBillingService(
            ISmsService smsService,
            ISmsPricingService smsPricing,
            IWalletService walletService,
            ISmsDeliveryTrackingService deliveryTracking,
            ILogger<UserSmsBillingService> logger)
        {
            _smsService = smsService;
            _smsPricing = smsPricing;
            _walletService = walletService;
            _deliveryTracking = deliveryTracking;
            _logger = logger;
        }

        public async Task<(decimal Cost, int PartsCount)> EstimateCostAsync(
            string message,
            CancellationToken cancellationToken = default)
        {
            var pricing = await _smsPricing.GetRuntimeAsync(cancellationToken);
            var prepared = SmsPartsCalculator.PrepareForSend(message, pricing.Rules);
            if (!SmsPartsCalculator.TryCalculateParts(prepared, pricing.Rules, out var parts, out _))
            {
                parts = Math.Max(1, pricing.Rules.MaxPages);
            }

            var cost = SmsPartsCalculator.CalculateCost(parts, pricing.CostPerPart);
            return (cost, parts);
        }

        public async Task<UserSmsSendResult> TrySendAsync(
            int userId,
            string mobile,
            string message,
            string sourceModule,
            string walletTitle,
            string? walletDescription = null,
            int? sourceEntityId = null,
            string? sourceEntityLabel = null,
            CancellationToken cancellationToken = default)
        {
            if (userId <= 0 || string.IsNullOrWhiteSpace(mobile) || string.IsNullOrWhiteSpace(message))
            {
                return UserSmsSendResult.Failed(0, 0, "پارامترهای ارسال پیامک نامعتبر است");
            }

            var pricing = await _smsPricing.GetRuntimeAsync(cancellationToken);
            var prepared = SmsPartsCalculator.PrepareForSend(message, pricing.Rules);

            if (!SmsPartsCalculator.TryCalculateParts(prepared, pricing.Rules, out var parts, out var analysis)
                || analysis.ExceedsMaxPages)
            {
                _logger.LogWarning(
                    "SMS rejected — exceeds max pages. UserId={UserId}, Parts={Parts}, Max={Max}, Module={Module}",
                    userId, analysis.PartsCount, pricing.Rules.MaxPages, sourceModule);

                return UserSmsSendResult.Failed(
                    0,
                    analysis.PartsCount,
                    $"تعداد صفحات پیامک از حداکثر مجاز ({pricing.Rules.MaxPages} صفحه) بیشتر است");
            }

            var cost = SmsPartsCalculator.CalculateCost(parts, pricing.CostPerPart);
            var shouldBill = pricing.IsBillingEffectivelyEnabled && cost > 0;
            decimal reserved = 0;

            // اول کسر با قفل — اگر موجودی کافی نباشد هیچ پیامکی ارسال نمی‌شود
            if (shouldBill)
            {
                var deduct = await _walletService.DeductBalanceAsync(
                    userId,
                    cost,
                    walletTitle,
                    walletDescription ?? $"هزینه ارسال پیامک ({parts} پارت) — {sourceModule}");

                if (!deduct.Success)
                {
                    _logger.LogInformation(
                        "SMS skipped — insufficient wallet (pre-deduct). UserId={UserId}, Cost={Cost}, Module={Module}",
                        userId, cost, sourceModule);

                    return UserSmsSendResult.Skipped(cost, parts);
                }

                reserved = cost;
            }

            try
            {
                var smsResult = await _smsService.SendSmsAsync(new SendSmsRequestDto
                {
                    Mobile = mobile,
                    Message = prepared
                });

                var isSuccess = smsResult.Success && smsResult.Data != null &&
                                (smsResult.Data.Sid > 0 || smsResult.Data.Status > 0);

                if (!isSuccess)
                {
                    await RefundIfNeededAsync(userId, reserved, sourceModule, "ارسال ناموفق به سرویس پیامک");
                    _logger.LogWarning(
                        "SMS provider failed after wallet reserve. UserId={UserId}, Module={Module}, Message={Message}",
                        userId, sourceModule, smsResult.Message);

                    return UserSmsSendResult.Failed(cost, parts, ControlledErrorHelper.SmsFailed);
                }

                var sid = smsResult.Data!.Sid;

                await _deliveryTracking.TrackSuccessfulSendAsync(new SmsDeliveryTrackRequestDto
                {
                    UserId = userId,
                    SourceModule = sourceModule,
                    SourceEntityId = sourceEntityId,
                    SourceEntityLabel = sourceEntityLabel,
                    Mobile = mobile,
                    Sid = sid,
                    MessageText = prepared
                });

                return UserSmsSendResult.Success(sid, cost, parts, chargedAmount: reserved);
            }
            catch (Exception ex)
            {
                await RefundIfNeededAsync(userId, reserved, sourceModule, "خطای غیرمنتظره هنگام ارسال");
                _logger.LogError(ex, "Error sending billed SMS. UserId={UserId}, Module={Module}", userId, sourceModule);
                return UserSmsSendResult.Failed(cost, parts, ControlledErrorHelper.SmsFailed);
            }
        }

        public async Task<UserSmsSendResult> TrySendOtpAsync(
            int userId,
            string mobile,
            string otpCode,
            string templateType,
            string sourceModule,
            string walletTitle,
            string? walletDescription = null,
            int? sourceEntityId = null,
            string? sourceEntityLabel = null,
            CancellationToken cancellationToken = default)
        {
            var message = BuildOtpMessage(otpCode, templateType);
            return await TrySendAsync(
                userId,
                mobile,
                message,
                sourceModule,
                walletTitle,
                walletDescription,
                sourceEntityId,
                sourceEntityLabel,
                cancellationToken);
        }

        private async Task RefundIfNeededAsync(int userId, decimal reserved, string sourceModule, string reason)
        {
            if (reserved <= 0)
                return;

            try
            {
                var refund = await _walletService.AddBalanceAsync(
                    userId,
                    reserved,
                    WalletTransactionTypes.Refund,
                    "برگشت هزینه پیامک",
                    $"برگشت رزرو پیامک — {sourceModule} — {reason}",
                    sendPushNotification: false);

                if (!refund.Success)
                {
                    _logger.LogError(
                        "CRITICAL: SMS wallet refund failed. UserId={UserId}, Amount={Amount}, Module={Module}, Reason={Reason}, Error={Error}",
                        userId, reserved, sourceModule, reason, refund.Message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "CRITICAL: SMS wallet refund exception. UserId={UserId}, Amount={Amount}, Module={Module}",
                    userId, reserved, sourceModule);
            }
        }

        private static string BuildOtpMessage(string otpCode, string templateType) =>
            templateType switch
            {
                "ResetPassword" => $"کد بازیابی رمز عبور: {otpCode}",
                "Register" => $"کد تایید ثبت نام: {otpCode}",
                "ForgotPassword" => $"کد بازیابی رمز عبور: {otpCode}",
                "Registration" => $"کد تایید ثبت نام: {otpCode}",
                _ => $"کد تایید شما: {otpCode}"
            };
    }
}
