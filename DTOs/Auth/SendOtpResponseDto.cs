namespace Api_Vapp.DTOs.Auth
{
    public class SendOtpResponseDto
    {
        public int StatusCode { get; set; }
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int ExpiresInSeconds { get; set; } // زمان انقضای OTP
        public int? RetryAfterSeconds { get; set; } // زمان باقی‌مانده تا امکان درخواست مجدد (برای Rate Limit)
        public List<string>? Errors { get; set; } // لیست خطاهای اعتبارسنجی (اختیاری)
    }
}



