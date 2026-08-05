using Api_Vapp.DTOs.Sms;

namespace Api_Vapp.Interfaces
{
    /// <summary>
    /// ارسال پیامک با کسر از کیف پول کاربر اپ.
    /// کمبود موجودی → ارسال نمی‌شود، ولی فراخوان‌کننده نباید عملیات اصلی را fail کند.
    /// </summary>
    public interface IUserSmsBillingService
    {
        /// <summary>
        /// هزینه تخمینی یک پیام (بر اساس تعرفه و تعداد پارت).
        /// </summary>
        Task<(decimal Cost, int PartsCount)> EstimateCostAsync(
            string message,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// ارسال پیامک و در صورت موفقیت، کسر از کیف پول.
        /// اگر صورتحساب خاموش باشد، بدون کسر ارسال می‌کند.
        /// اگر موجودی کافی نباشد، ارسال را رد می‌کند (بدون exception).
        /// </summary>
        Task<UserSmsSendResult> TrySendAsync(
            int userId,
            string mobile,
            string message,
            string sourceModule,
            string walletTitle,
            string? walletDescription = null,
            int? sourceEntityId = null,
            string? sourceEntityLabel = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// ارسال OTP پولی (مثلاً تأیید شماره در فرم/گردونه عمومی).
        /// OTPهای احراز هویت (ورود/ثبت‌نام/فراموشی رمز) از این مسیر استفاده نکنند.
        /// </summary>
        Task<UserSmsSendResult> TrySendOtpAsync(
            int userId,
            string mobile,
            string otpCode,
            string templateType,
            string sourceModule,
            string walletTitle,
            string? walletDescription = null,
            int? sourceEntityId = null,
            string? sourceEntityLabel = null,
            CancellationToken cancellationToken = default);
    }
}
