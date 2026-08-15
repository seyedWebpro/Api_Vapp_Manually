namespace Api_Vapp.DTOs.Auth
{
    public class SendOtpResponseDto
    {
        public int StatusCode { get; set; }
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;

        /// <summary>کد خطای استاندارد برای کلاینت (مثلاً OTP_RATE_LIMITED)</summary>
        public string? ErrorCode { get; set; }

        public int ExpiresInSeconds { get; set; } // زمان انقضای OTP

        /// <summary>
        /// در موفقیت: حداقل فاصله تا ارسال مجدد.
        /// در 429: ثانیه‌های باقی‌مانده تا مجاز شدن درخواست بعدی.
        /// </summary>
        public int? RetryAfterSeconds { get; set; }

        public List<string>? Errors { get; set; }

        /// <summary>برای پیگیری پشتیبانی — با TraceId در لاگ سرور جور است</summary>
        public string? TraceId { get; set; }

        // DEV ONLY — TODO(production): قبل از release این property را حذف کنید (جستجو: OtpCode)
        public string? OtpCode { get; set; }
    }
}



