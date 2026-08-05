using Api_Vapp.DTOs.Sms;
using Api_Vapp.Interfaces;
using Microsoft.Extensions.Logging;

namespace Api_Vapp.Services
{
    /// <summary>
    /// لایه یکپارچه ارسال پیامک پولی از کیف پول کاربر اپ.
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
            var parts = SafePartsCount(message, pricing.Rules);
            var cost = Math.Round(pricing.CostPerPart * parts, 2, MidpointRounding.AwayFromZero);
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
            var prepared = EnsureOptOutSuffix(message, pricing.Rules.OptOutSuffix);
            var parts = SafePartsCount(prepared, pricing.Rules);
            var cost = Math.Round(pricing.CostPerPart * parts, 2, MidpointRounding.AwayFromZero);

            if (pricing.IsBillingEffectivelyEnabled && cost > 0)
            {
                var balance = await _walletService.GetBalanceAsync(userId);
                if (balance < cost)
                {
                    _logger.LogInformation(
                        "SMS skipped — insufficient wallet. UserId={UserId}, Cost={Cost}, Balance={Balance}, Module={Module}",
                        userId, cost, balance, sourceModule);

                    return UserSmsSendResult.Skipped(cost, parts);
                }
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
                    _logger.LogWarning(
                        "SMS provider failed. UserId={UserId}, Module={Module}, Message={Message}",
                        userId, sourceModule, smsResult.Message);

                    return UserSmsSendResult.Failed(cost, parts, smsResult.Message);
                }

                var sid = smsResult.Data!.Sid;
                decimal charged = 0;

                if (pricing.IsBillingEffectivelyEnabled && cost > 0)
                {
                    var deduct = await _walletService.DeductBalanceAsync(
                        userId,
                        cost,
                        walletTitle,
                        walletDescription ?? $"هزینه ارسال پیامک ({parts} پارت) — {sourceModule}");

                    if (!deduct.Success)
                    {
                        // ارسال انجام شده اما کسر ناموفق؛ لاگ هشدار — پیامک را برگشت نمی‌زنیم
                        _logger.LogWarning(
                            "SMS sent but wallet deduct failed. UserId={UserId}, Cost={Cost}, Sid={Sid}, Error={Error}",
                            userId, cost, sid, deduct.Message);
                    }
                    else
                    {
                        charged = cost;
                    }
                }

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

                return UserSmsSendResult.Success(sid, cost, parts, charged);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending billed SMS. UserId={UserId}, Module={Module}", userId, sourceModule);
                return UserSmsSendResult.Failed(cost, parts, ex.Message);
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

        private static string BuildOtpMessage(string otpCode, string templateType) =>
            templateType switch
            {
                "ResetPassword" => $"کد بازیابی رمز عبور: {otpCode}",
                "Register" => $"کد تایید ثبت نام: {otpCode}",
                "ForgotPassword" => $"کد بازیابی رمز عبور: {otpCode}",
                "Registration" => $"کد تایید ثبت نام: {otpCode}",
                _ => $"کد تایید شما: {otpCode}"
            };

        private static string EnsureOptOutSuffix(string message, string? optOutSuffix)
        {
            var suffix = string.IsNullOrWhiteSpace(optOutSuffix) ? "لغو11" : optOutSuffix.Trim();
            if (string.IsNullOrWhiteSpace(message))
                return message;

            return message.TrimEnd().EndsWith(suffix, StringComparison.Ordinal)
                ? message
                : $"{message.TrimEnd()}\n{suffix}";
        }

        private static int SafePartsCount(string message, SmsPartsRules rules)
        {
            try
            {
                return SmsPartsCalculator.CalculateParts(message, rules);
            }
            catch (ArgumentException)
            {
                return Math.Max(1, rules.MaxPages);
            }
        }
    }
}
