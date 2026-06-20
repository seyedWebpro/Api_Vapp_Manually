using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Api_Vapp.Utilities
{
    /// <summary>
    /// پیام سطح بالای پاسخ 400 اعتبارسنجی — بدون نمایش «خطای اعتبارسنجی اطلاعات ورودی» برای OTP
    /// </summary>
    public static class ValidationResponseMessageResolver
    {
        private const string GenericValidationMessage = "خطای اعتبارسنجی اطلاعات ورودی";

        private static readonly string[] OtpVerifyPathSegments =
        {
            "verify-login",
            "verify-registration",
            "reset-password",
            "admin/verify-login",
        };

        public static string Resolve(
            ModelStateDictionary modelState,
            string requestPath,
            IReadOnlyList<string> errors)
        {
            if (IsOtpVerifyPath(requestPath) && HasFieldError(modelState, "OtpCode"))
            {
                return ControlledErrorHelper.OtpIncorrect;
            }

            if (errors.Count == 1 && ControlledErrorHelper.IsSafeUserMessage(errors[0]))
            {
                return errors[0];
            }

            return GenericValidationMessage;
        }

        private static bool IsOtpVerifyPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            return OtpVerifyPathSegments.Any(segment =>
                path.Contains(segment, StringComparison.OrdinalIgnoreCase));
        }

        private static bool HasFieldError(ModelStateDictionary modelState, string fieldName)
        {
            foreach (var key in modelState.Keys)
            {
                if (!key.Equals(fieldName, StringComparison.OrdinalIgnoreCase)
                    && !key.EndsWith($".{fieldName}", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (modelState[key]?.Errors.Count > 0)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
