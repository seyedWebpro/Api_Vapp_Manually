namespace Api_Vapp.Services.Zohal
{
    public enum ShahkarVerificationStatus
    {
        Matched,
        NotMatched,
        InvalidInput,
        ServiceUnavailable,
        InsufficientBalance,
        ProviderAuthFailed,
        IpNotAllowed,
        Skipped
    }

    /// <summary>
    /// نتیجه استعلام شاهکار — بدون افشای جزئیات API خارجی به کاربر
    /// </summary>
    public sealed class ShahkarVerificationResult
    {
        public ShahkarVerificationStatus Status { get; init; }

        public long? InquiryLogId { get; init; }

        public int? HttpStatusCode { get; init; }

        public int? ZohalResultCode { get; init; }

        public string? ProviderErrorCode { get; init; }

        public bool IsMatched => Status == ShahkarVerificationStatus.Matched;

        public bool IsSkipped => Status == ShahkarVerificationStatus.Skipped;

        public static ShahkarVerificationResult Matched(long? logId = null) =>
            new() { Status = ShahkarVerificationStatus.Matched, InquiryLogId = logId };

        public static ShahkarVerificationResult NotMatched(long? logId = null) =>
            new() { Status = ShahkarVerificationStatus.NotMatched, InquiryLogId = logId };

        public static ShahkarVerificationResult InvalidInput(long? logId = null, string? providerErrorCode = null) =>
            new()
            {
                Status = ShahkarVerificationStatus.InvalidInput,
                InquiryLogId = logId,
                ProviderErrorCode = providerErrorCode
            };

        public static ShahkarVerificationResult ServiceUnavailable(
            long? logId = null,
            int? httpStatusCode = null,
            int? zohalResultCode = null,
            string? providerErrorCode = null) =>
            new()
            {
                Status = ShahkarVerificationStatus.ServiceUnavailable,
                InquiryLogId = logId,
                HttpStatusCode = httpStatusCode,
                ZohalResultCode = zohalResultCode,
                ProviderErrorCode = providerErrorCode
            };

        public static ShahkarVerificationResult InsufficientBalance(long? logId = null, string? providerErrorCode = null) =>
            new()
            {
                Status = ShahkarVerificationStatus.InsufficientBalance,
                InquiryLogId = logId,
                ProviderErrorCode = providerErrorCode
            };

        public static ShahkarVerificationResult ProviderAuthFailed(long? logId = null, string? providerErrorCode = null) =>
            new()
            {
                Status = ShahkarVerificationStatus.ProviderAuthFailed,
                InquiryLogId = logId,
                ProviderErrorCode = providerErrorCode
            };

        public static ShahkarVerificationResult IpNotAllowed(long? logId = null) =>
            new() { Status = ShahkarVerificationStatus.IpNotAllowed, InquiryLogId = logId };

        public static ShahkarVerificationResult Skipped() =>
            new() { Status = ShahkarVerificationStatus.Skipped };
    }
}
