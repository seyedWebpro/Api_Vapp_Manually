namespace Api_Vapp.Interfaces
{
    /// <summary>
    /// کلاینت ارتباط با API رسمی زرین‌پال (Request / Verify)
    /// </summary>
    public interface IZarinPalGatewayClient
    {
        /// <summary>
        /// ایجاد درخواست پرداخت و دریافت Authority + URL درگاه
        /// </summary>
        Task<ZarinPalRequestResult> RequestPaymentAsync(
            int amountToman,
            string description,
            string callbackUrl,
            string? mobile = null,
            string? email = null,
            string? orderId = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// تأیید پرداخت با Authority و مبلغ ذخیره‌شده در دیتابیس
        /// </summary>
        Task<ZarinPalVerifyResult> VerifyPaymentAsync(
            int amountToman,
            string authority,
            CancellationToken cancellationToken = default);

        /// <summary>ساخت URL هدایت کاربر به صفحه پرداخت</summary>
        string BuildStartPayUrl(string authority);
    }

    public sealed class ZarinPalRequestResult
    {
        public bool Success { get; init; }
        public int Code { get; init; }
        public string? Authority { get; init; }
        public string? PaymentUrl { get; init; }
        public string? ErrorMessage { get; init; }
    }

    public sealed class ZarinPalVerifyResult
    {
        public bool Success { get; init; }
        /// <summary>true وقتی کد 101 باشد (قبلاً Verify شده)</summary>
        public bool AlreadyVerified { get; init; }
        public int Code { get; init; }
        public string? RefId { get; init; }
        public string? CardPan { get; init; }
        public string? CardHash { get; init; }
        public string? ErrorMessage { get; init; }
    }
}
