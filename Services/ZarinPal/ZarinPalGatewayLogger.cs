using Api_Vapp.Utilities;
using Microsoft.Extensions.Logging;

namespace Api_Vapp.Services.ZarinPal
{
    /// <summary>
    /// لاگ یکنواخت و قابل‌جستجوی درگاه زرین‌پال (پیشوند ZARINPAL_).
    /// هرگز MerchantId یا body کامل حاوی secret را لاگ نکن.
    /// </summary>
    internal static class ZarinPalGatewayLogger
    {
        private const int MaxBody = 800;

        public static string AuthorityPrefix(string? authority)
        {
            if (string.IsNullOrWhiteSpace(authority))
                return "";
            var a = authority.Trim();
            return a.Length <= 12 ? a : a[..12];
        }

        public static string TraceId() => ControlledErrorHelper.GetTraceId();

        public static void RequestStart(
            ILogger logger,
            bool sandbox,
            int amountToman,
            string currency,
            string? orderId,
            string? callbackHost,
            string? description)
        {
            logger.LogInformation(
                "ZARINPAL_REQUEST_START TraceId={TraceId} Sandbox={Sandbox} AmountToman={Amount} Currency={Currency} OrderId={OrderId} CallbackHost={CallbackHost} Description={Description}",
                TraceId(), sandbox, amountToman, currency, orderId, callbackHost, Truncate(description, 120));
        }

        public static void RequestOk(
            ILogger logger,
            bool sandbox,
            int amountToman,
            string? authority,
            int? fee,
            string? feeType,
            int httpStatus,
            long durationMs)
        {
            logger.LogInformation(
                "ZARINPAL_REQUEST_OK TraceId={TraceId} Sandbox={Sandbox} AmountToman={Amount} AuthorityPrefix={AuthorityPrefix} Fee={Fee} FeeType={FeeType} HttpStatus={HttpStatus} DurationMs={DurationMs}",
                TraceId(), sandbox, amountToman, AuthorityPrefix(authority), fee, feeType, httpStatus, durationMs);
        }

        public static void RequestFail(
            ILogger logger,
            bool sandbox,
            int amountToman,
            int code,
            string hint,
            int httpStatus,
            long durationMs,
            string? body)
        {
            logger.LogWarning(
                "ZARINPAL_REQUEST_FAIL TraceId={TraceId} Sandbox={Sandbox} AmountToman={Amount} Code={Code} Hint={Hint} HttpStatus={HttpStatus} DurationMs={DurationMs} Body={Body}",
                TraceId(), sandbox, amountToman, code, hint, httpStatus, durationMs, Truncate(body, MaxBody));
        }

        public static void VerifyStart(
            ILogger logger,
            bool sandbox,
            int amountToman,
            string? authority)
        {
            logger.LogInformation(
                "ZARINPAL_VERIFY_START TraceId={TraceId} Sandbox={Sandbox} AmountToman={Amount} AuthorityPrefix={AuthorityPrefix}",
                TraceId(), sandbox, amountToman, AuthorityPrefix(authority));
        }

        public static void VerifyOk(
            ILogger logger,
            bool sandbox,
            int amountToman,
            string? authority,
            int code,
            bool alreadyVerified,
            string? refId,
            string? cardPan,
            int? fee,
            string? feeType,
            int httpStatus,
            long durationMs)
        {
            logger.LogInformation(
                "ZARINPAL_VERIFY_OK TraceId={TraceId} Sandbox={Sandbox} AmountToman={Amount} AuthorityPrefix={AuthorityPrefix} Code={Code} AlreadyVerified={AlreadyVerified} RefId={RefId} CardPan={CardPan} Fee={Fee} FeeType={FeeType} HttpStatus={HttpStatus} DurationMs={DurationMs}",
                TraceId(), sandbox, amountToman, AuthorityPrefix(authority), code, alreadyVerified, refId,
                MaskPan(cardPan), fee, feeType, httpStatus, durationMs);
        }

        public static void VerifyFail(
            ILogger logger,
            bool sandbox,
            int amountToman,
            string? authority,
            int code,
            string hint,
            bool definitive,
            int httpStatus,
            long durationMs,
            string? body)
        {
            logger.LogWarning(
                "ZARINPAL_VERIFY_FAIL TraceId={TraceId} Sandbox={Sandbox} AmountToman={Amount} AuthorityPrefix={AuthorityPrefix} Code={Code} Hint={Hint} Definitive={Definitive} HttpStatus={HttpStatus} DurationMs={DurationMs} Body={Body}",
                TraceId(), sandbox, amountToman, AuthorityPrefix(authority), code, hint, definitive, httpStatus, durationMs,
                Truncate(body, MaxBody));
        }

        public static void TransportError(
            ILogger logger,
            string operation,
            bool sandbox,
            Exception ex,
            long durationMs)
        {
            logger.LogError(
                ex,
                "ZARINPAL_TRANSPORT_ERROR TraceId={TraceId} Operation={Operation} Sandbox={Sandbox} DurationMs={DurationMs} ExceptionType={ExceptionType}",
                TraceId(), operation, sandbox, durationMs, ex.GetType().Name);
        }

        private static string? MaskPan(string? pan)
        {
            if (string.IsNullOrWhiteSpace(pan))
                return pan;
            var digits = new string(pan.Where(char.IsDigit).ToArray());
            if (digits.Length < 4)
                return "****";
            return $"******{digits[^4..]}";
        }

        private static string Truncate(string? value, int max)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;
            // هرگز merchant_id را در body لاگ نکن
            var scrubbed = value.Replace("merchant_id", "merchant_id_redacted", StringComparison.OrdinalIgnoreCase);
            return scrubbed.Length <= max ? scrubbed : scrubbed[..max] + "...";
        }
    }
}
