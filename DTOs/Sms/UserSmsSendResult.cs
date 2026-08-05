namespace Api_Vapp.DTOs.Sms
{
    /// <summary>
    /// نتیجه تلاش برای ارسال پیامک پولی از کیف پول کاربر.
    /// کمبود موجودی باعث خطا نمی‌شود — فقط ارسال رد می‌شود.
    /// </summary>
    public sealed class UserSmsSendResult
    {
        public bool Sent { get; init; }

        /// <summary>به خاطر کمبود موجودی کیف پول ارسال نشد (عملیات اصلی باید ادامه یابد).</summary>
        public bool SkippedInsufficientBalance { get; init; }

        /// <summary>ارسال به پنل پیامک ناموفق بود.</summary>
        public bool ProviderFailed { get; init; }

        public long? Sid { get; init; }

        public decimal Cost { get; init; }

        public decimal ChargedAmount { get; init; }

        public int PartsCount { get; init; }

        public string? Message { get; init; }

        public static UserSmsSendResult Skipped(decimal cost, int partsCount, string? message = null) => new()
        {
            Sent = false,
            SkippedInsufficientBalance = true,
            Cost = cost,
            PartsCount = partsCount,
            Message = message ?? "موجودی کیف پول برای ارسال پیامک کافی نیست"
        };

        public static UserSmsSendResult Failed(decimal cost, int partsCount, string? message = null) => new()
        {
            Sent = false,
            ProviderFailed = true,
            Cost = cost,
            PartsCount = partsCount,
            Message = message ?? "ارسال پیامک ناموفق بود"
        };

        public static UserSmsSendResult Success(long sid, decimal cost, int partsCount, decimal chargedAmount) => new()
        {
            Sent = true,
            Sid = sid,
            Cost = cost,
            PartsCount = partsCount,
            ChargedAmount = chargedAmount
        };
    }
}
