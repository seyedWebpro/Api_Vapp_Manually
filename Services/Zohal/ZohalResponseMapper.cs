using System.Net;

namespace Api_Vapp.Services.Zohal
{
    internal static class ZohalResponseMapper
    {
        public static ShahkarVerificationStatus MapOutcome(
            int httpStatusCode,
            int zohalResult,
            bool? matched,
            string? providerErrorCode,
            string? providerMessage)
        {
            if (zohalResult == 1)
            {
                return matched == true
                    ? ShahkarVerificationStatus.Matched
                    : ShahkarVerificationStatus.NotMatched;
            }

            var errorCode = providerErrorCode?.Trim().ToLowerInvariant() ?? string.Empty;
            var message = providerMessage?.Trim().ToLowerInvariant() ?? string.Empty;

            if (IsAuthFailure(httpStatusCode, errorCode, message))
                return ShahkarVerificationStatus.ProviderAuthFailed;

            if (IsInsufficientBalance(errorCode, message))
                return ShahkarVerificationStatus.InsufficientBalance;

            if (IsIpRestriction(httpStatusCode, errorCode, message))
                return ShahkarVerificationStatus.IpNotAllowed;

            if (errorCode is "permission_denied" or "forbidden")
                return ShahkarVerificationStatus.IpNotAllowed;

            if (zohalResult == 6 || httpStatusCode == (int)HttpStatusCode.BadRequest)
            {
                if (IsInvalidNationalCode(errorCode, message))
                    return ShahkarVerificationStatus.InvalidInput;

                return ShahkarVerificationStatus.InvalidInput;
            }

            if (zohalResult == 4)
                return ShahkarVerificationStatus.InvalidInput;

            if (zohalResult == 5
                || string.Equals(errorCode, "internal_error", StringComparison.OrdinalIgnoreCase)
                || string.Equals(errorCode, "service_unavailable", StringComparison.OrdinalIgnoreCase)
                || httpStatusCode == (int)HttpStatusCode.ServiceUnavailable
                || httpStatusCode == (int)HttpStatusCode.InternalServerError)
            {
                return ShahkarVerificationStatus.ServiceUnavailable;
            }

            return ShahkarVerificationStatus.ServiceUnavailable;
        }

        private static bool IsAuthFailure(int httpStatusCode, string errorCode, string message) =>
            httpStatusCode == (int)HttpStatusCode.Unauthorized
            || errorCode is "not_authenticated" or "unauthorized" or "invalid_token"
            || message.Contains("not_authenticated", StringComparison.Ordinal)
            || message.Contains("اعتبارسنجی", StringComparison.Ordinal);

        private static bool IsInsufficientBalance(string errorCode, string message) =>
            ContainsAny(errorCode, message,
                "insufficient", "balance", "credit", "wallet", "not_enough",
                "low_balance", "شارژ", "اعتبار", "موجودی");

        private static bool IsIpRestriction(int httpStatusCode, string errorCode, string message) =>
            httpStatusCode == (int)HttpStatusCode.Forbidden
            || errorCode.Contains("ip", StringComparison.Ordinal)
            || message.Contains("ip", StringComparison.Ordinal)
            || message.Contains("whitelist", StringComparison.Ordinal);

        private static bool IsInvalidNationalCode(string errorCode, string message) =>
            errorCode is "invalid" or "national_id_invalid"
            || message.Contains("national_code", StringComparison.Ordinal)
            || message.Contains("national code", StringComparison.Ordinal);

        private static bool ContainsAny(string errorCode, string message, params string[] needles)
        {
            foreach (var needle in needles)
            {
                if (errorCode.Contains(needle, StringComparison.OrdinalIgnoreCase)
                    || message.Contains(needle, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
