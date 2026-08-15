using Api_Vapp.Constants;
using Api_Vapp.DTOs.Sms;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;
using Api_Vapp.Services.Audit;
using Api_Vapp.Utilities;
using Microsoft.Extensions.Configuration;
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
        private readonly IAuditService _audit;
        private readonly ILogger<UserSmsBillingService> _logger;
        private readonly string? _otpAutofillDomain;
        private readonly string? _androidAppHash;

        public UserSmsBillingService(
            ISmsService smsService,
            ISmsPricingService smsPricing,
            IWalletService walletService,
            ISmsDeliveryTrackingService deliveryTracking,
            IAuditService audit,
            ILogger<UserSmsBillingService> logger,
            IConfiguration configuration)
        {
            _smsService = smsService;
            _smsPricing = smsPricing;
            _walletService = walletService;
            _deliveryTracking = deliveryTracking;
            _audit = audit;
            _logger = logger;
            _otpAutofillDomain = configuration["Sms:OtpAutofillDomain"];
            _androidAppHash = configuration["Sms:AndroidAppHash"];
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

                await WriteSmsAuditAsync(
                    AuditActions.SmsSendFailed,
                    userId,
                    mobile,
                    sourceModule,
                    sourceEntityId,
                    cost: 0,
                    parts: analysis.PartsCount,
                    succeeded: false,
                    providerSid: null,
                    reason: "ExceedsMaxPages");

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

                    await WriteSmsAuditAsync(
                        AuditActions.SmsInsufficientBalance,
                        userId,
                        mobile,
                        sourceModule,
                        sourceEntityId,
                        cost,
                        parts,
                        succeeded: false,
                        providerSid: null,
                        reason: "InsufficientBalance");

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
                        "SMS provider failed after wallet reserve. UserId={UserId}, Module={Module}, Message={Message}, ProviderStatus={Status}",
                        userId, sourceModule, smsResult.Message, smsResult.Data?.Status);

                    await WriteSmsAuditAsync(
                        AuditActions.SmsSendFailed,
                        userId,
                        mobile,
                        sourceModule,
                        sourceEntityId,
                        cost,
                        parts,
                        succeeded: false,
                        providerSid: smsResult.Data?.Sid,
                        reason: "ProviderFailed",
                        providerStatus: smsResult.Data?.Status,
                        chargedThenRefunded: reserved > 0);

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

                await WriteSmsAuditAsync(
                    AuditActions.SmsSendSucceeded,
                    userId,
                    mobile,
                    sourceModule,
                    sourceEntityId,
                    cost,
                    parts,
                    succeeded: true,
                    providerSid: sid,
                    reason: null,
                    providerStatus: smsResult.Data.Status,
                    chargedAmount: reserved);

                return UserSmsSendResult.Success(sid, cost, parts, chargedAmount: reserved);
            }
            catch (Exception ex)
            {
                await RefundIfNeededAsync(userId, reserved, sourceModule, "خطای غیرمنتظره هنگام ارسال");
                _logger.LogError(ex, "Error sending billed SMS. UserId={UserId}, Module={Module}", userId, sourceModule);

                await WriteSmsAuditAsync(
                    AuditActions.SmsSendFailed,
                    userId,
                    mobile,
                    sourceModule,
                    sourceEntityId,
                    cost,
                    parts,
                    succeeded: false,
                    providerSid: null,
                    reason: "Exception",
                    chargedThenRefunded: reserved > 0);

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
            var message = OtpSmsMessageBuilder.BuildForSend(
                otpCode,
                templateType,
                _otpAutofillDomain,
                _androidAppHash);
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

        private Task WriteSmsAuditAsync(
            string action,
            int userId,
            string mobile,
            string sourceModule,
            int? sourceEntityId,
            decimal cost,
            int parts,
            bool succeeded,
            long? providerSid,
            string? reason,
            int? providerStatus = null,
            decimal? chargedAmount = null,
            bool chargedThenRefunded = false)
        {
            // شماره را فقط به صورت ماسک‌شده در audit نگه می‌داریم
            var masked = MaskMobile(mobile);
            return _audit.WriteAsync(new AuditEntry
            {
                Category = AuditCategories.Sms,
                Action = action,
                EntityType = AuditEntityTypes.SmsSend,
                EntityId = sourceEntityId?.ToString() ?? userId.ToString(),
                ActorUserId = userId,
                TargetUserId = userId,
                Succeeded = succeeded,
                ErrorMessage = reason,
                Metadata = new
                {
                    mobileMasked = masked,
                    sourceModule,
                    sourceEntityId,
                    cost,
                    parts,
                    providerSid,
                    providerStatus,
                    chargedAmount,
                    chargedThenRefunded
                }
            });
        }

        private static string MaskMobile(string mobile)
        {
            var digits = new string(mobile.Where(char.IsDigit).ToArray());
            if (digits.Length < 4)
                return "****";
            return $"{digits[..2]}****{digits[^2..]}";
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
    }
}
