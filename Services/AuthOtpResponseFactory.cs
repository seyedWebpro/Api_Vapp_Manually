using Api_Vapp.DTOs.Auth;
using Api_Vapp.DTOs.Common;
using Api_Vapp.Utilities;

namespace Api_Vapp.Services
{
    /// <summary>
    /// پیام‌ها و پاسخ‌های یکنواخت ارسال/ارسال‌مجدد OTP احراز هویت — برای موبایل و کراول.
    /// </summary>
    public static class AuthOtpResponseFactory
    {
        public const int DefaultExpiresInSeconds = 5 * 60;
        public const int DefaultRetryAfterSeconds = 2 * 60;

        public static string FormatRetryWaitMessage(int retryAfterSeconds)
        {
            if (retryAfterSeconds <= 0)
                return "کد تایید اخیراً ارسال شده است. لطفاً کمی صبر کنید و دوباره تلاش کنید";

            if (retryAfterSeconds < 60)
                return $"کد تایید اخیراً ارسال شده است. لطفاً {retryAfterSeconds} ثانیه صبر کنید و دوباره تلاش کنید";

            var minutes = retryAfterSeconds / 60;
            var seconds = retryAfterSeconds % 60;
            if (seconds == 0)
                return $"کد تایید اخیراً ارسال شده است. لطفاً {minutes} دقیقه صبر کنید و دوباره تلاش کنید";

            return $"کد تایید اخیراً ارسال شده است. لطفاً {minutes} دقیقه و {seconds} ثانیه صبر کنید و دوباره تلاش کنید";
        }

        public static SendOtpResponseDto Success(
            string message,
            string? otpCode,
            int expiresInSeconds = DefaultExpiresInSeconds,
            int retryAfterSeconds = DefaultRetryAfterSeconds)
        {
            return new SendOtpResponseDto
            {
                StatusCode = 200,
                Success = true,
                Message = message,
                ExpiresInSeconds = expiresInSeconds,
                RetryAfterSeconds = retryAfterSeconds,
                // TODO(remove-before-production) REMOVE_DEV_OTP — کد تایید موقتی در پاسخ برای موبایل؛ قبل از release حذف شود
                OtpCode = otpCode
            };
        }

        public static SendOtpResponseDto RateLimited(int retryAfterSeconds)
        {
            var safe = Math.Max(1, retryAfterSeconds);
            return new SendOtpResponseDto
            {
                StatusCode = 429,
                Success = false,
                Message = FormatRetryWaitMessage(safe),
                ErrorCode = ErrorCodes.OtpRateLimited,
                ExpiresInSeconds = 0,
                RetryAfterSeconds = safe
            };
        }

        public static SendOtpResponseDto SmsFailed() => new()
        {
            StatusCode = 503,
            Success = false,
            Message = ControlledErrorHelper.SmsFailed,
            ErrorCode = ErrorCodes.SmsFailed,
            ExpiresInSeconds = 0
        };

        public static SendOtpResponseDto NotFound(string message) => new()
        {
            StatusCode = 404,
            Success = false,
            Message = message,
            ErrorCode = ErrorCodes.NotFound,
            ExpiresInSeconds = 0
        };

        public static SendOtpResponseDto Forbidden(string message) => new()
        {
            StatusCode = 403,
            Success = false,
            Message = message,
            ErrorCode = ErrorCodes.Forbidden,
            ExpiresInSeconds = 0
        };

        public static SendOtpResponseDto Conflict(string message) => new()
        {
            StatusCode = 409,
            Success = false,
            Message = message,
            ErrorCode = ErrorCodes.InvalidInput,
            ExpiresInSeconds = 0
        };

        public static SendOtpResponseDto BadRequest(string message, List<string>? errors = null) => new()
        {
            StatusCode = 400,
            Success = false,
            Message = message,
            ErrorCode = ErrorCodes.ValidationFailed,
            ExpiresInSeconds = 0,
            Errors = errors
        };
    }
}
