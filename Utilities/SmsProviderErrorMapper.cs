using Api_Vapp.DTOs.Common;
using System.Net.Sockets;

namespace Api_Vapp.Utilities
{
    /// <summary>
    /// نگاشت خطای پنل پیامک به پیام کنترل‌شده فارسی (هرگز متن خام پنل به کاربر نمی‌رود).
    /// </summary>
    public static class SmsProviderErrorMapper
    {
        public sealed record MappedError(string UserMessage, string ErrorCode, bool IsNonRetryable);

        public static MappedError Map(int status, string? providerMessage)
        {
            var provider = providerMessage ?? string.Empty;
            var lower = provider.ToLowerInvariant();

            if (status == -15 ||
                ContainsAny(lower, "تکراری", "duplicate", "مجاز به ارسال پیام تکراری"))
            {
                return new MappedError(
                    ControlledErrorHelper.SmsDuplicateTooSoon,
                    ErrorCodes.SmsDuplicate,
                    IsNonRetryable: true);
            }

            if (ContainsAny(lower, "شماره نامعتبر", "invalid number", "invalid mobile"))
            {
                return new MappedError(
                    ControlledErrorHelper.SmsInvalidNumber,
                    ErrorCodes.SmsInvalidNumber,
                    IsNonRetryable: true);
            }

            if (ContainsAny(lower, "blacklist", "لیست سیاه", "مشترک در لیست سیاه"))
            {
                return new MappedError(
                    ControlledErrorHelper.SmsBlacklisted,
                    ErrorCodes.SmsBlacklisted,
                    IsNonRetryable: true);
            }

            // سایر Status منفی پنل — غیرقابل retry، پیام عمومی کنترل‌شده
            if (status < 0)
            {
                return new MappedError(
                    ControlledErrorHelper.SmsFailed,
                    ErrorCodes.SmsFailed,
                    IsNonRetryable: true);
            }

            return new MappedError(
                ControlledErrorHelper.SmsFailed,
                ErrorCodes.SmsFailed,
                IsNonRetryable: false);
        }

        /// <summary>خطاهای شبکه/DNS هنگام تماس با پنل</summary>
        public static MappedError MapException(Exception ex)
        {
            if (IsTransientNetworkFailure(ex))
            {
                return new MappedError(
                    ControlledErrorHelper.SmsTemporarilyUnavailable,
                    ErrorCodes.SmsTemporarilyUnavailable,
                    IsNonRetryable: false);
            }

            return new MappedError(
                ControlledErrorHelper.SmsFailed,
                ErrorCodes.SmsFailed,
                IsNonRetryable: false);
        }

        public static bool IsTransientNetworkFailure(Exception ex)
        {
            for (Exception? current = ex; current != null; current = current.InnerException)
            {
                if (current is HttpRequestException or SocketException or TaskCanceledException or TimeoutException)
                    return true;

                var msg = current.Message ?? string.Empty;
                if (msg.Contains("Name or service not known", StringComparison.OrdinalIgnoreCase) ||
                    msg.Contains("No such host", StringComparison.OrdinalIgnoreCase) ||
                    msg.Contains("Connection refused", StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static bool ContainsAny(string haystack, params string[] needles)
            => needles.Any(n => haystack.Contains(n, StringComparison.OrdinalIgnoreCase));
    }
}
